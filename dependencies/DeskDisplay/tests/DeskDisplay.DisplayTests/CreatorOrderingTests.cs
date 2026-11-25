using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;
internal static class CreatorOrderingTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-ordering-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output(); var initial = new AppSettings();
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "library")); main.SetLibrary(new(main, catalog, logger)); await main.Library!.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); await creator.InitializeAsync(); await creator.OpenProjectAsync(new ThemeProject());
            var width = creator.Project.ActiveLayout.Width; var height = creator.Project.ActiveLayout.Height;
            string Image(string name, byte red, byte green)
            {
                var path = Path.Combine(root, name + ".png"); var bytes = Enumerable.Range(0, 80 * 20).SelectMany(_ => new byte[] { 20, green, red, 255 }).ToArray();
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(80, 20, 96, 96, PixelFormats.Bgra32, null, bytes, 320))); using var stream = File.Create(path); encoder.Save(stream); return path;
            }
            var a = Image("first", 240, 40); var b = Image("second", 40, 240); var c = Image("third", 100, 180);
            await main.Library.ImportAsync([a]); var first = creator.FilteredAssets.Single(); var revision = creator.MaterialImportRevision;
            await main.Library.ImportAsync([b, c]);
            Check(creator.FilteredAssets.Select(i => i.Name).SequenceEqual(new[] { "third", "second", "first" }), "Newly imported materials appear first, including batch imports");
            Check(creator.MaterialImportRevision > revision, "Material imports notify the editor to reveal the newest item without reopening the page");
            creator.MaterialSearch = "second"; Check(creator.FilteredAssets.Single().Name == "second", "Newest-first material ordering retains the existing search filter"); creator.MaterialSearch = "";
            var tied = DateTimeOffset.UtcNow; foreach (var item in main.Library.AllItems) item.Asset.AddedAt = tied;
            creator.MaterialSearch = "i"; creator.MaterialSearch = "";
            Check(creator.FilteredAssets.Select(i => i.Name).SequenceEqual(new[] { "third", "second", "first" }), "Equal import timestamps retain newest insertion order rather than random GUID order");
            creator.Add(ThemeElementKind.DigitalClock); await creator.PendingOperation; var clockLayer = creator.SelectedLayer!;
            creator.MaterialTab = "壁纸"; creator.AddAsset(first.Asset); await creator.PendingOperation; var wallpaper = creator.SelectedLayer!;
            Check(creator.Layers[0].Id == wallpaper.Id && creator.LayerRows[0].Layer.Id == wallpaper.Id && wallpaper.ZIndex > clockLayer.ZIndex, "Adding wallpaper from materials creates the top selected layer instead of silently sending it behind existing content");
            creator.AddAsset(main.Library.AllItems.Single(i => i.Name == "second").Asset); await creator.PendingOperation; var second = creator.SelectedLayer!;
            Check(creator.LayerRows[0].Layer.Id == second.Id && creator.SelectedLayerRow?.Layer.Id == second.Id, "The latest added image is first in the layer panel and selected immediately");
            using var renderer = new SceneJpegRenderer(); var jpeg = renderer.Render(creator.Scene, 0); using var stream = new MemoryStream(jpeg);
            var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; var rgba = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0); var pixel = new byte[4]; rgba.CopyPixels(new System.Windows.Int32Rect(960, 240, 1, 1), pixel, 4, 0);
            Check(pixel[1] > 180 && pixel[2] < 100, "Layer panel order agrees with the real JPEG compositor's frontmost image");
            creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.Layers[0].Id == wallpaper.Id, "Undo restores the previous top layer");
            creator.RedoCommand.Execute(null); await creator.PendingOperation; Check(creator.Layers[0].Id == second.Id, "Redo restores the newly added layer at the top");
            creator.SelectedLayer = creator.Layers[0]; creator.CopyToOtherLayout(); Check(creator.Project.Portrait.Layers.Single().Kind == ThemeElementKind.Wallpaper, "Cross-layout image copy remains a distinct editable layer");
            creator.Add(ThemeElementKind.Rectangle); await creator.PendingOperation; Check(creator.Layers[0].Kind == ThemeElementKind.Rectangle, "New shape layers follow the same top insertion rule");
            await creator.InsertImageFilesAsync([c], 10, 10); Check(creator.Layers[0].Kind == ThemeElementKind.Image && creator.Layers[0].Name == "third", "File-inserted image layers also appear first");
            await creator.SaveAsync(); var restored = await store.ReadSavedAsync(creator.Project.Id);
            Check(restored.Landscape.Layers.OrderByDescending(l => l.ZIndex).First().Id == creator.Layers[0].Id, "Newest layer ordering survives project save and reopen");
            Check(creator.Project.ActiveLayout.Width == width && creator.Project.ActiveLayout.Height == height, "Ordering changes preserve the existing canvas resolution");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool result, string name) { if (!result) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
}
