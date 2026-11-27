using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using MiraBox.App;

internal static class Program
{
    static int count;
    static void Check(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS " + description); count++; }
    [STAThread] static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "ThemeHostChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new StudioLibraryStore(root);
        var theme = JsonNode.Parse("""{"id":"theme","displayName":"fixture","category":"动态时钟","inLibrary":true,"editorProject":{"bg":{"kind":"image","url":""},"master":[{"id":"image","kind":"image","src":"","rot":17,"alpha":70},{"id":"clock","kind":"clock","style":"flip","tz":"JP"}],"layouts":[{"size":[1280,480],"follow":false},{"size":[480,1280],"follow":true,"vis":{"image":true}}]}}""")!;
        var data = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII=";
        theme["editorProject"]!["bg"]!["url"] = data;
        theme["editorProject"]!["master"]![0]!["src"] = data;
        var state = new JsonObject { ["themes"] = new JsonArray(theme), ["playlists"] = new JsonArray(), ["schedules"] = new JsonArray() };
        store.Save(state);
        var managed = theme["editorProject"]!["bg"]!["url"]!.GetValue<string>();
        Check(managed.StartsWith("https://media.studio.local/"), "nested background image managed persistently");
        Check(theme["editorProject"]!["master"]![0]!["src"]!.GetValue<string>() == managed, "image layers share deduplicated managed asset");
        Check(File.Exists(Path.Combine(store.MediaRoot, new Uri(managed).Segments[^1])), "managed original exists on disk");
        var roundtrip = store.Load()!["themes"]![0]!;
        Check(roundtrip["editorProject"]!["layouts"]![1]!["vis"]!["image"]!.GetValue<bool>(), "following layout visibility survives roundtrip");
        Check(roundtrip["editorProject"]!["master"]![0]!["rot"]!.GetValue<int>() == 17 && roundtrip["editorProject"]!["master"]![1]!["tz"]!.GetValue<string>() == "JP", "rotation and timezone survive roundtrip");
        Environment.SetEnvironmentVariable("MIRABOX_DATA_DIR", root);
        var window = new StudioWindow();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(StudioWindow).GetField("librarySnapshot", flags)!.SetValue(window, state);
        var issue = typeof(StudioWindow).GetMethod("ThemeIssue", flags)!;
        var available = typeof(StudioWindow).GetMethod("PlaylistImagePath", flags)!;
        Check(issue.Invoke(window, [theme]) == null, "valid theme accepted for native output");
        Check(available.Invoke(window, ["theme", false]) != null, "theme without top-level image joins playlists");
        theme["editorProject"]!["master"]![0]!["src"] = "https://media.studio.local/missing.png";
        Check(issue.Invoke(window, [theme]) != null && available.Invoke(window, ["theme", false]) == null, "missing nested asset excludes theme from playlists");
        theme["editorProject"]!["master"]![0]!["src"] = managed;
        theme["deletedAt"] = 1;
        Check(available.Invoke(window, ["theme", false]) == null, "trashed theme excluded while its stable ID remains");
        theme.AsObject().Remove("deletedAt");
        Check(available.Invoke(window, ["theme", false]) != null, "restored theme becomes eligible again");
        theme["editorProject"]!["master"]![0]!["kind"] = "plugin";
        Check(issue.Invoke(window, [theme]) != null, "unconnected plugin data cannot masquerade as live output");
        theme["editorProject"]!["master"]![0]!["kind"] = "image";
        var previous = File.ReadAllText(Path.Combine(root, "library-workspace.json"));
        theme["editorProject"]!["master"]![0]!["src"] = "blob:expired";
        try { store.Save(state); throw new Exception("blob accepted"); } catch (InvalidDataException) { }
        Check(File.ReadAllText(Path.Combine(root, "library-workspace.json")) == previous, "failed temporary asset save preserves prior index");

        var savedDevices = (Dictionary<string, StudioWindow.SavedDevice>)typeof(StudioWindow).GetField("saved", flags)!.GetValue(window)!;
        var save = typeof(StudioWindow).GetMethod("Save", flags)!;
        savedDevices["orientation-test"] = new("orientation-test", "D92", ThemeId: "landscape-first");
        save.Invoke(window, null);
        savedDevices["orientation-test"] = savedDevices["orientation-test"] with { Portrait = true, ThemeId = "portrait-first" };
        save.Invoke(window, null);
        savedDevices["orientation-test"] = savedDevices["orientation-test"] with { Portrait = false, ThemeId = "landscape-latest" };
        save.Invoke(window, null);
        var stored = System.Text.Json.JsonSerializer.Deserialize<StudioWindow.Settings>(File.ReadAllText(Path.Combine(root, "desktop-state.json")))!;
        Check(stored.OrientationOutputs!.Single(o => !o.Portrait).ThemeId == "landscape-latest" &&
            stored.OrientationOutputs!.Single(o => o.Portrait).ThemeId == "portrait-first", "last output stored independently for both orientations");
        Check(stored.OrientationHistory!.Where(h => !h.Portrait).Select(h => h.ThemeId).SequenceEqual(new[] { "landscape-latest", "landscape-first" }) &&
            stored.OrientationHistory!.Where(h => h.Portrait).Single().ThemeId == "portrait-first", "recent histories remain separated and newest first");
        save.Invoke(window, null);
        var unchanged = System.Text.Json.JsonSerializer.Deserialize<StudioWindow.Settings>(File.ReadAllText(Path.Combine(root, "desktop-state.json")))!;
        Check(unchanged.OrientationHistory!.Length == stored.OrientationHistory!.Length &&
            unchanged.OrientationHistory[0].UsedAt == stored.OrientationHistory![0].UsedAt, "background saves do not duplicate or reorder recent history");
        savedDevices.Clear();
        typeof(StudioWindow).GetMethod("Load", flags)!.Invoke(window, null);
        Check(savedDevices["orientation-test"].ThemeId == "landscape-latest", "orientation state loads after restart");
        var oldState = new StudioWindow.Settings(null, true, [new("legacy-test", "D69", ThemeId: "legacy")]);
        File.WriteAllText(Path.Combine(root, "desktop-state.json"), System.Text.Json.JsonSerializer.Serialize(oldState));
        savedDevices.Clear();
        typeof(StudioWindow).GetMethod("Load", flags)!.Invoke(window, null);
        save.Invoke(window, null);
        var migrated = System.Text.Json.JsonSerializer.Deserialize<StudioWindow.Settings>(File.ReadAllText(Path.Combine(root, "desktop-state.json")))!;
        Check(migrated.OrientationOutputs!.Single().ThemeId == "legacy", "legacy device state seeds its current orientation");

        Console.WriteLine(count + " checks passed"); Console.Out.Flush(); Environment.Exit(0);
    }
}
