using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Output;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;
internal static class BackgroundAdaptationTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-background-adapt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "three-to-one.png");
            var pixels = Enumerable.Range(0, 300 * 100).SelectMany(_ => new byte[] { 32, 160, 240, 255 }).ToArray();
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(300, 100, 96, 96, PixelFormats.Bgra32, null, pixels, 1200))); using (var file = File.Create(path)) png.Save(file);
            var source = new ThemeProject { Name = "背景适配离线验证" };
            source.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = path, Width = 1920, Height = 480, Fill = ImageDisplayMode.Uniform, ZIndex = -1 });
            source.Landscape.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, X = 700, Y = 120, Width = 520, Height = 140, FontSize = 100, ShowSeconds = true });
            source.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Wallpaper, Path = path, Width = 480, Height = 1920, ZIndex = -1 });
            source.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Text, Text = "独立竖屏", X = 20, Y = 600, Width = 400, Height = 100 });
            var original = JsonSerializer.Serialize(source); var document = LegacyThemeAdapter.Read(source);
            var target = new PixelSize(1920, 462); var selection = LayoutSelector.Select(document, target);
            var contained = LayoutResolver.Resolve(selection.Layout, target);
            var filled = EditableLayoutAdapter.CaptureWithFilledBackground(document, contained, "目标设备铺满");
            var background = filled.Layout.Layers[0]; var clock = filled.Layout.Layers[1];
            Check(filled.Layout.Width == 1920 && filled.Layout.Height == 462 && background.X == 0 && background.Y == 0 && background.Width == 1920 && background.Height == 462, "Background adaptation uses actual target dimensions and covers the whole surface");
            Check(background.Fill == ImageDisplayMode.Fill && background.Path == path && background.Id == source.Landscape.Layers[0].Id, "Background switches to proportional crop while retaining source reference and layer identity");
            Check(clock.Width / clock.Height == source.Landscape.Layers[1].Width / source.Landscape.Layers[1].Height && clock.FontSize == 100 * contained.UniformScale, "Clock keeps its aspect ratio and uniform font scale instead of stretching with background");
            Check(clock.X == contained.Layers[1].Bounds.X && clock.Y == contained.Layers[1].Bounds.Y, "Foreground positions preserve the original proportional layout adaptation");
            Check(JsonSerializer.Serialize(source) == original, "Generating a filled background layout never mutates original landscape or portrait sources");
            var store = new ThemeProjectStore(Path.Combine(directory, "themes")); await store.SaveAsync(source);
            var sourcePath = Path.Combine(directory, "themes", source.Id.ToString("N") + ".theme.json"); var bytes = await File.ReadAllBytesAsync(sourcePath);
            var copy = source.Clone(); copy.Version = 2; copy.LayoutVersions.Add(filled); copy.SelectedLayoutId = filled.Id; copy.Validate();
            Check(bytes.SequenceEqual(await File.ReadAllBytesAsync(sourcePath)), "Preparing the new layout does not silently overwrite a saved project file");
            await store.SaveAsync(copy); var restored = await store.ReadSavedAsync(source.Id);
            Check(restored.ActiveLayout.Width == 1920 && restored.ActiveLayout.Height == 462 && restored.Landscape.Width == 1920 && restored.Landscape.Height == 480, "Saving and reopening keeps new device layout and unchanged original canvas independently");
            var selected = LayoutSelector.Select(LegacyThemeAdapter.Read(restored), target);
            Check(selected.Layout.LayoutId == filled.Id && selected.Match == LayoutMatch.Exact, "Device apply selects the new exact-ratio layout instead of the former four-to-one layout");
            using var displays = new WorkspaceTests.Manager(); var factory = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, displays, logger);
            await using var before = await factory.CreatePreviewAsync(document, contained, CancellationToken.None);
            var nextDocument = LegacyThemeAdapter.Read(restored);
            await using var after = await factory.CreatePreviewAsync(nextDocument, LayoutResolver.Resolve(selected.Layout, target), CancellationToken.None);
            using var renderer = new SceneJpegRenderer(); var beforeJpeg = renderer.Render(before.Scene, 0); var afterJpeg = renderer.Render(after.Scene, 0);
            Check(!Bright(beforeJpeg, 4, 230) && Bright(afterJpeg, 4, 230) && Bright(afterJpeg, 1915, 230), "Real JPEG rendering removes left and right background margins without altering clock geometry");
            Directory.CreateDirectory("artifacts"); await File.WriteAllBytesAsync("artifacts/background-adaptation-before.jpg", beforeJpeg); await File.WriteAllBytesAsync("artifacts/background-adaptation-after.jpg", afterJpeg);
            foreach (var size in new[] { new PixelSize(2560, 720), new PixelSize(462, 1920) })
            {
                var layout = LayoutSelector.Select(document, size).Layout;
                var adapted = EditableLayoutAdapter.CaptureWithFilledBackground(document, LayoutResolver.Resolve(layout, size), "另一设备");
                Check(adapted.Layout.Width == size.Width && adapted.Layout.Height == size.Height && adapted.Layout.Layers[0].Height == size.Height, "Background fill supports different resolutions and independent portrait layout without fixed D92 dimensions");
            }
            var noBackground = source.Clone(); noBackground.Landscape.Layers.RemoveAt(0);
            var noneDoc = LegacyThemeAdapter.Read(noBackground);
            try { EditableLayoutAdapter.CaptureWithFilledBackground(noneDoc, LayoutResolver.Resolve(LayoutSelector.Select(noneDoc, target).Layout, target), "无背景"); throw new Exception("No background accepted"); } catch (InvalidOperationException ex) when (ex.Message.Contains("背景图片")) { }
            Check(noBackground.Landscape.Layers.Single().Kind == ThemeElementKind.DigitalClock, "No full-canvas background reports a clear error without resizing a clock or small image");
            var registry = new DeviceRegistry();
            var device = new DeviceDescriptor(OutputDeviceKeys.D92("background-test"), "D92", DeviceTransport.UsbHid, new("test", "background-test", IdentityConfidence.VerifiedStable), new("virtual-background", "virtual-background", 0), true);
            registry.Replace(DeviceTransport.UsbHid, [device]);
            await using var sessions = new OutputSessionManager(registry, new InMemoryDeviceLeaseProvider(), (_, _) => throw new InvalidOperationException("Preview must not start an output session"));
            using var managed = new ManagedDevicesViewModel(registry, sessions, new DevicePreferenceStore(Path.Combine(directory, "devices.json")), () => [source], () => [], _ => Task.CompletedTask, logger);
            managed.PreviewFactory = factory.CreatePreviewAsync; await managed.InitializeAsync();
            var card = managed.Cards.Single(); card.Width = "1920"; card.Height = "462";
            await using var model = new CreatorDevicePreviewViewModel(source, managed, logger);
            NamedThemeLayout? generated = null; model.EditAdaptedLayout = layout => generated = layout; await model.PendingPreview;
            var window = new CreatorDevicePreview(model) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            try
            {
                window.Show(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                ((Expander)window.FindName("AdaptationOptions")).IsExpanded=true;window.UpdateLayout();
                var button = Descendants<Button>(window).Single(b => Equals(b.Content, "生成铺满布局"));
                Check(button.IsEnabled, "Real device preview exposes an enabled generate-filled-layout action for editable sources");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await model.DisposeAsync();
            }
            finally { window.Close(); }
            Check(generated is not null && generated.Layout.Width == 1920 && generated.Layout.Height == 462 && generated.Layout.Layers[0].Fill == ImageDisplayMode.Fill, "Actual WPF fill action returns the exact device layout to the editor callback");
            Check(sessions.Snapshot.Length == 0 && JsonSerializer.Serialize(source) == original, "Generating a device layout neither starts HID output nor changes the source preview project");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var nested in Descendants<T>(child)) yield return nested; } }
    private static bool Bright(byte[] jpeg, int x, int y)
    {
        using var stream = new MemoryStream(jpeg); var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var bitmap = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0); var pixel = new byte[4]; bitmap.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0); return pixel[2] > 180 && pixel[1] > 100;
    }
    private static void Check(bool result, string name) { if (!result) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
}

