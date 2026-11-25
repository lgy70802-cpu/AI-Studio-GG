using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    StudioPackageStore.CheckedPackage? inspectedPackage;
    string? inspectedToken;
    async Task<object> PackageCommand(string command, JsonElement message)
    {
        var packages = new StudioPackageStore(dataRoot, Path.Combine(AppContext.BaseDirectory, "Web"));
        if (command == "package-cancel")
        {
            if (inspectedPackage != null) Directory.Delete(inspectedPackage.Directory, true);
            inspectedPackage = null; inspectedToken = null; return true;
        }
        if (command == "package-export")
        {
            var themeId = message.TryGetProperty("themeId", out var item) ? item.GetString() : null;
            var path = message.TryGetProperty("path", out var given) ? given.GetString() : null;
            if (path == null)
            {
                var dialog = new SaveFileDialog { Filter = "MiraBox 包|*.mirabox", FileName = (themeId == null ? "MiraBox-备份-" : "MiraBox-工程-") + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".mirabox", AddExtension = true };
                if (dialog.ShowDialog(this) != true) return new { cancelled = true };
                path = dialog.FileName;
            }
            if (videoUploads.Count > 0) throw new InvalidOperationException("请等待视频导入完成后再备份。");
            Save(); SaveSchedules();
            var snapshot = librarySnapshot?.DeepClone().AsObject() ?? throw new InvalidOperationException("资源库尚未载入。");
            var preferences = message.TryGetProperty("preferences", out var prefs) ? JsonNode.Parse(prefs.GetRawText())!.AsObject() : new JsonObject();
            await Task.Run(() => packages.Export(path!, snapshot, preferences, themeId));
            return new { cancelled = false, path };
        }
        if (command == "package-inspect")
        {
            var path = message.TryGetProperty("path", out var given) ? given.GetString() : null;
            if (path == null)
            {
                var dialog = new OpenFileDialog { Filter = "MiraBox 包|*.mirabox" };
                if (dialog.ShowDialog(this) != true) return new { cancelled = true };
                path = dialog.FileName;
            }
            if (inspectedPackage != null) Directory.Delete(inspectedPackage.Directory, true);
            inspectedPackage = await Task.Run(() => packages.Inspect(path!));
            inspectedToken = Guid.NewGuid().ToString("N");
            return new { cancelled = false, token = inspectedToken, scope = inspectedPackage.Manifest["scope"]!.GetValue<string>(),
                themes = inspectedPackage.Snapshot["themes"]!.AsArray().Count, playlists = inspectedPackage.Snapshot["playlists"]!.AsArray().Count,
                schedules = inspectedPackage.Snapshot["schedules"]!.AsArray().Count,
                note = "合并导入保留原内容，冲突标识生成新副本。恢复备份会替换资源库与设备设置，并先暂停输出和定时。原素材不删除。" };
        }
        if (inspectedPackage == null || inspectedToken != message.GetProperty("token").GetString()) throw new InvalidOperationException("请重新选择并校验备份包。");
        var mode = message.GetProperty("mode").GetString();
        if (mode is not ("restore" or "merge")) throw new InvalidOperationException("请选择合并或恢复模式。");
        var replace = mode == "restore";
        foreach (var serial in temporaryThemes.Keys.ToArray()) await EndThemePreview(serial, true);
        var working = playlistProgress.Values.ToDictionary(p => p.Serial, p => p.Running);
        if (replace) foreach (var p in playlistProgress.Values) p.Running = false;
        try
        {
            foreach (var serial in replace ? saved.Keys.ToArray() : Array.Empty<string>())
            {
                InvalidatePlaylistWork(serial);
                await Stop(serial);
            }
            librarySnapshot = await Task.Run(() => packages.Import(inspectedPackage, replace));
            var preferencesFile = Path.Combine(inspectedPackage.Directory, "preferences.json");
            var preferences = replace && File.Exists(preferencesFile) ? JsonNode.Parse(File.ReadAllText(preferencesFile)) : new JsonObject();
            if (replace)
            {
                saved.Clear(); playlistProgress.Clear(); lastSavedState = null; savedScheduleText = null; scheduleState = new();
                Load(); LoadSchedules();
                foreach (var serial in saved.Keys.ToArray()) saved[serial] = saved[serial] with { Playing = false };
                foreach (var p in playlistProgress.Values) p.Running = false;
                scheduleState.Enabled = false;
            }
            else foreach (var p in playlistProgress.Values) p.Running = working.GetValueOrDefault(p.Serial);
            Save(); SaveSchedules(); librarySaveFailed = false;
            if (replace) foreach (var name in new[] { "desktop-state.json", "schedule-state.json" }) File.Copy(Path.Combine(dataRoot, name), Path.Combine(dataRoot, name + ".previous"), true);
            Directory.Delete(inspectedPackage.Directory, true); inspectedPackage = null; inspectedToken = null;
            return new { snapshot = librarySnapshot, preferences, paused = replace };
        }
        catch { foreach (var p in playlistProgress.Values) p.Running = working.GetValueOrDefault(p.Serial); throw; }
    }
}
