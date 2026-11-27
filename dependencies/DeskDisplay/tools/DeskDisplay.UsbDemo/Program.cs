using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Usb;
using DeskDisplay.Display.Views;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DeskDisplay.UsbDemo;

internal static class Program
{
    private sealed record Plan(string SerialNumber, int LogicalWidth, int LogicalHeight, int RotationDegrees, int DurationSeconds = 300, int JpegQuality = SceneJpegRenderer.DefaultQuality);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--self-test"]) return RendererChecks.Run();
        if (args is ["--wake", var serial])
        {
            var craft = System.Diagnostics.Process.GetProcessesByName("MiraBox Craft");
            try { if (craft.Length != 0) throw new InvalidOperationException("Exit Craft before waking D92"); }
            finally { foreach (var process in craft) process.Dispose(); }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            D92HidDevice.WakeAsync(serial, timeout.Token).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new { result = "wake_report_written", serial, visualConfirmation = "pending" }));
            return 0;
        }
        if (args is ["--send-static", var staticPlanPath, var staticImagePath])
        {
            var staticPlan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(staticPlanPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (staticPlan.DurationSeconds is < 1 or > 60) throw new ArgumentException("Static diagnostic must last 1–60 seconds");
            var jpeg = File.ReadAllBytes(staticImagePath);
            using var staticLogger = new LoggerConfiguration().WriteTo.Sink(new JsonConsoleSink()).CreateLogger();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(staticPlan.DurationSeconds));
            var staticTarget = new D92HidRenderTarget(staticLogger);
            staticTarget.RunAsync(staticPlan.SerialNumber, _ => Task.FromResult(jpeg), TimeSpan.FromSeconds(1), deadline.Token).GetAwaiter().GetResult();
            return 0;
        }
        if (args is ["--snapshot", var planPath, var imagePath])
        {
            var snapshot = JsonSerializer.Deserialize<Plan>(File.ReadAllText(planPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var clock = new ClockScene();
            clock.SetCanvasSize(snapshot.LogicalWidth, snapshot.LogicalHeight);
            clock.UpdateTime(DateTimeOffset.Now);
            using var jpegRenderer = new SceneJpegRenderer();
            File.WriteAllBytes(imagePath, jpegRenderer.Render(clock, snapshot.RotationDegrees, snapshot.JpegQuality));
            Console.WriteLine(JsonSerializer.Serialize(new { result = "snapshot_saved", imagePath, hardwareAccess = false }));
            return 0;
        }
        if (args is ["--list"])
        {
            Console.WriteLine(JsonSerializer.Serialize(D92HidDevice.Enumerate()));
            return 0;
        }
        var themeMode = args is ["--theme", _, _];
        if (args.Length != 1 && !themeMode) { Console.Error.WriteLine("Usage: DeskDisplay.UsbDemo <plan.json> | --theme <plan.json> <source.theme.json> | --list"); return 2; }
        var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(themeMode ? args[1] : args[0]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (plan.DurationSeconds is < 10 or > 1800 || plan.LogicalWidth is < 1 or > 8192 || plan.LogicalHeight is < 1 or > 8192)
            throw new ArgumentException("Invalid experiment duration or canvas dimensions");
        using var logger = new LoggerConfiguration().WriteTo.Sink(new JsonConsoleSink()).CreateLogger();
        var app = new Application();
        using var cancellation = new CancellationTokenSource();
        Scene scene;
        if (themeMode)
        {
            var project = JsonSerializer.Deserialize<ThemeProject>(File.ReadAllText(args[2])) ?? throw new InvalidDataException("Theme source is empty"); project.Validate();
            var theme = new ThemeScene(project);
            foreach (var layer in project.ActiveLayout.Layers.Where(l => l.Visible))
            {
                if (layer.IsVideo || layer.Kind is ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock) throw new NotSupportedException("Diagnostic accepts source text, shapes and local still images only");
                if (layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper) theme.SetChild(layer.Id, new ImageSceneLoader().LoadAsync(layer.Path, layer.Fill).GetAwaiter().GetResult());
            }
            scene = theme;
        }
        else scene = new ClockScene();
        scene.SetCanvasSize(plan.LogicalWidth, plan.LogicalHeight);
        using var provider = scene is ClockScene providerClock ? new ClockSceneDataProvider(providerClock, logger) : null;
        var themeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; themeTimer.Tick += (_, _) => { if (scene is ThemeScene theme) theme.Tick(DateTimeOffset.Now); };
        using var renderer = new SceneJpegRenderer();
        var model = new PreviewModel(scene);
        var preview = new SceneView();
        preview.SetBinding(SceneView.SceneProperty, new Binding(nameof(PreviewModel.Scene)));
        var status = new TextBlock { Margin = new Thickness(16), FontSize = 16 };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(PreviewModel.Status)));
        var stack = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        stack.Children.Add(status);
        stack.Children.Add(preview);
        var window = new Window { Title = "DeskDisplay · USB 共享场景测试", Width = 1000, Height = 380,
            Content = stack, DataContext = model };
        var target = new D92HidRenderTarget(logger);
        target.StateChanged += state =>
        {
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "state", time = DateTimeOffset.Now, state.Status, state.Frames, state.Connections, state.Error }));
            _ = window.Dispatcher.InvokeAsync(() => model.Update(state));
        };
        provider?.Start(); if (scene is ThemeScene) themeTimer.Start();
        Task? output = null;
        var finished = false;
        var closing = false;
        var exitCode = 0;
        var renderedFrames = 0;
        var lifetime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(plan.DurationSeconds) };
        lifetime.Tick += (_, _) => { lifetime.Stop(); window.Close(); };
        window.Loaded += (_, _) =>
        {
            output = Task.Run(() => target.RunAsync(plan.SerialNumber,
                token => window.Dispatcher.InvokeAsync(() =>
                {
                    var jpeg = renderer.Render(scene, plan.RotationDegrees, plan.JpegQuality);
                    if (++renderedFrames <= 3)
                    {
                        using var stream = new MemoryStream(jpeg); var image = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
                        Console.WriteLine(JsonSerializer.Serialize(new { kind = "frameMetadata", time = DateTimeOffset.Now, renderedFrames, jpegBytes = jpeg.Length, width = image.PixelWidth, height = image.PixelHeight, designOrientation = (scene as ThemeScene)?.Project.Orientation.ToString(), rotation = plan.RotationDegrees }));
                    }
                    return jpeg;
                }, DispatcherPriority.Background, token).Task,
                TimeSpan.FromSeconds(1), cancellation.Token));
            _ = output.ContinueWith(_ => window.Dispatcher.InvokeAsync(() => { if (!closing) window.Close(); }), TaskScheduler.Default);
            lifetime.Start();
        };
        window.Closing += async (_, e) =>
        {
            if (finished) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            lifetime.Stop();
            cancellation.Cancel();
            try { if (output is not null) await output; }
            catch (Exception exception) { logger.Error(exception, "USB demonstration failed"); exitCode = 1; }
            provider?.Dispose(); themeTimer.Stop(); if (scene is ThemeScene disposable) disposable.Dispose();
            renderer.Dispose();
            finished = true;
            window.Close();
        };
        app.Run(window);
        return exitCode;
    }

    private sealed class PreviewModel(Scene scene) : INotifyPropertyChanged
    {
        public Scene Scene { get; } = scene;
        public string Status { get; private set; } = "正在连接 USB 屏幕…";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Update(D92OutputState state)
        {
            var label = state.Status switch
            {
                "Streaming" => "正在显示", "Connected" => "设备已连接", "WaitingForDevice" => "设备已断开，等待自动重连",
                "Stopped" => "已停止", "BlockedByCraft" => "请先退出 Craft", _ => "输出错误"
            };
            Status = $"{label} · 已发送 {state.Frames} 帧 · 连接次数 {state.Connections}";
            PropertyChanged?.Invoke(this, new(nameof(Status)));
        }
    }

    private sealed class JsonConsoleSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => Console.WriteLine(JsonSerializer.Serialize(new { kind = "log", time = logEvent.Timestamp,
            level = logEvent.Level.ToString(), messageTemplate = logEvent.MessageTemplate.Text,
            properties = logEvent.Properties.ToDictionary(p => p.Key, p => p.Value.ToString()), error = logEvent.Exception?.Message }));
    }
}
