using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Display.Views;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DeskDisplay.HardwareAcceptance;
internal static class Program
{
    private sealed record Plan(string SerialNumber, int Width, int Height, int RotationDegrees, int DurationSeconds, string Orientation = "Landscape");
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is not ["--run-real-device", var planPath, var outputDirectory]) { Console.Error.WriteLine("Explicit opt-in required: --run-real-device <plan.json> <new-output-directory>"); return 2; }
        if (Directory.Exists(outputDirectory)) throw new IOException("Output directory must be new; existing acceptance evidence is never overwritten.");
        var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(planPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Missing plan");
        if (plan.DurationSeconds is < 60 or > 900 || plan.Width is < 1 or > 8192 || plan.Height is < 1 or > 8192 || (long)plan.Width * plan.Height > 16_777_216 || plan.RotationDegrees is not (0 or 90 or 180 or 270) || (!Enum.TryParse<ThemeOrientation>(plan.Orientation, out var parsedOrientation) || !Enum.IsDefined(parsedOrientation))) throw new InvalidDataException("Invalid acceptance plan");
        var found = D92HidDevice.Enumerate().Where(d => d.SerialNumber == plan.SerialNumber).ToArray();
        if (found.Length != 1) throw new IOException("Exactly one matching real D92 is required");
        foreach (var name in new[] { "MiraBox Craft", "DeskDisplay.App", "DeskDisplay.UsbDemo" }) { var processes = Process.GetProcessesByName(name); try { if (processes.Length > 0) throw new IOException("Close competing output application first: " + name); } finally { foreach (var process in processes) process.Dispose(); } }
        Directory.CreateDirectory(outputDirectory);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var task = RunAsync(plan, Path.GetFullPath(outputDirectory)); var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher; _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default); Dispatcher.PushFrame(frame);
        try { task.GetAwaiter().GetResult(); return 0; } catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static async Task RunAsync(Plan plan, string directory)
    {
        using var evidence = new Evidence(Path.Combine(directory, "events.jsonl")); using var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(evidence).CreateLogger();
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskDisplay", "settings.json");
        var settingsHash = File.Exists(settingsPath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(settingsPath))) : null;
        evidence.Write(new { kind = "plan", time = DateTimeOffset.Now, plan, settingsMode = "isolated diagnostic directory; user settings read-only" });
        var initial = new AppSettings { UsbOutput = new() { SerialNumber = plan.SerialNumber, Width = plan.Width, Height = plan.Height, RotationDegrees = plan.RotationDegrees } };
        var settings = new JsonSettingsService(logger, Path.Combine(directory, "isolated-settings.json")); await settings.SaveAsync(initial);
        using var displayManager = new WindowsDisplayManager(logger); using var devices = new DevicesViewModel(displayManager, new TargetDisplayService(initial, settings, logger), logger);
        using var main = new MainViewModel(devices, new ClockScene()); var target = new D92HidRenderTarget(logger);
        D92OutputState? lastState = null; var waits = 0; var failures = 0; long reconnectFrame = -1; var sawStreaming = false;
        target.StateChanged += state => { lastState = state; if (state.Status == "WaitingForDevice") Interlocked.Increment(ref waits); if (state.Status is "Failed" or "BlockedByCraft") Interlocked.Increment(ref failures); if (state.Status == "Connected" && state.Connections >= 2) reconnectFrame = state.Frames; if (state.Status == "Streaming") sawStreaming = true; evidence.Write(new { kind = "transport", time = DateTimeOffset.Now, state }); };
        using var usb = new UsbOutputViewModel(main, settings, initial, logger, target); main.SetUsb(usb); await usb.RefreshAsync(); if (!usb.IsSelectedConnected) throw new IOException("Selected device is not connected");
        var imagePath = Path.Combine(directory, "acceptance-background.png"); CreateImage(imagePath, plan.Width, plan.Height);
        var store = new ThemeProjectStore(Path.Combine(directory, "themes")); var orientation = Enum.Parse<ThemeOrientation>(plan.Orientation);
        var a = MakeTheme("A · BLUE · LIVE CLOCK", "#173A62", "#8DC8FF", orientation, plan.Width, plan.Height, imagePath);
        var b = MakeTheme("B · ORANGE · LIVE CLOCK", "#64361C", "#FFD195", orientation, plan.Width, plan.Height, null);
        await store.SaveAsync(a); await store.SaveAsync(b);
        using var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); await creator.RefreshProjectsAsync(); await creator.LoadThemePlaylistAsync(); creator.AddThemePlaylist(a.Id); creator.AddThemePlaylist(b.Id); foreach (var row in creator.ThemePlaylistItems) row.Seconds = 20;
        var sceneView = new SceneView { Scene = main.Scene }; var caption = new TextBlock { Margin = new(16), TextWrapping = TextWrapping.Wrap, FontSize = 16 };
        var panel = new DockPanel(); DockPanel.SetDock(caption, Dock.Top); panel.Children.Add(caption); panel.Children.Add(sceneView);
        var window = new Window { Title = "D92 REAL HARDWARE ACCEPTANCE — live editable themes", Width = 1100, Height = 500, Content = panel };
        main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.Scene)) sceneView.Scene = main.Scene; };
        using var stop = new CancellationTokenSource(); var finishing = false; window.Closing += (_, _) => { if (!finishing) stop.Cancel(); }; window.Show();
        var timer = Stopwatch.StartNew(); var transitions = 0; var title = ""; var absentSamples = 0; var returnedAfterAbsence = false; var everAbsent = false; bool? previousPresence = null; var pauseChecked = false; var resumed = false; var manualNext = false; long staticFrames = 0; var completed = false;
        try
        {
            main.PreviewScene(await new ImageSceneLoader().LoadAsync(imagePath, ImageDisplayMode.Uniform)); await usb.ApplyCurrentAsync();
            for (var i = 0; i < 15; i++) { await Task.Delay(1000, stop.Token); caption.Text = "静态画面验收：请观察方向、边界和清晰度。" + usb.Status; }
            staticFrames = lastState?.Frames ?? 0; if (staticFrames == 0 || !sawStreaming) throw new IOException("Static output did not stream");
            await creator.StartThemePlaylistAsync(); if (!creator.IsThemeRotating) throw new IOException("Production theme playlist did not start: " + creator.ThemePlaylistStatus);
            evidence.Write(new { kind = "phase", phase = "theme-playlist", time = DateTimeOffset.Now, staticFrames });
            while (timer.Elapsed.TotalSeconds < plan.DurationSeconds)
            {
                await Task.Delay(1000, stop.Token);
                var present = await Task.Run(() => D92HidDevice.Enumerate().Any(d => d.SerialNumber == plan.SerialNumber));
                if (!present) { absentSamples++; everAbsent = true; } else if (everAbsent) returnedAfterAbsence = true;
                if (previousPresence != present) { evidence.Write(new { kind = "presence", time = DateTimeOffset.Now, present }); previousPresence = present; }
                if (creator.ThemeCurrentName != title) { title = creator.ThemeCurrentName; transitions++; evidence.Write(new { kind = "theme", time = DateTimeOffset.Now, title, creator.IsThemePaused, geometry = main.Scene is ThemeScene active ? ThemeOutputGeometry.Resolve(plan.Width, plan.Height, plan.RotationDegrees, active.Project.Orientation) : (ThemeOutputGeometry?)null }); }
                var elapsed = timer.Elapsed.TotalSeconds;
                if (!pauseChecked && elapsed >= 80) { creator.ToggleThemePause(); pauseChecked = creator.IsThemePaused; evidence.Write(new { kind = "action", action = "pause", time = DateTimeOffset.Now, passed = pauseChecked }); }
                if (!resumed && elapsed >= 95) { if (creator.IsThemePaused) creator.ToggleThemePause(); resumed = !creator.IsThemePaused; evidence.Write(new { kind = "action", action = "resume", time = DateTimeOffset.Now, passed = resumed }); }
                if (!manualNext && elapsed >= 110) { await creator.AdvanceThemePlaylistAsync(); manualNext = true; evidence.Write(new { kind = "action", action = "manual-next", time = DateTimeOffset.Now }); }
                caption.Text = $"剩余 {Math.Max(0, plan.DurationSeconds - (int)elapsed)} 秒 · {creator.ThemeCurrentName} · {creator.ThemeCountdown}\n{usb.Status}\n设备枚举：{present}；观察画面秒数。拔插时请保持数据线断开至少5秒。";
                if (!creator.IsThemeRotating || !usb.IsRunning) throw new IOException("Production output or playlist stopped: " + creator.ThemePlaylistStatus + "; " + usb.Status);
                if ((int)elapsed % 10 == 0) evidence.Write(new { kind = "sample", time = DateTimeOffset.Now, elapsed, state = lastState, memoryBytes = Process.GetCurrentProcess().PrivateMemorySize64, cpuMilliseconds = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds });
            }
            completed = true;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { evidence.Write(new { kind = "cancelled", time = DateTimeOffset.Now }); }
        finally
        {
            creator.StopThemePlaylist(); await usb.StopAsync(); await creator.PrepareForCloseAsync(); await usb.PrepareForCloseAsync();
            var unchanged = settingsHash == (File.Exists(settingsPath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(settingsPath))) : null);
            var final = lastState;
            var result = new { time = DateTimeOffset.Now, completed, durationSeconds = timer.Elapsed.TotalSeconds, staticFrames, finalState = final, themeTransitions = transitions, pauseChecked, resumed, manualNext, absentSamples, returnedAfterAbsence, waitingEvents = waits, failures, userSettingsUnchanged = unchanged, reconnectEvidence = everAbsent && returnedAfterAbsence && waits > 0 && final?.Connections >= 2 && reconnectFrame >= 0 && final.Frames > reconnectFrame + 5, visualConfirmation = "pending user response; frame writes do not prove correct physical display" };
            await File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); evidence.Write(new { kind = "result", result });
            finishing = true; window.Close(); sceneView.Scene = null;
            if (!unchanged) throw new IOException("User settings changed externally during acceptance; inspect before claiming unchanged");
        }
    }
    private static ThemeProject MakeTheme(string name, string background, string foreground, ThemeOrientation orientation, int width, int height, string? image)
    {
        var landscape = new ThemeLayout { Width = Math.Max(width, height), Height = Math.Min(width, height), Background = background };
        var portrait = new ThemeLayout { Width = Math.Min(width, height), Height = Math.Max(width, height), Background = background };
        var project = new ThemeProject { Name = name, Orientation = orientation, Landscape = landscape, Portrait = portrait }; var l = project.ActiveLayout;
        if (image is not null) l.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "editable static image", Path = image, AssetOrigin = ThemeAssetOrigin.Local, Width = l.Width, Height = l.Height, Opacity = .3 });
        l.Layers.Add(new() { Kind = ThemeElementKind.Text, Text = "TOP / " + name, Name = "editable title", X = l.Width * .05, Y = l.Height * .04, Width = l.Width * .9, Height = l.Height * .2, FontSize = Math.Min(l.Width * .06, l.Height * .16), Color = foreground, ZIndex = 1 });
        l.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Format = "HH:mm", ShowSeconds = true, Name = "real system clock", X = l.Width * .05, Y = l.Height * .3, Width = l.Width * .9, Height = l.Height * .3, FontSize = Math.Min(l.Width * .17, l.Height * .3), Color = "#FFFFFF", ZIndex = 2 });
        l.Layers.Add(new() { Kind = ThemeElementKind.Date, Format = "yyyy-MM-dd", Name = "real system date", X = l.Width * .05, Y = l.Height * .75, Width = l.Width * .9, Height = l.Height * .15, FontSize = Math.Min(l.Width * .08, l.Height * .12), Color = foreground, ZIndex = 3 });
        project.Validate(); return project;
    }
    private static void CreateImage(string path, int width, int height)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.MidnightBlue, null, new Rect(0, 0, width, height)); dc.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, width * .04, height)); dc.DrawRectangle(Brushes.Orange, null, new Rect(width * .96, 0, width * .04, height)); dc.DrawRectangle(Brushes.White, new Pen(Brushes.Red, 5), new Rect(width * .45, height * .25, width * .1, height * .5)); }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private sealed class Evidence : ILogEventSink, IDisposable
    {
        private readonly object _gate = new(); private readonly StreamWriter _writer;
        public Evidence(string path) { _writer = new(path) { AutoFlush = true }; }
        public void Write(object value) { var json = JsonSerializer.Serialize(value); lock (_gate) { _writer.WriteLine(json); Console.WriteLine(json); } }
        public void Emit(LogEvent logEvent) => Write(new { kind = "log", time = logEvent.Timestamp, level = logEvent.Level.ToString(), message = logEvent.RenderMessage(), error = logEvent.Exception?.Message });
        public void Dispose() { lock (_gate) _writer.Dispose(); }
    }
}
