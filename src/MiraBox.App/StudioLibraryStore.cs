using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;

namespace MiraBox.App;

// Theme metadata stays in the React schema so existing editor projects and references survive round trips.
internal sealed class StudioLibraryStore
{
    readonly string index;
    public string MediaRoot { get; }
    public StudioLibraryStore(string root)
    {
        MediaRoot = Path.Combine(root, "Library");
        Directory.CreateDirectory(MediaRoot);
        index = Path.Combine(root, "library-workspace.json");
    }
    public JsonObject? Load()
    {
        StudioAtomicFile.Recover(index, node => { try { Validate(node!.AsObject()); return true; } catch { return false; } });
        if (!File.Exists(index)) return null;
        var state = JsonNode.Parse(File.ReadAllText(index)) as JsonObject
            ?? throw new InvalidDataException("资源库索引损坏，原文件已保留。");
        Validate(state);
        return state;
    }
    static void Validate(JsonObject state)
    {
        if (state["themes"] is not JsonArray themes || state["playlists"] is not JsonArray || state["schedules"] is not JsonArray)
            throw new InvalidDataException("资源库字段不完整。");
        var ids = new HashSet<string>();
        foreach (var item in themes)
        {
            var id = item?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new InvalidDataException("资源标识为空或重复。");
        }
    }
    public JsonObject Save(JsonObject state)
    {
        Validate(state);
        foreach (var theme in (JsonArray)state["themes"]!) PrepareTheme(theme!);
        StudioAtomicFile.Write(index, state.ToJsonString());
        return state;
    }
    void NormalizeAssets(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var (key, child) in obj.ToArray())
            {
                if (child is JsonValue value && value.TryGetValue<string>(out var text) && text.StartsWith("data:image/", StringComparison.Ordinal))
                {
                    var asset = new JsonObject { ["imageUrl"] = text };
                    PrepareTheme(asset);
                    obj[key] = asset["imageUrl"]!.GetValue<string>();
                }
                else if (child is JsonValue blob && blob.TryGetValue<string>(out var url) && url.StartsWith("blob:", StringComparison.Ordinal))
                    throw new InvalidDataException("主题包含临时素材，请重新导入后保存。");
                else NormalizeAssets(child);
            }
        else if (node is JsonArray array) foreach (var child in array) NormalizeAssets(child);
    }
    public void PrepareTheme(JsonNode theme)
    {
        NormalizeAssets(theme["editorProject"]);
        if (theme["imageUrl"]?.GetValue<string>() is not { } url || !url.StartsWith("data:image/")) return;
        var comma = url.IndexOf(',');
        if (comma < 0) throw new InvalidDataException("图片数据无效。");
        var bytes = Convert.FromBase64String(url[(comma + 1)..]);
        if (bytes.Length > 50 * 1024 * 1024) throw new InvalidDataException("单张图片超过 50 MiB。");
        var name = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var media = Path.Combine(MediaRoot, name + ".png");
        BitmapFrame frame;
        using (var stream = new MemoryStream(bytes))
            frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        if ((long)frame.PixelWidth * frame.PixelHeight > 40_000_000)
            throw new InvalidDataException("图片像素数超过 4000 万。");
        if (!File.Exists(media))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            using var file = File.Create(media + ".tmp");
            encoder.Save(file);
            file.Dispose();
            File.Move(media + ".tmp", media, true);
        }
        var thumbnail = name + ".thumb.jpg";
        if (!File.Exists(Path.Combine(MediaRoot, thumbnail)))
        {
            var preview = new BitmapImage();
            preview.BeginInit();
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.UriSource = new Uri(media);
            preview.DecodePixelWidth = Math.Min(640, frame.PixelWidth);
            preview.EndInit();
            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(preview));
            using var file = File.Create(Path.Combine(MediaRoot, thumbnail));
            encoder.Save(file);
        }
        theme["imageUrl"] = "https://media.studio.local/" + name + ".png";
        theme["thumbnailUrl"] = "https://media.studio.local/" + thumbnail;
        theme["sourceWidth"] = frame.PixelWidth;
        theme["sourceHeight"] = frame.PixelHeight;
        theme["resolution"] = frame.PixelWidth + " × " + frame.PixelHeight;
    }
}
