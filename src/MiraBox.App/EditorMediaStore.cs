using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly Dictionary<(string Project, string Source), string> retainedThemeMediaAliases = [];
    JsonElement RetainThemeAssets(JsonElement data, string projectId)
    {
        var node = JsonNode.Parse(data.GetRawText())!;
        var directory = Path.Combine(DataRoot, "Library", "Themes", projectId);
        Directory.CreateDirectory(directory);
        Visit(node);
        return JsonSerializer.SerializeToElement(node);

        void Visit(JsonNode value)
        {
            if (value is JsonArray array) { foreach (var item in array) if (item != null) Visit(item); return; }
            if (value is not JsonObject obj) return;
            if (obj["kind"]?.GetValue<string>() == "video" && obj["dur"] is JsonValue duration &&
                duration.TryGetValue<double>(out var seconds) && seconds > MiraBox.Core.VideoImportLimits.MaximumDuration.TotalSeconds)
                throw new InvalidOperationException("单个视频不能超过 10 分钟。");
            foreach (var key in obj.Select(pair => pair.Key).ToArray())
            {
                var item = obj[key];
                if (item is JsonValue scalar && scalar.TryGetValue<string>(out var url) &&
                    key is "src" or "url" or "vurl" or "poster" or "videoUrl")
                {
                    if (retainedThemeMediaAliases.TryGetValue((projectId, url), out var retained) &&
                        ThemeAssetPath(retained) is { } retainedPath && File.Exists(retainedPath))
                    { obj[key] = retained; continue; }
                    byte[]? bytes = null;
                    var extension = ".png";
                    if (url.StartsWith("data:", StringComparison.Ordinal))
                    {
                        var comma = url.IndexOf(',');
                        if (comma < 0 || !url[..comma].EndsWith(";base64")) continue;
                        var type = url[5..url.IndexOf(';')];
                        extension = type switch { "image/jpeg" => ".jpg", "video/webm" => ".webm", "video/mp4" => ".mp4", _ => ".png" };
                        bytes = Convert.FromBase64String(url[(comma + 1)..]);
                        if (type.StartsWith("video/", StringComparison.Ordinal) && MiraBox.Core.VideoImportLimits.Exceeded(bytes.LongLength))
                            throw new InvalidOperationException("单个视频不能超过 500 MB。");
                    }
                    else if (ThemeAssetPath(url) is { } source && File.Exists(source))
                    {
                        if (Path.GetFullPath(source).StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                        extension = Path.GetExtension(source);
                        bytes = File.ReadAllBytes(source);
                    }
                    if (bytes == null) continue;
                    var destination = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(bytes)) + extension);
                    if (!File.Exists(destination)) File.WriteAllBytes(destination, bytes);
                    obj[key] = EditorMediaUrl(destination);
                    if (url.StartsWith("https://workspace.local/", StringComparison.Ordinal))
                        retainedThemeMediaAliases[(projectId, url)] = EditorMediaUrl(destination)!;
                }
                else if (item != null) Visit(item);
            }
        }
    }
}
