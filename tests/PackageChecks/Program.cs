using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using MiraBox.App;

internal static class Program
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); passed++; }
    static void Reject(Action action, string message) { try { action(); throw new Exception("unexpected success: " + message); } catch (InvalidDataException) { } Check(true, message); }
    [STAThread] static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "MiraBoxPackages-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "Source"); var target = Path.Combine(root, "Target"); var web = Path.Combine(root, "Web");
        Directory.CreateDirectory(Path.Combine(web, "src", "assets")); Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        var pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII=");
        File.WriteAllBytes(Path.Combine(web, "src", "assets", "builtin.png"), pixel);
        var store = new StudioLibraryStore(source); File.WriteAllBytes(Path.Combine(store.MediaRoot, "image.png"), pixel);
        using (var video = File.Create(Path.Combine(store.MediaRoot, "large.mp4"))) video.SetLength(64L * 1024 * 1024);
        var state = JsonNode.Parse("""{"themes":[{"id":"a","displayName":"Portable","category":"主题","inLibrary":true,"imageUrl":"/src/assets/builtin.png","editorProject":{"bg":{"kind":"video","vurl":"https://media.studio.local/large.mp4"},"master":[{"kind":"image","src":"https://media.studio.local/image.png"}],"layouts":[{"size":[1280,480],"follow":true}]}}],"playlists":[{"id":"list","deviceId":"QA","orientation":"landscape","items":[{"themeId":"a","durationSeconds":30}]}],"schedules":[{"id":"rule","themeId":"a","playlistId":"list"}]}""")!.AsObject();
        store.Save(state); File.WriteAllBytes(Path.Combine(source, "original.image"), pixel); File.WriteAllText(Path.Combine(source, "original.json"), state["themes"]![0]!.ToJsonString());
        StudioAtomicFile.Write(Path.Combine(source, "desktop-state.json"), """{"Selected":"QA","Restore":true,"Devices":[{"Serial":"QA","Model":"D69","Playing":true,"ImageFile":"original.image","ThemeFile":"original.json"}],"PlaylistSessions":[{"Serial":"QA","PlaylistId":"list","Running":true}]}""");
        StudioAtomicFile.Write(Path.Combine(source, "schedule-state.json"), """{"Enabled":true,"History":[],"Completed":[]}""");
        var path = Path.Combine(root, "complete.mirabox"); var exporter = new StudioPackageStore(source, web);
        File.WriteAllBytes(Path.Combine(store.MediaRoot, "preset-only.png"), pixel.Concat(new byte[] { 0 }).ToArray());
        var presets = new JsonArray(new JsonObject { ["id"] = "preset", ["name"] = "preset only", ["elements"] = new JsonArray(new JsonObject { ["kind"] = "image", ["src"] = "https://media.studio.local/preset-only.png" }) });
        var progress = new List<(int Done, int Total)>();
        exporter.Export(path, state, new JsonObject { ["studio-test"] = "kept", ["d92-te-custom-assets-v1"] = presets.ToJsonString() }, progress: (done, total) => progress.Add((done, total))); Check(File.Exists(path), "full backup including a 64 MiB media source exported by streaming");
        var projectPath = Path.Combine(root, "project.mirabox"); exporter.Export(projectPath, state, new JsonObject(), "a");
        Directory.Move(source, Path.Combine(root, "SourceOffline")); Directory.Move(web, Path.Combine(root, "WebOffline"));
        var importer = new StudioPackageStore(target, Path.Combine(root, "AbsentWeb")); var inspected = importer.Inspect(path);
        Check(inspected.Snapshot["themes"]![0]!["imageUrl"]!.GetValue<string>().StartsWith("https://media.studio.local/"), "bundled relative resources rewritten to portable managed assets");
        Check(progress.Count > 0 && progress[^1].Done == progress[^1].Total, "export reports completed file progress");
        var preferences = JsonNode.Parse(File.ReadAllText(Path.Combine(inspected.Directory, "preferences.json")))!;
        var importedPresets = JsonNode.Parse(preferences["d92-te-custom-assets-v1"]!.GetValue<string>())!;
        var presetUrl = importedPresets[0]!["elements"]![0]!["src"]!.GetValue<string>();
        Check(!presetUrl.EndsWith("preset-only.png"), "custom preset media is rebased to packaged dependency");
        var restored = importer.Import(inspected, true); var targetStore = new StudioLibraryStore(target);
        var image = restored["themes"]![0]!["editorProject"]!["master"]![0]!["src"]!.GetValue<string>();
        Check(File.Exists(Path.Combine(targetStore.MediaRoot, new Uri(image).Segments[^1])), "nested image survives original directory becoming unavailable");
        var url = restored["themes"]![0]!["editorProject"]!["bg"]!["vurl"]!.GetValue<string>();
        Check(new FileInfo(Path.Combine(targetStore.MediaRoot, new Uri(url).Segments[^1])).Length == 64L * 1024 * 1024, "video dependency restored completely in a different workspace");
        Check(File.Exists(Path.Combine(targetStore.MediaRoot, new Uri(presetUrl).Segments[^1])), "preset-only dependency survives migration without original directory");
        var desktop = JsonNode.Parse(File.ReadAllText(Path.Combine(target, "desktop-state.json")))!;
        var outputFile = desktop["Devices"]![0]!["ThemeFile"]!.GetValue<string>();
        Check(File.Exists(Path.Combine(target, outputFile)) && !File.ReadAllText(Path.Combine(target, outputFile)).Contains("/src/assets/"), "immutable output snapshots and their dependencies are portable");
        Check(!desktop["Devices"]![0]!["Playing"]!.GetValue<bool>() && !desktop["PlaylistSessions"]![0]!["Running"]!.GetValue<bool>() &&
            !JsonNode.Parse(File.ReadAllText(Path.Combine(target, "schedule-state.json")))!["Enabled"]!.GetValue<bool>() &&
            File.ReadAllText(Path.Combine(target, "desktop-state.json.previous")) == desktop.ToJsonString() &&
            File.ReadAllText(Path.Combine(target, "schedule-state.json.previous")) == File.ReadAllText(Path.Combine(target, "schedule-state.json")),
            "restore commits paused primary and recovery snapshots before any host reload");
        var merged = importer.Import(inspected, false);
        Check(merged["themes"]!.AsArray().Count == 2 && merged["themes"]![1]!["id"]!.GetValue<string>() != "a", "merge preserves original and creates a distinct ID on collision");
        var importedId = merged["themes"]![1]!["id"]!.GetValue<string>();
        Check(merged["playlists"]![1]!["items"]![0]!["themeId"]!.GetValue<string>() == importedId && merged["schedules"]![1]!["playlistId"]!.GetValue<string>() == merged["playlists"]![1]!["id"]!.GetValue<string>(), "merge remaps playlist and schedule references together");
        var project = importer.Inspect(projectPath); Check(project.Manifest["scope"]!.GetValue<string>() == "theme" && project.Snapshot["themes"]!.AsArray().Count == 1 && project.Snapshot["playlists"]!.AsArray().Count == 0, "single project package contains only its content and dependencies");
        Reject(() => importer.Import(project, true), "single project package cannot overwrite an entire workspace");
        var corrupt = Path.Combine(root, "corrupt.mirabox"); File.Copy(path, corrupt);
        using (var archive = ZipFile.Open(corrupt, ZipArchiveMode.Update)) { archive.GetEntry("workspace.json")!.Delete(); using var writer = new StreamWriter(archive.CreateEntry("workspace.json").Open()); writer.Write("{}"); }
        Reject(() => importer.Inspect(corrupt), "modified package payload rejected by checksum");
        var extra = Path.Combine(root, "extra.mirabox"); File.Copy(path, extra);
        using (var archive = ZipFile.Open(extra, ZipArchiveMode.Update)) { using var writer = new StreamWriter(archive.CreateEntry("unlisted.txt").Open()); writer.Write("extra"); }
        Reject(() => importer.Inspect(extra), "unlisted package entry rejected");
        Reject(() => StudioPackageStore.SafePath(target, "../outside"), "path traversal rejected");
        var index = Path.Combine(target, "library-workspace.json"); var before = File.ReadAllText(index);
        var prior = File.ReadAllText(index + ".previous");
        StudioAtomicFile.Write(Path.Combine(target, "restore.pending.json"), new JsonObject { ["library-workspace.json"] = before, ["library-workspace.json.previous"] = prior }.ToJsonString());
        File.WriteAllText(index, "{interrupted"); File.WriteAllText(index + ".previous", "{interrupted-backup"); StudioPackageStore.RecoverRestore(target);
        Check(File.ReadAllText(index) == before, "interrupted restore journal rolls back original index");
        Check(File.ReadAllText(index + ".previous") == prior, "interrupted restore rolls back the previous snapshot with its primary");
        targetStore.Save(merged); File.WriteAllText(index, "{}"); Check(targetStore.Load()!["themes"]!.AsArray().Count == 2, "semantic index damage recovers previous valid snapshot");
        Check(Directory.GetFiles(target, "library-workspace.json.corrupt-*").Length > 0, "damaged source preserved for diagnosis");
        Console.WriteLine(passed + " checks passed; " + root);
    }
}
