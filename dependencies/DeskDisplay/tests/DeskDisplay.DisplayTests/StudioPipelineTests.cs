using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class StudioPipelineTests
{
    private sealed record Descriptor(string ServerUrl, string ResourceId, string ExpectedSha256, string OutputDirectory);
    public static async Task RunAsync(ILogger logger)
    {
        var path = Environment.GetEnvironmentVariable("DESKDISPLAY_PIPELINE_DESCRIPTOR") ?? throw new InvalidOperationException("Explicit isolated pipeline descriptor required");
        var plan = JsonSerializer.Deserialize<Descriptor>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (!Uri.TryCreate(plan.ServerUrl, UriKind.Absolute, out var origin) || !origin.IsLoopback || !Guid.TryParse(plan.ResourceId, out _)) throw new InvalidDataException("Pipeline requires an isolated loopback API");
        var root = Path.Combine(plan.OutputDirectory, "windows"); Directory.CreateDirectory(root);
        using var api = new StudioApiClient(logger, cacheDirectory: Path.Combine(root, "cache"));
        var page = await api.GetResourcesAsync(plan.ServerUrl, sort: "newest"); var item = page.Items.Single(i => i.Id == plan.ResourceId);
        Check(item.Sha256 == plan.ExpectedSha256 && item.HasDownloadEntitlement && item.Type == "STATIC_WALLPAPER", "Real Nest/PostgreSQL catalog returns the exact uploaded free resource");
        var download = await api.DownloadAsync(plan.ServerUrl, item); var bytes = await File.ReadAllBytesAsync(download);
        Check(Convert.ToHexString(SHA256.HashData(bytes)).Equals(plan.ExpectedSha256, StringComparison.OrdinalIgnoreCase), "Real HTTP download matches uploaded PNG SHA-256");
        await File.WriteAllBytesAsync(download, [0]); download = await api.DownloadAsync(plan.ServerUrl, item);
        Check(bytes.SequenceEqual(await File.ReadAllBytesAsync(download)), "Corrupted local cache is replaced by a newly verified server download");
        using var displays = new Displays(); using var devices = new DevicesViewModel(displays, new TargetDisplayService(new(), new Settings(), logger), logger); using var main = new MainViewModel(devices, new ClockScene()); using var output = new WorkspaceTests.Output();
        main.SetImageEditor(new ImageSceneViewModel(devices, new Settings(), new(), new ClockScene(), main.PreviewScene, logger)); main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger)); main.SetWorkspace(new WorkspaceViewModel(main, new ClockScene(), new PerformanceScene(), output, new Settings(), new WorkspaceTests.Sensors(), new(), logger, false)); await main.Workspace!.RestoreAsync();
        var library = new LibraryViewModel(main, new WallpaperCatalog(logger, Path.Combine(root, "library")), logger); main.SetLibrary(library); await library.InitializeAsync();
        await library.ImportCloudAsync(download, item.Name, item.Id, item.Version, "隔离链路验收", "真实HTTP", false);
        var asset = library.AllItems.Single().Asset; Check(asset.CloudResourceId == item.Id && asset.CloudVersion == item.Version, "Production local library retains cloud identity and downloaded version");
        var store = new ThemeProjectStore(Path.Combine(root, "themes")); var editor = new CreatorViewModel(main, logger, store); main.SetCreator(editor); main.SelectedPage = AppPage.Creator; await editor.InitializeAsync();
        var project = new ThemeProject { Name = "隔离验收 · Web 内容组合主题" }; project.Landscape.Width = item.Width; project.Landscape.Height = item.Height;
        await editor.OpenProjectAsync(project); editor.AddAsset(asset); await editor.PendingOperation;
        editor.Add(ThemeElementKind.DigitalClock); await editor.PendingOperation; editor.X = 80; editor.Y = 270; editor.Width = 700; editor.Height = 120; editor.FontSize = 90; editor.ShowSeconds = true;
        editor.Add(ThemeElementKind.Text); await editor.PendingOperation; editor.Text = "WEB → WINDOWS → EDITABLE THEME"; editor.FontSize = 48; editor.X = 80; editor.Y = 40; editor.Width = 1500; editor.Height = 90;
        await editor.SaveAsync(); var source = (await store.ListAsync()).Single(); Check(source.ActiveLayout.Layers.Count == 3 && source.ActiveLayout.Layers.Any(l => l.CloudResourceId == item.Id) && source.ActiveLayout.Layers.Any(l => l.Kind == ThemeElementKind.DigitalClock && l.ShowSeconds), "Downloaded resource becomes a saved editable theme with real clock component");
        using var renderer = new SceneJpegRenderer(); var first = renderer.Render(editor.Scene, 0); var before = editor.Scene.CurrentTime; await Task.Delay(1100); var second = renderer.Render(editor.Scene, 0);
        Check(editor.Scene.CurrentTime > before && !first.SequenceEqual(second), "Real output renderer changes JPEG frames as the clock advances");
        await File.WriteAllBytesAsync(Path.Combine(root, "preview.jpg"), second); await File.WriteAllBytesAsync(Path.Combine(root, "usb-ready-90.jpg"), renderer.Render(editor.Scene, 90));
        var sourcePath = Path.Combine(root, "pipeline.theme.json"); await File.WriteAllTextAsync(sourcePath, JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true }));
        var bundle = Path.Combine(root, "references-only.d92theme"); var exported = await new ThemeTransferService(store).ExportAsync(source, bundle, true, library.AllItems.ToDictionary(i => i.Asset.Id, i => i.Asset));
        Check(exported.Included == 0 && exported.Omitted == 1, "Export does not redistribute downloaded cloud content as a local asset");
        await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { success = true, resourceId = item.Id, sha256 = item.Sha256, sourcePath, originalWidth = item.Width, originalHeight = item.Height, outputFramesDiffer = true, hardwareSent = false, boundary = "Real management REST, PostgreSQL, client, editable source and JPEG renderer; physical HID pending explicit device run." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("WINDOWS_PIPELINE_OK");
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private sealed class Settings : ISettingsService { public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings()); public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true); }
    private sealed class Displays : IDisplayManager { public event EventHandler? DisplaysChanged { add { } remove { } } public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]); public void Dispose() { } }
}
