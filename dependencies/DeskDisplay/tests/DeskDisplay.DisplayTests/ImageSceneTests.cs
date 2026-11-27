using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Rendering;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class ImageSceneTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskDisplay-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "wide.png");
            CreateImage(path, 6000, 2000, false);
            var loader = new ImageSceneLoader();
            var image = await loader.LoadAsync(path, ImageDisplayMode.Fill);
            var decoded = (WpfSceneImage)image.Image;
            Check(decoded.Bitmap.IsFrozen && decoded.Bitmap.PixelWidth <= ImageSceneLoader.MaximumEdge
                && (long)decoded.Bitmap.PixelWidth * decoded.Bitmap.PixelHeight <= ImageSceneLoader.MaximumPixels,
                "Large image is frozen and decoded within edge/pixel limits");
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(exclusive.Length > 0, "Decoded image does not lock its source file");
            File.Delete(path);
            var pixel = new byte[4];
            decoded.Bitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            Check(pixel[2] > 200, "Decoded image remains drawable after source deletion");
            try { await loader.LoadAsync(path, ImageDisplayMode.Fill); throw new Exception("Expected missing file"); }
            catch (FileNotFoundException) { Check(true, "Deleted source is detected on reload"); }

            path = Path.Combine(folder, "small.jpg");
            CreateImage(path, 120, 60, true);
            image = await loader.LoadAsync(path, ImageDisplayMode.Uniform);
            Check(image.Image.OriginalWidth == 120 && image.Image.OriginalHeight == 60, "JPG dimensions are read correctly");
            var frame = new Rect(0, 0, 400, 400);
            foreach (var mode in Enum.GetValues<ImageDisplayMode>())
            {
                var scene = new ImageScene(path, image.Image, mode);
                scene.SetCanvasSize(400, 400);
                var destination = SceneView.ImageDestination(scene, frame);
                var expected = mode switch
                {
                    ImageDisplayMode.Fill => new Rect(-200, 0, 800, 400),
                    ImageDisplayMode.Uniform => new Rect(0, 100, 400, 200),
                    ImageDisplayMode.Stretch => frame,
                    _ => new Rect(140, 170, 120, 60)
                };
                Check(destination == expected, mode + " has correct image geometry");
                Check(SceneView.ImageDestination(scene, new Rect(0, 0, 100, 100)).Width == destination.Width / 4,
                    mode + " preview scales identically to external canvas");
                var view = new SceneView { Scene = scene };
                view.Measure(new Size(400, 400));
                view.Arrange(frame);
                var bitmap = new RenderTargetBitmap(400, 400, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var corner = new byte[4];
                bitmap.CopyPixels(new Int32Rect(1, 1, 1, 1), corner, 4, 0);
                Check(mode is ImageDisplayMode.Fill or ImageDisplayMode.Stretch ? corner[2] > 200 : corner[2] == 0x0C,
                    mode + " actual renderer correctly covers or letterboxes corners");
            }

            var settings = new JsonSettingsService(logger, Path.Combine(folder, "settings.json"));
            await settings.LoadAsync();
            var target = new DisplayInfo("external", "Test display", "stable", -400, 0, 400, 400, false);
            var targets = new TargetDisplayService(new AppSettings(), settings, logger);
            targets.UpdateDisplays([target]);
            targets.SetTargetDisplay(target);
            await Task.WhenAll(targets.SaveAsync(), settings.UpdateAsync(value => { value.LastImagePath = path; value.ImageMode = ImageDisplayMode.Center; }));
            var saved = await settings.LoadAsync();
            Check(saved.TargetDisplay?.DeviceId == "stable" && saved.LastImagePath == path && saved.ImageMode == ImageDisplayMode.Center,
                "Concurrent target/image updates preserve both settings");

            using var manager = new FakeManager { Displays = [target] };
            var clock = new ClockScene();
            var surface = new FakeSurface();
            using var output = new RenderWindowService(logger, clock, () => surface);
            using var devices = new DevicesViewModel(manager, targets, logger, output);
            using var main = new MainViewModel(devices, clock);
            using var editor = new ImageSceneViewModel(devices, settings, saved, clock, main.PreviewScene, logger);
            await devices.RefreshAsync();
            await editor.RestoreAsync();
            Check(main.Scene is ImageScene { Mode: ImageDisplayMode.Center } && !output.IsOpen,
                "Startup restores last image and mode to preview without opening external output");
            main.SetImageEditor(editor);
            var window = new DeskDisplay.App.Views.MainWindow { DataContext = main };
            try
            {
                window.Show();
                window.UpdateLayout();
                await Task.Delay(100);
                var screenshot = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                screenshot.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(screenshot));
                using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "image-scene-home.png"));
                encoder.Save(file);
                Check(window.IsVisible, "Image editor home window loads with production bindings");
            }
            finally { window.Close(); }
            await editor.ApplyAsync();
            Check(output.IsOpen && ReferenceEquals(surface.Scene, main.Scene), "Apply shares exact same ImageScene with preview and output");
            var live = surface.Scene;
            editor.Mode = ImageDisplayMode.Stretch;
            Check(!ReferenceEquals(main.Scene, live) && ReferenceEquals(surface.Scene, live), "Editing preview waits for explicit apply");
            await editor.ApplyAsync();
            Check(ReferenceEquals(surface.Scene, main.Scene), "Reapply updates existing output to the same preview model");
            File.Delete(path);
            live = surface.Scene;
            await editor.ApplyAsync();
            Check(editor.Status.Contains("已被删除") && ReferenceEquals(live, surface.Scene), "Deleted image reports friendly error and preserves existing output");
            var bad = Path.Combine(folder, "broken.png");
            await File.WriteAllTextAsync(bad, "not an image");
            await editor.LoadAsync(bad);
            Check(editor.Status.Contains("无法读取") && editor.CanEdit, "Corrupt image reports error and permits another selection");
            manager.Displays = [];
            await devices.RefreshAsync();
            Check(!editor.ApplyCommand.CanExecute(null) && !output.IsOpen, "Unplug disables image apply and closes output");
            using var restored = new ImageSceneViewModel(devices, settings, saved, clock, main.PreviewScene, logger);
            await restored.RestoreAsync();
            Check(restored.Status.Contains("已被删除"), "Missing remembered image cannot prevent startup");
            await editor.PrepareForCloseAsync();
            Check(!editor.CanEdit, "Shutdown stops new image operations");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("All image scene tests passed.");
    }

    private static void CreateImage(string path, int width, int height, bool jpeg)
    {
        var pixels = new byte[width * height * 3];
        for (var offset = 0; offset < pixels.Length; offset += 3) pixels[offset + 2] = 240;
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, width * 3);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder() : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        encoder.Save(file);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
    private sealed class FakeManager : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Displays { get; set; } = [];
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(Displays);
        public void Dispose() { }
    }
    private sealed class FakeSurface : IRenderSurface
    {
        public event EventHandler? Closed { add { } remove { } }
        public event Action<Exception>? Faulted { add { } remove { } }
        public event EventHandler? TopologyInvalidated { add { } remove { } }
        public Scene? Scene { get; private set; }
        public void ShowOnDisplay(DisplayInfo target) { }
        public void Render(Scene scene) => Scene = scene;
        public void Dispose() => Scene = null;
    }
}
