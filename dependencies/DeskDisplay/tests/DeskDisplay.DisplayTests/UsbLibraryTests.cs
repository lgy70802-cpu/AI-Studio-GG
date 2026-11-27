using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class UsbLibraryTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-usb-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            var initial = new AppSettings { UsbOutput = new() { SerialNumber = "selected-device", Width = 320, Height = 100, RotationDegrees = 90 } };
            await settings.SaveAsync(initial);
            using var manager = new WorkspaceTests.Manager();
            using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene();
            using var main = new MainViewModel(devices, clock);
            var target = new Target();
            var usb = new UsbOutputViewModel(main, settings, initial, logger, target, () => []);
            main.SetUsb(usb);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            var workspace = new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false);
            main.SetWorkspace(workspace);
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog"));
            var first = new WallpaperAsset { Path = Path.Combine(root, "red.png"), Name = "红色壁纸", Width = 320, Height = 100 };
            var second = new WallpaperAsset { Path = Path.Combine(root, "blue.png"), Name = "蓝色壁纸", Width = 320, Height = 100 };
            var video = new WallpaperAsset { Path = Path.Combine(root, "movie.mp4"), Name = "视频", IsVideo = true };
            var missing = new WallpaperAsset { Path = Path.Combine(root, "missing.png"), Name = "丢失图片" };
            var corrupt = new WallpaperAsset { Path = Path.Combine(root, "corrupt.png"), Name = "损坏图片" };
            CreateImage(first.Path, Colors.Red); CreateImage(second.Path, Colors.Blue);
            await File.WriteAllTextAsync(video.Path, "not decoded for USB");
            await File.WriteAllTextAsync(corrupt.Path, "invalid image");
            await catalog.SaveAsync(new WallpaperLibrary { Assets = [first, video, missing, corrupt, second], Playlist = [first.Id, video.Id, missing.Id, corrupt.Id, second.Id], IntervalSeconds = 10 });
            var library = new LibraryViewModel(main, catalog, logger);
            main.SetLibrary(library);
            try
            {
                await workspace.RestoreAsync(); await library.InitializeAsync();
                Check(workspace.ApplyUsbCommand.CanExecute(null) && !workspace.ApplyCommand.CanExecute(null), "D92 component shortcut works without a Windows secondary display");
                workspace.ApplyUsbCommand.Execute(null); await workspace.PendingOperation;
                await WaitUntil(() => target.Frames > 0);
                Check(usb.IsRunning && target.Serial == "selected-device" && main.Scene.PixelWidth == 320 && main.Scene.PixelHeight == 100,
                    "D92 shortcut uses selected serial and configured arbitrary canvas");
                workspace.ApplyUsbCommand.Execute(null); await workspace.PendingOperation;
                Check(target.Calls == 1, "Repeated D92 apply reuses the running USB session");
                workspace.SelectedScene = "性能"; await workspace.PendingOperation;
                workspace.ApplyUsbCommand.Execute(null); await workspace.PendingOperation;
                Check(main.Scene is PerformanceScene && usb.IsRunning, "Performance component supports the D92 shortcut");
                await usb.StopAsync();
                usb.SelectedSerial = null;
                await workspace.ApplyUsbAsync();
                Check(main.SelectedNavigation == "USB屏" && !usb.IsRunning && target.Calls == 1, "Unconfigured D92 shortcut opens setup without choosing a device");
                usb.SelectedSerial = "selected-device";
                library.SelectedTab = "我的资源";
                var firstItem = library.VisibleItems.Single(item => item.Asset.Id == first.Id);
                var secondItem = library.VisibleItems.Single(item => item.Asset.Id == second.Id);
                library.Select(library.VisibleItems.Single(item => item.Asset.Id == video.Id));
                Check(library.ApplyUsbCommand.CanExecute(null), "D92 wallpaper detail exposes the new standalone video action");
                var videoAttempts=target.Calls;library.ApplyUsbCommand.Execute(null);await library.PendingOperation;
                Check(target.Calls==videoAttempts && !usb.IsRunning && library.Status.Contains("失败"), "Corrupt MP4 is rejected before opening a D92 output session");
                library.Select(firstItem);
                library.ApplyUsbCommand.Execute(null); await library.PendingOperation;
                await WaitUntil(() => target.Calls == 2 && target.Frames > 1);
                Check(main.Scene is ImageScene image && image.Path == first.Path && usb.IsRunning && !output.IsOpen,
                    "Wallpaper detail applies an image directly to D92 with no Windows output");
                var jpegBefore = target.Jpeg;
                library.Select(secondItem); library.ApplyUsbCommand.Execute(null); await library.PendingOperation;
                await WaitUntil(() => target.Jpeg is not null && jpegBefore is not null && !target.Jpeg.SequenceEqual(jpegBefore));
                Check(target.Calls == 2, "Switching wallpapers changes rendered pixels without reconnecting USB");
                using (var stream = new MemoryStream(target.Jpeg!))
                {
                    var frame = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    Check(frame.PixelWidth == 100 && frame.PixelHeight == 320, "D92 wallpaper respects saved output rotation");
                }
                jpegBefore = target.Jpeg;
                library.Select(firstItem); library.ApplyDashboardUsbCommand.Execute(null); await library.PendingOperation;
                await WaitUntil(() => main.Scene is DashboardScene && target.Jpeg is not null && jpegBefore is not null && !target.Jpeg.SequenceEqual(jpegBefore));
                Check(main.Scene is DashboardScene dashboard && dashboard.Background?.Path == first.Path && target.Calls == 2,
                    "Wallpaper detail creates a shared wallpaper and component scene without reconnecting D92");
                library.Select(secondItem); library.ApplyUsbCommand.Execute(null); await library.PendingOperation;
                await WaitUntil(() => main.Scene is ImageScene image && image.Path == second.Path);
                library.RotationTarget = "D92 USB";
                library.NextCommand.Execute(null); await library.PendingOperation;
                var before = ((ImageScene)main.Scene).Path;
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path != before && target.Calls == 2, "USB sequential playlist skips videos, missing files and corrupt images");
                library.PreviousCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path == before, "USB previous traverses only usable static wallpapers");
                library.Shuffle = true;
                before = ((ImageScene)main.Scene).Path;
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path != before, "USB shuffle avoids the current image when another usable image exists");
                library.Shuffle = false;
                library.RotationCommand.Execute(null); await library.PendingOperation;
                before = ((ImageScene)main.Scene).Path;
                Check(library.IsRotating && !library.CanChangeRotationTarget, "USB rotation locks its output destination while running");
                await Task.Delay(10600); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path != before && library.IsRotating, "USB rotation timer advances after the configured interval");
                target.Publish("WaitingForDevice"); await WaitUntil(() => usb.IsWaitingForDevice);
                before = ((ImageScene)main.Scene).Path;
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path == before && library.IsRotating, "USB disconnect pauses playlist advancement while preserving rotation intent");
                target.Publish("Streaming"); await WaitUntil(() => !usb.IsWaitingForDevice);
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)main.Scene).Path != before, "USB playlist continues after the selected device resumes");
                var window = new MainWindow { DataContext = main };
                try
                {
                    window.Show(); window.UpdateLayout(); await Task.Delay(50);
                    Capture(window, "usb-library-detail.png");
                    window.Width = 1100; window.Height = 740; window.UpdateLayout();
                    Capture(window, "usb-library-compact.png");
                }
                finally { window.Close(); }
                workspace.StopCommand.Execute(null); await usb.StopAsync();
                Check(!library.IsRotating && !usb.IsRunning && target.Active == 0, "Global stop cancels D92 rotation and awaits USB release");
                library.RotationCommand.Execute(null); await library.PendingOperation;
                await usb.StopAsync();
                Check(!library.IsRotating, "Stopping from USB controls also cancels the playlist timer");
                library.RotationCommand.Execute(null); await library.PendingOperation;
                workspace.Suspend(); await usb.StopAsync();
                Check(!library.IsRotating && !usb.IsRunning, "Suspend stops both D92 rotation and USB output");
                await workspace.ResumeAsync();
                Check(!library.IsRotating && !usb.IsRunning, "Resume does not automatically restart D92 rotation");
                library.PlaylistCommand.Execute(firstItem); library.PlaylistCommand.Execute(secondItem);
                library.RotationCommand.Execute(null); await library.PendingOperation;
                Check(!library.IsRotating && !usb.IsRunning, "USB playlist with no usable static images stops without starting output");
                await library.PrepareForCloseAsync(); await workspace.PrepareForCloseAsync();
                Check((await catalog.LoadAsync()).RotateToUsb, "D92 playlist destination survives catalog persistence without auto-start");
                Console.WriteLine("All D92 library tests passed.");
            }
            finally { await usb.PrepareForCloseAsync(); await library.PrepareForCloseAsync(); await workspace.PrepareForCloseAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!condition()) throw new TimeoutException("D92 library test state transition timed out");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private static void CreateImage(string path, Color color)
    {
        var bitmap = new WriteableBitmap(320, 100, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[320 * 100 * 4];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = 255; }
        bitmap.WritePixels(new Int32Rect(0, 0, 320, 100), pixels, 320 * 4, 0);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
    private static void Capture(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); png.Save(file);
    }
    private sealed class Target : ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;
        public int Calls, Active, Frames;
        public string? Serial;
        public byte[]? Jpeg;
        public void Publish(string state) => StateChanged?.Invoke(new(state, Frames, Calls));
        public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken token)
        {
            Calls++; Active++; Serial = serialNumber;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Jpeg = await render(token); Frames++;
                    if (Frames == 1) Publish("Streaming");
                    await Task.Delay(30, token);
                }
            }
            finally { Active--; }
        }
    }
}
