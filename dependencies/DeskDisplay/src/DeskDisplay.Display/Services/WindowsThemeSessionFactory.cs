using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Display.Usb;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Themes;
using Serilog;

namespace DeskDisplay.Display.Services;

/// <summary>Creates isolated Scenes using the existing WPF renderer and HID sender. No device I/O during preparation.</summary>
public sealed class WindowsThemeSessionFactory(Dispatcher dispatcher, IDisplayManager displays, ILogger logger,
    Func<ThemeLayer, string>? assetPath = null, Func<ThemeLayer, Scene?>? pluginSource = null,
    Func<OutputSessionContext, ID92RenderTarget>? usbTargetFactory = null,
    Func<OutputSessionContext, ThemeScene, RenderWindowService>? windowFactory = null,
    Func<string, UsbVideoPreferences>? videoOptions = null,
    Action<string, VideoThroughput>? videoMetrics = null,
    Action<string, Guid>? videoCompleted = null,
    Action<string, Guid, System.Windows.Media.Imaging.BitmapSource>? videoPreview = null,
    Action<VideoFrameDiagnostic>? videoFrameDiagnostics = null)
{
    private readonly SessionImagePool _images = new();

    public async ValueTask<IOutputSessionDriver> CreateAsync(OutputSessionContext context, CancellationToken ct)
    {
        if (context.Profile.SurfaceSize.Width > 8192 || context.Profile.SurfaceSize.Height > 8192
            || (long)context.Profile.SurfaceSize.Width * context.Profile.SurfaceSize.Height > 16_777_216)
            throw new NotSupportedException("目标画布超出当前 WPF 输出预算。");
        if (context.Device.Transport == DeviceTransport.UsbHid && context.Profile.Capabilities.VerifiedFramesPerSecond is < 1)
            throw new NotSupportedException("目标刷新能力低于当前一秒帧周期，需要先验证更低频率的心跳调度。");
        if (context.Device.Transport == DeviceTransport.UsbHid && context.Device.ModelKey is not ("D92" or "D69"))
            throw new NotSupportedException("尚未验证此型号使用 D92 HID 协议。");
        if (context.Device.Transport == DeviceTransport.UsbHid && context.Device.ModelKey == "D69")
        {
            D69Compatibility.Validate(context.Profile);
            if (context.Theme.Layouts.Single(l => l.LayoutId == context.Layout.LayoutId).Layers.Any(l =>
                l.Visible && (l.CreateLegacyLayer().IsVideo || l.CreateLegacyLayer().Kind is ThemeElementKind.CameraClock or ThemeElementKind.StockPlugin)))
                throw new NotSupportedException("D69 实验版暂支持静态素材、数字时钟及基础图层，视频和复杂插件尚未验证。");
        }
        if (context.Device.Transport == DeviceTransport.SystemDisplay && (string.IsNullOrWhiteSpace(context.Device.Identity.SerialNumber)
            || context.Device.DeviceId != OutputDeviceKeys.WindowsDisplay(context.Device.Identity.SerialNumber)))
            throw new NotSupportedException("系统显示器标识无法唯一定位，请在 Windows 中确认扩展模式后刷新。");
        var prepared = await LoadSceneAsync(context.Theme, context.Layout, ct);
        if (context.Device.Transport == DeviceTransport.UsbHid)
        {
            var driver = new UsbDriver(context, prepared, dispatcher, logger, usbTargetFactory,
                (videoOptions?.Invoke(context.Device.Identity.SerialNumber!) ?? new()).Resolve(), videoMetrics, videoCompleted, videoPreview,videoFrameDiagnostics);
            try { await driver.PrepareVideoAsync(ct); return driver; }
            catch { await driver.DisposeAsync(); throw; }
        }
        if (context.Device.Transport == DeviceTransport.SystemDisplay) return new WindowDriver(context, prepared, dispatcher, displays, logger, windowFactory);
        await dispatcher.InvokeAsync(prepared.Dispose);
        throw new NotSupportedException("Unsupported production transport.");
    }

    public async Task<ThemeDevicePreview> CreatePreviewAsync(ThemeDocument theme, ResolvedLayout layout, CancellationToken ct)
    {
        var prepared = await LoadSceneAsync(theme, layout, ct);
        return new(prepared.Scene, prepared.Tick, async () => await dispatcher.InvokeAsync(prepared.Dispose));
    }

    private async Task<PreparedTheme> LoadSceneAsync(ThemeDocument theme, ResolvedLayout layout, CancellationToken ct)
    {
        theme.Validate();
        var prepared = await dispatcher.InvokeAsync(() => Prepare(theme, layout), DispatcherPriority.Background, ct);
        try
        {
            foreach (var layer in prepared.Scene.Project.ActiveLayout.Layers.Where(l => l.Visible && (l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper)))
            {
                var path = await dispatcher.InvokeAsync(() => assetPath?.Invoke(layer) ?? layer.Path, DispatcherPriority.Background, ct);
                if (layer.IsVideo)
                {
                    await dispatcher.InvokeAsync(async () =>
                    {
                        var player = new WindowsVideoPlaybackService(logger); prepared.Players.Add(player);
                        await player.OpenAsync(path, 30, ct); prepared.VideoPath = path;
                        prepared.Scene.SetChild(layer.Id, new VideoScene(path, player));
                    }, DispatcherPriority.Background, ct).Task.Unwrap();
                    continue;
                }
                var reference = await _images.AcquireAsync(path, ct).ConfigureAwait(false);
                prepared.Images.Add(reference);
                await dispatcher.InvokeAsync(() => prepared.Scene.SetChild(layer.Id, reference.Image), DispatcherPriority.Background, ct);
            }
            return prepared;
        }
        catch { await dispatcher.InvokeAsync(prepared.Dispose); throw; }
    }

    private PreparedTheme Prepare(ThemeDocument theme, ResolvedLayout resolved)
    {
        dispatcher.VerifyAccess();
        var source = theme.Layouts.Single(l => l.LayoutId == resolved.LayoutId);
        var layout = new ThemeLayout { Width = source.DesignSize.Width, Height = source.DesignSize.Height, Background = source.Background };
        foreach (var definition in source.Layers)
        {
            var layer = definition.CreateLegacyLayer();
            layer.X = definition.Bounds.X; layer.Y = definition.Bounds.Y;
            layer.Width = definition.Bounds.Width; layer.Height = definition.Bounds.Height;
            layer.Visible = definition.Visible; layer.Locked = definition.Locked; layer.ZIndex = definition.ZIndex;
            layer.Opacity = definition.Opacity; layer.Rotation = definition.Rotation;
            if (layer.IsVideo && layer.Kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper)) throw new NotSupportedException("视频只能作为图片或壁纸素材图层。");
            if (layer.Visible && (layer.Kind is ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock) && pluginSource?.Invoke(layer) is null)
                throw new NotSupportedException("主题所需插件未启用，尚未开始输出。");
            layout.Layers.Add(layer);
        }
        if (layout.Layers.Count(l => l.Visible && l.IsVideo) > 1) throw new NotSupportedException("每个布局目前支持一个可见视频图层。");
        var project = new ThemeProject { Id = theme.ThemeId, Name = theme.Name,
            Landscape = layout, Portrait = layout.Clone(), Orientation = ThemeOrientation.Landscape };
        var scene = new ThemeScene(project, resolved);
        var prepared = new PreparedTheme(scene, pluginSource);
        try { prepared.Tick(); return prepared; } catch { prepared.Dispose(); throw; }
    }

    private sealed class PreparedTheme(ThemeScene scene, Func<ThemeLayer, Scene?>? pluginSource) : IDisposable
    {
        public ThemeScene Scene { get; } = scene;
        public List<SessionImagePool.ImageReference> Images { get; } = [];
        public List<WindowsVideoPlaybackService> Players { get; } = [];
        public string? VideoPath { get; set; }
        public void Tick()
        {
            var now = DateTimeOffset.Now;
            Scene.Tick(now);
            foreach (var layer in Scene.Project.ActiveLayout.Layers.Where(l => l.Visible))
            {
                if (layer.Kind == ThemeElementKind.StockPlugin)
                {
                    if (pluginSource?.Invoke(layer) is not StockScene source) throw new NotSupportedException("股票插件当前不可用。");
                    var child = Scene.Children.GetValueOrDefault(layer.Id) as StockScene ?? new StockScene();
                    child.Update(source.Quotes.ToArray(), source.UpdatedAt ?? now); child.SetStatus(source.Status);
                    Scene.SetChild(layer.Id, child);
                }
                else if (layer.Kind == ThemeElementKind.CameraClock)
                {
                    if (pluginSource?.Invoke(layer) is not RetroCameraClockScene source) throw new NotSupportedException("相机时钟插件当前不可用。");
                    var child = Scene.Children.GetValueOrDefault(layer.Id) as RetroCameraClockScene ?? new RetroCameraClockScene();
                    child.Animate = source.Animate; child.Hour12 = source.Hour12; child.DurationMs = source.DurationMs; child.CropY = source.CropY;
                    child.Tick(now, Stopwatch.GetTimestamp()); Scene.SetChild(layer.Id, child);
                }
            }
        }
        public void Dispose() { foreach (var player in Players) player.Dispose(); Players.Clear(); Scene.Dispose(); foreach (var image in Images) image.Dispose(); Images.Clear(); }
    }

    private sealed class UsbDriver(OutputSessionContext context, PreparedTheme theme, Dispatcher dispatcher, ILogger logger,
        Func<OutputSessionContext, ID92RenderTarget>? targetFactory, UsbVideoPreferences preferences,
        Action<string, VideoThroughput>? videoMetrics, Action<string, Guid>? videoCompleted,
        Action<string, Guid, System.Windows.Media.Imaging.BitmapSource>? videoPreview,
        Action<VideoFrameDiagnostic>? videoFrameDiagnostics) : IOutputSessionDriver, IVideoOutputControl
    {
        private SceneJpegRenderer? _renderer;
        private VideoJpegFrameSource? _video;
        private VideoFramePipeline? _pipeline;
        private ThemeVideoComposition? _composition;
        public bool HasVideo => _video is not null;
        public bool BackgroundPaused => _video?.IsPaused == true;
        public Task SetBackgroundPausedAsync(bool paused) => _pipeline?.SetPausedAsync(paused) ?? throw new InvalidOperationException("当前会话没有视频背景。");
        public async Task PrepareVideoAsync(CancellationToken ct)
        {
            if (theme.VideoPath is not { } path) return;
            var composition = await dispatcher.InvokeAsync(() => new ThemeVideoComposition(theme.Scene, async token =>
                await dispatcher.InvokeAsync(() => { theme.Tick(); return ThemeVideoComposition.CaptureChildren(theme.Scene); }, DispatcherPriority.Background, token)));
            _composition = composition;
            _video = await VideoJpegFrameSource.OpenAsync(path, context.Profile.SurfaceSize.Width, context.Profile.SurfaceSize.Height,
                context.Profile.ContentToTransportRotation, quality: preferences.Resolve().JpegQuality, token: ct, backend: VideoFrameBackend.ContinuousFrameServer, composition: composition,captureDiagnostics:videoFrameDiagnostics is not null,encodingPreferences:preferences);
            var first = await _video.RenderAsync(ct);
            _pipeline = new(_video, preferences.FramesPerSecond > 10, new(first, _video.PreviewFrame!, _video.LastTiming!));
            await dispatcher.InvokeAsync(() => { foreach (var player in theme.Players) player.Dispose(); theme.Players.Clear(); });
        }
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken ct)
        {
            using var timer = new VideoTimerResolution(_video is not null && preferences.FramesPerSecond > 10);
            var target = targetFactory?.Invoke(context) ?? new D92HidRenderTarget(logger, context.Ownership, confirmedDevice: context.Device, experimentalHighFrameRate: _video is not null);
            var rate = new VideoRateController(preferences.FramesPerSecond, preferences.AutoReduceFrameRate);
            var rateClock = Stopwatch.StartNew(); var effective = preferences.FramesPerSecond; long lastDelivery = 0;
            var completedLoops = _video?.CompletedLoops ?? 0;
            var previewQueued = 0;
            var previewCadence = new VideoPreviewCadence();
            double sampledDecodeMs = 0, sampledEncodeMs = 0, sampledWriteMs = 0;
            long sampledFrames = 0, sampledBytes = 0;
            var diagnosticAt = rateClock.Elapsed;
            VideoSampleTiming? deliveredSample=null;
            var metered = _video is null ? null : target as D92HidRenderTarget;
            void Completed(D92FrameTiming frame)
            {
                if(videoFrameDiagnostics is not null&&deliveredSample is {} sample)
                {
                    var lookAhead=_pipeline?.LatestPreparedTiming;if(ReferenceEquals(lookAhead,sample))lookAhead=null;
                    videoFrameDiagnostics(new(context.Device.Identity.SerialNumber!,context.Theme.ThemeId,sample,lookAhead,_pipeline?.LookAheadPending==true,frame));
                }
                sampledWriteMs += frame.WriteMilliseconds;
                var snapshot = rate.Observe(rateClock.Elapsed.TotalSeconds, _video?.IsPaused == true);
                if (snapshot is null) return;
                if(deliveredSample is {} delivered)snapshot=snapshot with{JpegQuality=delivered.JpegQuality,JpegBytes=delivered.JpegBytes,QualityReason=delivered.QualityReason};
                effective = snapshot.EffectiveFps; videoMetrics?.Invoke(context.Device.Identity.SerialNumber!, snapshot);
                if (sampledFrames > 0 && rateClock.Elapsed - diagnosticAt >= TimeSpan.FromSeconds(5))
                {
                    logger.Information("Video output performance: {DeviceId}, actual {ActualFps}, limit {EffectiveFps}, decode {DecodeMs}ms, compose/encode {EncodeMs}ms, HID {WriteMs}ms, JPEG {JpegBytes}, cached foreground frames {CachedFrames}, paused {Paused}",
                        context.Device.DeviceId, snapshot.ActualFps, effective, sampledDecodeMs / sampledFrames, sampledEncodeMs / sampledFrames, sampledWriteMs / sampledFrames, sampledBytes / sampledFrames, _composition?.FastPathFrames ?? 0, BackgroundPaused);
                    sampledDecodeMs = sampledEncodeMs = sampledWriteMs = 0; sampledFrames = sampledBytes = 0; diagnosticAt = rateClock.Elapsed;
                }
            }
            if (metered is not null) metered.FrameCompleted += Completed;
            var failed = false;
            void Changed(D92OutputState state)
            {
                if (state.Status is "WaitingForDevice" or "Connected") rate.Reset();
                if (state.Status is "BlockedByCraft" or "Failed") failed = true;
                report(state.Status switch
                {
                    "WaitingForDevice" => OutputActualState.WaitingForDevice,
                    "Connected" => OutputActualState.Starting,
                    "Streaming" => OutputActualState.Streaming,
                    "BlockedByCraft" => OutputActualState.Blocked,
                    "Failed" => OutputActualState.Failed,
                    _ => OutputActualState.Stopped
                });
            }
            target.StateChanged += Changed;
            try
            {
                await target.RunAsync(context.Device.Identity.SerialNumber!, async token =>
                {
                    if (_pipeline is not null)
                    {
                        if (lastDelivery != 0 && effective < preferences.FramesPerSecond)
                        { var wait = TimeSpan.FromSeconds(1d / effective) - Stopwatch.GetElapsedTime(lastDelivery); if (wait > TimeSpan.Zero) await Task.Delay(wait, token); }
                        var frame = await _pipeline.ReadAsync(token); lastDelivery = Stopwatch.GetTimestamp();
                        deliveredSample=frame.Timing;
                        sampledFrames++; sampledDecodeMs += frame.Timing.DecodeMilliseconds; sampledEncodeMs += frame.Timing.EncodeMilliseconds; sampledBytes += frame.Bytes.Length;
                        if (videoPreview is not null && previewCadence.TryPublish(rateClock.Elapsed.TotalSeconds, frame.Preview) && Interlocked.Exchange(ref previewQueued, 1) == 0)
                            _ = dispatcher.InvokeAsync(() => { try { if (!ct.IsCancellationRequested) videoPreview(context.Device.Identity.SerialNumber!, context.Theme.ThemeId, frame.Preview); } finally { Interlocked.Exchange(ref previewQueued, 0); } }, DispatcherPriority.Background);
                        if (_video!.CompletedLoops > completedLoops) { completedLoops = _video.CompletedLoops; videoCompleted?.Invoke(context.Device.Identity.SerialNumber!, context.Theme.ThemeId); }
                        return frame.Bytes;
                    }
                    return await dispatcher.InvokeAsync(() =>
                    {
                        token.ThrowIfCancellationRequested(); theme.Tick();
                        _renderer ??= new SceneJpegRenderer();
                        return _renderer.Render(theme.Scene, context.Profile.ContentToTransportRotation,
                            context.Device.ModelKey == "D69" ? 85 : SceneJpegRenderer.DefaultQuality);
                    }, DispatcherPriority.Background, token);
                }, _video is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1d / preferences.FramesPerSecond), ct).ConfigureAwait(false);
                if (failed) throw new InvalidOperationException("USB 会话被阻止或发送失败，请查看设备日志。");
            }
            finally { target.StateChanged -= Changed; if (metered is not null) metered.FrameCompleted -= Completed; }
        }
        public async ValueTask DisposeAsync()
        {
            try { if (_pipeline is not null) await _pipeline.DisposeAsync(); }
            finally
            {
                try { if (_video is not null) await _video.DisposeAsync(); }
                finally { _composition = null; await dispatcher.InvokeAsync(() => { try { _renderer?.Dispose(); } finally { _renderer = null; theme.Dispose(); } }); }
            }
        }
    }

    private sealed class WindowDriver(OutputSessionContext context, PreparedTheme theme, Dispatcher dispatcher,
        IDisplayManager displays, ILogger logger, Func<OutputSessionContext, ThemeScene, RenderWindowService>? createWindow) : IOutputSessionDriver, IVideoOutputControl
    {
        private RenderWindowService? _window;
        public bool HasVideo => theme.VideoPath is not null;
        public bool BackgroundPaused => theme.Players.Count > 0 && theme.Players.All(p => p.IsPaused);
        public async Task SetBackgroundPausedAsync(bool paused) => await dispatcher.InvokeAsync(() => { foreach (var player in theme.Players) { if (paused) player.Pause(); else player.Resume(); } });
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken ct)
        {
            if (context.Profile.ContentToTransportRotation != 0) throw new NotSupportedException("系统显示器使用系统方向，不额外旋转输出。");
            while (!ct.IsCancellationRequested)
            {
                var snapshot = await displays.GetDisplaysAsync(ct).ConfigureAwait(false);
                var matches = snapshot.Where(d => !d.IsPrimary && !string.IsNullOrWhiteSpace(d.DeviceId)
                    && OutputDeviceKeys.WindowsDisplay(d.DeviceId) == context.Device.DeviceId).ToArray();
                if (matches.Length > 1 || matches.Length == 1 && snapshot.Count(d => d.DeviceName == matches[0].DeviceName) > 1)
                    throw new InvalidOperationException("显示器身份或镜像输出存在歧义，请重新确认目标。");
                var target = matches.SingleOrDefault();
                await dispatcher.InvokeAsync(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    _window ??= createWindow?.Invoke(context, theme.Scene) ?? new RenderWindowService(logger, theme.Scene, context.Ownership);
                    if (target is null) { _window.Close(); report(OutputActualState.WaitingForDevice); return; }
                    if (target.Width != context.Profile.SurfaceSize.Width || target.Height != context.Profile.SurfaceSize.Height)
                        throw new InvalidOperationException("显示器分辨率已变化，请重新适配后应用。");
                    theme.Tick(); _window.ShowScene(target, theme.Scene);
                    if (!_window.IsOpen) throw new InvalidOperationException(_window.Status);
                    report(OutputActualState.Streaming);
                }, DispatcherPriority.Background, ct);
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }
        public async ValueTask DisposeAsync() => await dispatcher.InvokeAsync(() =>
        {
            try { _window?.Dispose(); } finally { _window = null; theme.Dispose(); }
        });
    }
}
