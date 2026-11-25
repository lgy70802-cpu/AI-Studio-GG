using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class SceneTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var scene = new ClockScene();
        scene.UpdateTime(new DateTimeOffset(2026, 9, 13, 23, 59, 59, TimeSpan.FromHours(8)));
        Check(scene.WeekdayText == "星期日", "ClockScene formats Chinese weekday");
        var midnightConsistent = true;
        scene.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClockScene.CurrentTime) && scene.TimeText == "00:00:00")
                midnightConsistent &= scene.DateText == "2026年09月14日" && scene.WeekdayText == "星期一";
        };
        scene.UpdateTime(scene.CurrentTime.AddSeconds(1));
        Check(midnightConsistent && scene.TimeText == "00:00:00" && scene.DateText == "2026年09月14日" && scene.WeekdayText == "星期一",
            "Time, date and weekday roll over from one atomic timestamp");

        var timeProvider = new CountingClock { Now = scene.CurrentTime.ToUniversalTime() };
        using var provider = new ClockSceneDataProvider(scene, logger, timeProvider);
        provider.Start();
        provider.Start();
        Check(timeProvider.ReadCount == 1, "Starting the shared provider twice does not duplicate timers or initial samples");
        timeProvider.Now = timeProvider.Now.AddSeconds(1);
        await Task.Delay(1150);
        Check(timeProvider.ReadCount == 2 && scene.CurrentTime == timeProvider.Now, "Shared clock samples exactly once on the next interval");
        timeProvider.Fail = true;
        var previous = scene.CurrentTime;
        await Task.Delay(1050);
        Check(scene.CurrentTime == previous, "Clock provider failure preserves last valid scene");
        provider.Dispose();
        var reads = timeProvider.ReadCount;
        await Task.Delay(1050);
        Check(!provider.IsRunning && timeProvider.ReadCount == reads, "Disposed shared clock has no pending sampling work");

        // Restore a fixed readable sample for deterministic images and ratio assertions.
        scene.UpdateTime(new DateTimeOffset(2026, 9, 14, 12, 34, 56, TimeSpan.FromHours(8)));
        var view = new SceneView { Scene = scene, DataContext = scene };
        var host = new PreviewHost { Child = view };
        BindingOperations.SetBinding(host, PreviewHost.AspectRatioProperty, new Binding(nameof(Scene.AspectRatio)) { Source = scene });
        scene.SetCanvasSize(3200, 500);
        Arrange(host, new Size(640, 300));
        Check(Math.Abs(view.ActualWidth / view.ActualHeight - 6.4) < 0.00001 && view.ActualWidth == 640,
            "Ultrawide preview preserves the target aspect ratio without stretching");
        SaveImage(view, "clock-preview-ultrawide.png");
        scene.SetCanvasSize(500, 2000);
        Arrange(host, new Size(640, 300));
        Check(Math.Abs(view.ActualWidth / view.ActualHeight - 0.25) < 0.00001 && view.ActualHeight == 300,
            "Portrait preview fits by height with the same SceneView");
        SaveImage(view, "clock-preview-portrait.png");

        var target = new DisplayInfo("EXTERNAL", "Wide display", "scene-target", 1920, 0, 3000, 500, false);
        using var manager = new FakeManager();
        var settings = new AppSettings { TargetDisplay = new TargetDisplaySettings
        {
            DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = 3000, LastHeight = 500
        } };
        var targets = new TargetDisplayService(settings, new MemorySettings(), logger);
        using var devices = new DevicesViewModel(manager, targets, logger);
        using var main = new MainViewModel(devices, scene);
        await devices.RefreshAsync();
        Check(scene.AspectRatio == 6 && main.PreviewDescription.Contains("未连接"), "Disconnected saved target supplies the preview's last known ratio");
        manager.Displays = [target with { Width = 2048, Height = 512 }];
        await devices.RefreshAsync();
        Check(scene.AspectRatio == 4 && ReferenceEquals(main.Scene, scene), "Target resolution change updates the existing shared scene canvas");
        manager.Displays = [];
        await devices.RefreshAsync();
        Check(scene.AspectRatio == 4, "Unplug preserves last known preview ratio");
        await devices.ClearTargetAsync();
        Check(Math.Abs(scene.AspectRatio - 16d / 9) < 0.00001 && main.PreviewDescription.Contains("示例比例"), "No target uses an explicitly labelled example ratio");
        Console.WriteLine("All shared-scene tests passed.");
    }

    private static void Arrange(PreviewHost host, Size space)
    {
        host.Measure(space);
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
    }

    private static void SaveImage(SceneView view, string name)
    {
        var image = new RenderTargetBitmap((int)Math.Round(view.ActualWidth), (int)Math.Round(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(file);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class CountingClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public int ReadCount { get; private set; }
        public bool Fail { get; set; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow()
        {
            ReadCount++;
            return Fail ? throw new InvalidOperationException("Simulated clock provider fault") : Now;
        }
    }

    private sealed class FakeManager : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Displays { get; set; } = [];
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(Displays);
        public void Dispose() { }
    }

    private sealed class MemorySettings : ISettingsService
    {
        public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings());
        public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true);
    }
}
