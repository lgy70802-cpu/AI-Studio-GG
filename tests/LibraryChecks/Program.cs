using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiraBox.App;

internal static class Program
{
    static int passed;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    [STAThread]
    static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "StudioLibraryChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var image = BitmapSource.Create(4, 2, 96, 96, PixelFormats.Bgr24, null,
            Enumerable.Repeat((byte)180, 24).ToArray(), 12);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        var original = Path.Combine(root, "source.png");
        File.WriteAllBytes(original, buffer.ToArray());
        var store = new StudioLibraryStore(Path.Combine(root, "Data"));
        Check(store.Load() == null, "first launch has no snapshot");
        var source = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(original));
        var snapshot = JsonNode.Parse("""{"themes":[{"id":"local-a","displayName":"Original","imageUrl":"","inLibrary":true,"isFavorite":false}],"playlists":[{"id":"list","items":[{"themeId":"local-a","durationSeconds":30}]}],"schedules":[{"targetThemeId":"local-a"}]}""")!.AsObject();
        snapshot["themes"]![0]!["imageUrl"] = source;
        var saved = store.Save(snapshot);
        var url = saved["themes"]![0]!["imageUrl"]!.GetValue<string>();
        var thumbnail = saved["themes"]![0]!["thumbnailUrl"]!.GetValue<string>();
        Check(url.StartsWith("https://media.studio.local/"), "base64 converted to managed media URL");
        File.Delete(original);
        Check(File.Exists(Path.Combine(store.MediaRoot, new Uri(url).AbsolutePath.TrimStart('/'))), "managed image survives original deletion");
        Check(File.Exists(Path.Combine(store.MediaRoot, new Uri(thumbnail).AbsolutePath.TrimStart('/'))), "thumbnail generated");
        Check(saved["themes"]![0]!["resolution"]!.GetValue<string>() == "4 × 2", "actual dimensions saved");
        var theme = saved["themes"]![0]!;
        theme["displayName"] = "Renamed"; theme["isFavorite"] = true; theme["deletedAt"] = 1234L; theme["inLibrary"] = false;
        store.Save(saved);
        var reloaded = new StudioLibraryStore(Path.Combine(root, "Data")).Load()!;
        Check(reloaded["themes"]![0]!["displayName"]!.GetValue<string>() == "Renamed" && reloaded["themes"]![0]!["isFavorite"]!.GetValue<bool>(), "rename and favorite survive restart");
        Check(reloaded["themes"]![0]!["deletedAt"]!.GetValue<long>() == 1234 &&
            reloaded["playlists"]![0]!["items"]![0]!["themeId"]!.GetValue<string>() == "local-a" &&
            reloaded["schedules"]![0]!["targetThemeId"]!.GetValue<string>() == "local-a", "trash preserves playlist and schedule references");
        reloaded["themes"]![0]!.AsObject().Remove("deletedAt");
        reloaded["themes"]![0]!["inLibrary"] = true;
        store.Save(reloaded);
        Check(store.Load()!["themes"]![0]!["inLibrary"]!.GetValue<bool>(), "restore retains stable ID");
        var bad = reloaded.DeepClone().AsObject();
        bad["themes"]![0]!["imageUrl"] = "data:image/png;base64,AAAA";
        try { store.Save(bad); throw new Exception("invalid image accepted"); }
        catch (NotSupportedException) { }
        catch (FileFormatException) { }
        Check(store.Load()!["themes"]![0]!["imageUrl"]!.GetValue<string>() == url, "failed import leaves prior index intact");
        var duplicate = reloaded.DeepClone().AsObject();
        duplicate["themes"]!.AsArray().Add(duplicate["themes"]![0]!.DeepClone());
        try { store.Save(duplicate); throw new Exception("duplicate ID accepted"); }
        catch (InvalidDataException) { }
        Check(store.Load()!["themes"]!.AsArray().Count == 1, "duplicate ID rejected without replacing index");
        File.WriteAllText(Path.Combine(root, "Data", "library-workspace.json"), "{broken");
        var recovered = store.Load()!;
        Check(recovered["themes"]!.AsArray().Count == 1 && Directory.GetFiles(Path.Combine(root, "Data"), "library-workspace.json.corrupt-*").Any(file => File.ReadAllText(file) == "{broken"), "corrupt index preserved separately and previous valid snapshot restored");
        Console.WriteLine(passed + " checks passed; " + root);
    }
}

