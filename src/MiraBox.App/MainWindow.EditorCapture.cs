using System.IO;
using System.Text.Json;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly HashSet<string> importedEditorCaptures = [];

    void ImportEditorCaptures(JsonElement message)
    {
        if (!message.TryGetProperty("captures", out var captures)) return;
        foreach (var capture in captures.EnumerateArray())
        {
            var key = JsText(capture, "name") + ":" + JsNumber(capture, "added");
            if (importedEditorCaptures.Contains(key)) continue;
            // Restored library entries are already durable and have no data/blob URLs.
            var media = JsText(capture, "videoUrl", JsText(capture, "url"));
            if (!media.StartsWith("data:", StringComparison.Ordinal)) { importedEditorCaptures.Add(key); continue; }
            var durable = RetainThemeAssets(capture, "Captures");
            var video = JsText(capture, "type") == "视频";
            var path = ThemeAssetPath(JsText(durable, video ? "videoUrl" : "url"));
            if (video && path == null)
            {
                var comma = media.IndexOf(',');
                path = Path.Combine(DataRoot, "Library", "Themes", "Captures", "录屏-" + Guid.NewGuid().ToString("N") + ".webm");
                File.WriteAllBytes(path, Convert.FromBase64String(media[(comma + 1)..]));
            }
            if (path == null) continue;
            if (new FileInfo(path).Length == 0) throw new InvalidDataException("采集结果为空，请重新录制。");
            var size = capture.GetProperty("sizes")[0];
            var dimensions = new MiraBox.Core.Size(size[0].GetInt32(), size[1].GetInt32());
            LibraryEntry? entry;
            if (video)
            {
                var poster = ThemeAssetPath(JsText(durable, "url"));
                entry = library.ImportVideo(path, new(dimensions, TimeSpan.FromSeconds(JsNumber(capture, "dur")), File.ReadAllBytes(poster!)));
            }
            else entry = library.Import(path, dimensions);
            if (entry != null)
                library.Update(entry.Resource.Id, current => current with
                { Resource = current.Resource with { Name = JsText(capture, "name") }, CaptureKind = JsText(capture, "cap") });
            importedEditorCaptures.Add(key);
            SyncLibrary();
            SaveWorkspace();
        }
    }
}
