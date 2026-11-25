using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class DashboardTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-dashboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var imagePath = Path.Combine(root, "background.png");
            CreateImage(imagePath);
            var targetDisplay = new DisplayInfo("DASHBOARD", "Dashboard target", "dashboard-target", 1920, 0, 1920, 462, false);
            var initial = new AppSettings
            {
                LastScene = "组合",
                DashboardImagePath = imagePath,
                DashboardLayout = DashboardLayout.Split,
                DashboardShowPerformance = true,
                TargetDisplay = new TargetDisplaySettings { DeviceId = targetDisplay.DeviceId, DeviceName = targetDisplay.DeviceName,
                    FriendlyName = targetDisplay.FriendlyName, LastWidth = 1920, LastHeight = 462 },
                UsbOutput = new UsbOutputSettings { SerialNumber = "dashboard-usb", Width = 1920, Height = 462, RotationDegrees = 90 }
            };
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            await settings.SaveAsync(initial);
            using var manager = new WorkspaceTests.Manager { Displays = [targetDisplay] };
            using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); clock.UpdateTime(new DateTimeOffset(2026, 10, 8, 22, 48, 15, TimeSpan.FromHours(8)));
            var performance = new PerformanceScene();
            performance.Update(new SensorSnapshot { CpuLoad = 32.5, CpuTemperature = 61, GpuLoad = 48, GpuTemperature = 58, MemoryLoad = 67 }, TimeSpan.FromSeconds(1));
            using var main = new MainViewModel(devices, clock);
            var usbTarget = new Target();
            var usb = new UsbOutputViewModel(main, settings, initial, logger, usbTarget, () => []);
            main.SetUsb(usb);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            var workspace = new WorkspaceViewModel(main, clock, performance, output, settings, new WorkspaceTests.Sensors(), initial, logger, false);
            main.SetWorkspace(workspace);
            var library = new LibraryViewModel(main, new WallpaperCatalog(logger, Path.Combine(root, "catalog")), logger);
            main.SetLibrary(library);
            try
            {
                await devices.RefreshAsync(); await workspace.RestoreAsync(); await library.InitializeAsync();
                library.SelectedTab = "小组件";
                Check(main.Scene is DashboardScene && workspace.IsReady, "Saved wallpaper and component scene restores without auto-output");
                var dashboard = (DashboardScene)main.Scene;
                Check(ReferenceEquals(dashboard.Clock, clock) && ReferenceEquals(dashboard.Performance, performance)
                    && dashboard.Background?.Path == imagePath, "Dashboard reuses shared clock, sensors and bounded image scene");
                var window = new MainWindow { DataContext = main };
                try
                {
                    window.Show(); window.UpdateLayout(); await Task.Delay(50);
                    Capture(window, "dashboard-workspace.png");
                }
                finally { window.Close(); }
                var notifications = 0;
                dashboard.PropertyChanged += (_, _) => notifications++;
                using var renderer = new SceneJpegRenderer();
                var split = renderer.Render(dashboard, 0);
                clock.UpdateTime(clock.CurrentTime.AddSeconds(1));
                var nextSecond = renderer.Render(dashboard, 0);
                Check(notifications > 0 && !split.SequenceEqual(nextSecond), "Clock sampling invalidates and changes the shared dashboard pixels");
                workspace.DashboardLayoutName = "时钟居中";
                var centered = renderer.Render(dashboard, 0);
                workspace.DashboardLayoutName = "时钟居左";
                var left = renderer.Render(dashboard, 0);
                Check(!centered.SequenceEqual(left) && !left.SequenceEqual(split), "Three dashboard layouts produce distinct compositions");
                workspace.DashboardShowPerformance = false;
                var noPerformance = renderer.Render(dashboard, 0);
                performance.Update(new SensorSnapshot { CpuLoad = 92, CpuTemperature = 72, GpuLoad = 83, GpuTemperature = 69, MemoryLoad = 81 }, TimeSpan.FromSeconds(1));
                var stillNoPerformance = renderer.Render(dashboard, 0);
                Check(noPerformance.SequenceEqual(stillNoPerformance), "Hidden performance values do not change dashboard pixels");
                workspace.DashboardShowPerformance = true;
                var withPerformance = renderer.Render(dashboard, 0);
                Check(!withPerformance.SequenceEqual(noPerformance), "Optional performance overlay renders current CPU, GPU and RAM values");
                var outputDirectory = AppContext.BaseDirectory;
                File.WriteAllBytes(Path.Combine(outputDirectory, "dashboard-clock-left.jpg"), withPerformance);
                workspace.DashboardLayoutName = "时钟与性能";
                File.WriteAllBytes(Path.Combine(outputDirectory, "dashboard-split.jpg"), renderer.Render(dashboard, 0));
                workspace.ApplyCommand.Execute(null); await workspace.PendingOperation;
                Check(ReferenceEquals(output.CurrentScene, main.Scene), "Windows output and preview share the dashboard scene instance");
                workspace.ApplyUsbCommand.Execute(null); await workspace.PendingOperation;
                await WaitUntil(() => usbTarget.Frames > 0);
                Check(usb.IsRunning && !output.IsOpen && ReferenceEquals(main.Scene, dashboard), "D92 shortcut transfers the same dashboard scene and closes Windows output");
                using (var stream = new MemoryStream(usbTarget.Jpeg!))
                {
                    var frame = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    Check(frame.PixelWidth == 462 && frame.PixelHeight == 1920, "Dashboard D92 JPEG uses configured canvas and output rotation");
                }
                await usb.StopAsync();
                await workspace.PrepareForCloseAsync();
                var saved = await settings.LoadAsync();
                Check(saved.LastScene == "组合" && saved.DashboardImagePath == imagePath
                    && saved.DashboardLayout == DashboardLayout.Split && saved.DashboardShowPerformance,
                    "Dashboard background, layout and performance preference survive restart");
                Console.WriteLine("All dashboard tests passed.");
            }
            finally { await usb.PrepareForCloseAsync(); await workspace.PrepareForCloseAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!condition()) throw new TimeoutException("Dashboard USB test state transition timed out");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private static void CreateImage(string path)
    {
        const int width = 1920, height = 462;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new LinearGradientBrush(Color.FromRgb(28, 56, 92), Color.FromRgb(101, 61, 117), 0), null, new Rect(0, 0, width, height));
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 202, 91)), null, new Point(1550, 115), 72, 72);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
    private static void Capture(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); png.Save(file);
    }
    private sealed class Target : ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;
        public int Frames;
        public byte[]? Jpeg;
        public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Jpeg = await render(token); Frames++;
                    StateChanged?.Invoke(new("Streaming", Frames, 1));
                    await Task.Delay(40, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }
}
