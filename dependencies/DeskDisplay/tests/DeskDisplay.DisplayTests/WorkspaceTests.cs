using System.IO;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class WorkspaceTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            var target = new DisplayInfo("SECONDARY", "Test display", "workspace-target", 1920, 0, 1280, 480, false);
            var initial = new AppSettings { LastScene = "性能", AutoDisplayOnStartup = true, LastVideoPath = Path.Combine(root, "clip.mp4"), VideoFps = 60,
                TargetDisplay = new TargetDisplaySettings { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = 1280, LastHeight = 480 } };
            await File.WriteAllTextAsync(initial.LastVideoPath, "fake player test input");
            await store.SaveAsync(initial);
            using var manager = new Manager { Displays = [target] };
            using var output = new Output();
            var targets = new TargetDisplayService(initial, store, logger);
            using var devices = new DevicesViewModel(manager, targets, logger, output);
            var clock = new ClockScene();
            var performance = new PerformanceScene();
            using var main = new MainViewModel(devices, clock);
            var playback = new Playback();
            main.SetImageEditor(new ImageSceneViewModel(devices, store, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, playback, logger));
            using var workspace = new WorkspaceViewModel(main, clock, performance, output, store, new Sensors(), initial, logger, false);
            main.SetWorkspace(workspace);
            Check(!workspace.CanEdit, "Startup restore blocks conflicting edits");
            await devices.RefreshAsync();
            await workspace.RestoreAsync();
            Check(ReferenceEquals(main.Scene, performance) && ReferenceEquals(output.CurrentScene, performance), "Startup restores performance and applies only to saved target");
            Check(workspace.OutputStatus.Contains("与预览同步"), "Output status identifies synchronized scene");
            var window = new DeskDisplay.App.Views.MainWindow { DataContext = main };
            try
            {
                window.Show(); window.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var imageFile = File.Create(Path.Combine(AppContext.BaseDirectory, "workspace-home.png"));
                encoder.Save(imageFile);
            }
            finally { window.Close(); }
            workspace.SelectedScene = "时钟";
            await workspace.PendingOperation;
            Check(workspace.OutputStatus.Contains("预览尚未应用"), "Draft selection does not mislabel existing output");
            workspace.ApplyCommand.Execute(null);
            await workspace.PendingOperation;
            Check(ReferenceEquals(output.CurrentScene, clock), "Unified apply uses selected clock");
            workspace.SelectedScene = "视频";
            await workspace.PendingOperation;
            Check(playback.IsPlaying && playback.FramesPerSecond == 60, "Video path and FPS restore through scene selection");
            workspace.ApplyCommand.Execute(null);
            await workspace.PendingOperation;
            Check(ReferenceEquals(output.CurrentScene, main.Scene), "Unified video output shares preview scene");
            workspace.SelectedScene = "性能";
            await workspace.PendingOperation;
            Check(!playback.IsPlaying && !output.IsOpen, "Leaving video releases playback and old output");
            for (var index = 0; index < 40; index++)
            {
                workspace.SelectedScene = "视频"; await workspace.PendingOperation;
                workspace.SelectedScene = "性能"; await workspace.PendingOperation;
            }
            Check(playback.ActiveSessions == 0 && playback.OpenCount == playback.CloseCount, "Repeated scene switching leaves no active video sessions");
            workspace.ApplyCommand.Execute(null); await workspace.PendingOperation;
            workspace.Suspend();
            Check(!output.IsOpen && !workspace.ApplyCommand.CanExecute(null), "Suspend closes output and blocks apply");
            await workspace.ResumeAsync();
            Check(!output.IsOpen && workspace.Status.Contains("手动应用"), "Resume revalidates display but does not silently reopen output");
            workspace.AutoDisplayOnStartup = false;
            main.Videos!.Fps = 30;
            main.Images!.Mode = ImageDisplayMode.Center;
            await workspace.PrepareForCloseAsync();
            var saved = await store.LoadAsync();
            Check(saved.LastScene == "性能" && !saved.AutoDisplayOnStartup && saved.LastVideoPath == initial.LastVideoPath
                && saved.VideoFps == 30 && saved.ImageMode == ImageDisplayMode.Center && saved.TargetDisplay?.DeviceId == target.DeviceId,
                "Scene, FPS, mode and auto preference survive writes alongside target identity");

            // New session with a missing file and a valid display must never fall back to clock output.
            initial.LastScene = "视频"; initial.LastVideoPath = Path.Combine(root, "missing.mp4");
            using var output2 = new Output();
            using var devices2 = new DevicesViewModel(manager, new TargetDisplayService(initial, store, logger), logger, output2);
            using var main2 = new MainViewModel(devices2, new ClockScene());
            main2.SetImageEditor(new ImageSceneViewModel(devices2, store, initial, (ClockScene)main2.Scene, main2.PreviewScene, logger));
            main2.SetVideoEditor(new VideoSceneViewModel(main2, new Playback(), logger));
            using var restored = new WorkspaceViewModel(main2, main2.Scene, new PerformanceScene(), output2, store, new Sensors(), initial, logger, false);
            await devices2.RefreshAsync(); await restored.RestoreAsync();
            Check(!output2.IsOpen && !restored.ApplyCommand.CanExecute(null) && restored.Status.Contains("未启动"), "Missing saved video prevents auto output without fallback scene");
            manager.Displays = [];
            await devices2.RefreshAsync();
            restored.SelectedScene = "性能"; await restored.PendingOperation;
            Check(!restored.ApplyCommand.CanExecute(null), "Disconnected target keeps unified apply disabled");
            Check(restored.Diagnostics.Contains("不可用") && restored.Diagnostics.Contains("进程内存"), "Diagnostics report absent sensors and resource metrics");
            await restored.PrepareForCloseAsync();
            Console.WriteLine("All workspace tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    internal sealed class Sensors : ISensorService
    {
        public SensorSnapshot Current { get; } = new();
        public Task StartAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
    }
    internal sealed class Manager : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Displays { get; set; } = [];
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(Displays);
        public void Dispose() { }
    }
    internal sealed class Output : IRenderWindowService
    {
        public bool IsOpen => CurrentScene is not null;
        public Scene? CurrentScene { get; private set; }
        public string Status => IsOpen ? "已显示" : "已停止";
        public event EventHandler? StateChanged;
        public void ShowTest(DisplayInfo target) => ShowScene(target, new ClockScene());
        public void ShowScene(DisplayInfo target, Scene scene) { CurrentScene = scene; StateChanged?.Invoke(this, EventArgs.Empty); }
        public void SynchronizeTarget(DisplayInfo? target) { if (target is null) Close(); }
        public void Close() { CurrentScene = null; StateChanged?.Invoke(this, EventArgs.Empty); }
        public void Dispose() => Close();
    }
    internal sealed class Playback : IVideoPlaybackService
    {
        public int ActiveSessions => IsPlaying ? 1 : 0;
        public int OpenCount, CloseCount;
        public bool IsPlaying { get; private set; }
        public string Status => IsPlaying ? "正在播放" : "已停止";
        public int FramesPerSecond { get; private set; }
        public event EventHandler? StateChanged;
        public Task OpenAsync(string path, int framesPerSecond, CancellationToken cancellationToken = default)
        { Stop(); IsPlaying = true; OpenCount++; FramesPerSecond = framesPerSecond; StateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
        public void Stop() { if (IsPlaying) CloseCount++; IsPlaying = false; StateChanged?.Invoke(this, EventArgs.Empty); }
        public void Dispose() => Stop();
    }
}
