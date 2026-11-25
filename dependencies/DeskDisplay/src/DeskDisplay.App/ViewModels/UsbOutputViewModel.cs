using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.App.ViewModels;

/// <summary>Owns one USB session; all Scene access and JPEG rendering stay on the UI dispatcher.</summary>
public sealed partial class UsbOutputViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly ISettingsService _settings;
    private readonly ILogger _logger;
    private readonly ID92RenderTarget _target;
    private readonly Func<IReadOnlyList<D92DeviceInfo>> _enumerate;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private CancellationTokenSource? _session;
    private Task _run = Task.CompletedTask, _operation = Task.CompletedTask;
    private bool _running, _busy, _closing, _disposed;
    private string? _selected;
    private string _width, _height, _status = "选择设备并填写画布尺寸。开始前请退出 Craft。";
    private int _rotation;
    private HashSet<string> _connectedSerials = new(StringComparer.Ordinal);
    private UsbOutputSettings? _active;
    private Scene? _outputScene;
    private long _intent;
    public ObservableCollection<string> Devices { get; } = [];
    public IReadOnlyList<int> Rotations { get; } = [0, 90, 180, 270];
    public string? SelectedSerial { get => _selected; set { if (CanEdit && SetProperty(ref _selected, value)) LoadVideoPreferences(); } }
    public string Width { get => _width; set { if (CanEdit) SetProperty(ref _width, value); } }
    public string Height { get => _height; set { if (CanEdit) SetProperty(ref _height, value); } }
    public int Rotation { get => _rotation; set { if (CanEdit) SetProperty(ref _rotation, value); } }
    public bool IsRunning => _running;
    public bool IsSelectedConnected => !string.IsNullOrWhiteSpace(_selected) && _connectedSerials.Contains(_selected);
    private bool _waitingForDevice;
    public bool IsWaitingForDevice { get => _waitingForDevice; private set => SetProperty(ref _waitingForDevice, value); }
    public bool CanApply => !_busy && !_closing && !_disposed && (_session?.IsCancellationRequested != true);
    public bool CanEdit => !_running && !_busy && !_closing;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public Scene Scene => _outputScene ?? _main.Scene;
    public Func<string,Task>? ReleaseManagedOutputAsync { get; set; }
    public Func<string,Scene?,Task>? ContentAccepted {get;set;}
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand StartCommand { get; }
    public AsyncCommand StopCommand { get; }

    public UsbOutputViewModel(MainViewModel main, ISettingsService settings, AppSettings initial, ILogger logger,
        ID92RenderTarget? target = null, Func<IReadOnlyList<D92DeviceInfo>>? enumerate = null)
    {
        _main = main; _settings = settings; _logger = logger;
        _target = target ?? new D92HidRenderTarget(logger, experimentalHighFrameRate: true);
        _enumerate = enumerate ?? D92HidDevice.Enumerate;
        var saved = initial.UsbOutput;
        _selected = saved?.SerialNumber;
        _videoPreferences = initial.UsbVideoPreferences?.Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null).ToDictionary(p => p.Key, p => p.Value.Normalize(), StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        LoadVideoPreferences();
        _width = saved?.Width > 0 ? saved.Width.ToString() : string.Empty;
        _height = saved?.Height > 0 ? saved.Height.ToString() : string.Empty;
        _rotation = saved?.RotationDegrees ?? 0;
        if (!string.IsNullOrWhiteSpace(_selected)) Devices.Add(_selected);
        RefreshCommand = new(RefreshAsync, () => CanEdit, logger);
        StartCommand = new(StartAsync, () => CanEdit, logger);
        StopCommand = new(StopAsync, () => (_running || _videoPreparing is not null) && !_closing, logger);
        main.PropertyChanged += OnMainChanged;
    }

    public Task RefreshAsync() => BeginOperation(async () =>
    {
        try
        {
            var found = await Task.Run(_enumerate);
            if (_closing) return;
            var selected = _selected;
            _connectedSerials = found.Select(device => device.SerialNumber).ToHashSet(StringComparer.Ordinal);
            Devices.Clear();
            foreach (var serial in found.Select(d => d.SerialNumber).Distinct()) Devices.Add(serial);
            // Preserve saved identity while unplugged; never switch automatically to another screen.
            if (!string.IsNullOrWhiteSpace(selected) && !Devices.Contains(selected)) Devices.Add(selected);
            _selected = selected;
            OnPropertyChanged(nameof(SelectedSerial));
            OnPropertyChanged(nameof(IsSelectedConnected));
            Status = found.Count == 0 ? "未发现已连接的 D92；已保存的设备选择仍保留。" : $"发现 {found.Count} 台 D92，请选择目标设备。";
        }
        catch (Exception ex) { _logger.Error(ex, "USB device refresh failed"); Status = "设备刷新失败，请重试或查看日志。"; }
    });

    public Task StartAsync() => _main.Scene is VideoScene or ThemeScene {ContainsVideo:true} ? ApplySceneAsync(_main.Scene) : StartSceneAsync(_main.Scene);
    private Task StartSceneAsync(Scene desired) => BeginOperation(()=>_main.Output.RunAsync("连接 D92",async () =>
    {
        var intent = _intent;
        if (!TryGetSettings(out var config)) {_main.Output.Report(Status,true);return;}
        if (!Supported(desired)) { Status = "USB 目前支持时钟、图片、性能、组合和股票场景，请先选择可用内容。";_main.Output.Report(Status,true); return; }
        try
        {
            if (!await _settings.UpdateAsync(s => s.UsbOutput = config))
            { Status = "USB 设置保存失败，尚未开始输出。"; _main.Output.Report(Status,true);return; }
            if (_closing || intent != _intent) return;
            if(ReleaseManagedOutputAsync is { } release)await release(config.SerialNumber);
            if (_closing || intent != _intent) return;
            _main.Devices.CloseRender();
            _active = config;
            _outputScene=desired;
            _session = new();
            IsWaitingForDevice = false;
            _running = true;
            _main.UseSharedHomeScene();
            _main.UpdatePreview();
            Notify();
            Status = "正在连接 USB 屏幕…";
            _run = RunSessionAsync(_session, config);
        }
        catch (Exception ex) { _logger.Error(ex, "USB output start failed"); Status = "USB 输出启动失败："+ex.Message;_main.Output.Report(Status,true);RegisterRetry(config.SerialNumber,desired); }
    }));

    public bool ValidateConfiguration() => TryGetSettings(out _);
    public Task ApplyCurrentAsync()=>ApplySceneAsync(_main.Scene);
    public Task ApplySceneAsync(Scene scene)=>scene is VideoScene video ? PlayVideoAsync(video.Path) : scene is ThemeScene {ContainsVideo:true} theme ? ApplyVideoThemeAsync(theme) : _main.Output.RunAsync("应用 D92 内容",()=>ApplySceneCoreAsync(scene));
    private async Task ApplySceneCoreAsync(Scene desired)
    {
        if (!CanApply) return;
        if (!Supported(desired)) { Status = "D92 USB 暂不支持包含视频的组合主题或当前场景；独立视频请从资源库使用。"; _main.Output.Report(Status,true);return; }
        if (_running && Scene is VideoFrameScene && _session is { } oldVideo) { oldVideo.Cancel(); await _run; }
        if (_running) { _outputScene=desired;OnPropertyChanged(nameof(Scene));ApplyCanvas();_main.UseSharedHomeScene();Status="USB 输出运行中 · "+WorkspaceViewModel.Name(Scene)+" · 已请求切换";_main.Output.Report(Status);if(ContentAccepted is { } accepted)await accepted(_active!.SerialNumber,Scene);return; }
        await StartSceneAsync(desired);
    }

    private bool TryGetSettings(out UsbOutputSettings config)
    {
        config = new();
        if (string.IsNullOrWhiteSpace(_selected)) { Status = "请先选择 USB 设备。"; return false; }
        if (!int.TryParse(_width, out var w) || !int.TryParse(_height, out var h)
            || w is < 1 or > 8192 || h is < 1 or > 8192 || (long)w * h > 16_777_216)
        { Status = "宽、高须为 1–8192 的整数，总像素不超过 16777216。"; return false; }
        if (!Rotations.Contains(_rotation)) { Status = "请选择 0、90、180 或 270 度旋转。"; return false; }
        config = new() { SerialNumber = _selected, Width = w, Height = h, RotationDegrees = _rotation };
        return true;
    }

    private async Task RunSessionAsync(CancellationTokenSource session, UsbOutputSettings config, DeskDisplay.Display.Services.VideoJpegFrameSource? video = null, byte[]? firstVideoFrame = null)
    {
        using var renderer = new SceneJpegRenderer();
        DeskDisplay.Display.Services.VideoTimerResolution? timerResolution = null;
        DeskDisplay.Display.Services.VideoFramePipeline? pipeline = null;
        var videoPreferences=video is null?new UsbVideoPreferences(VideoFps,AutoReduceVideoFps):_activeVideoPreferences??GetVideoPreferences(SelectedSerial??"");
        var rate = new VideoRateController(videoPreferences.FramesPerSecond, videoPreferences.AutoReduceFrameRate);
        var rateClock = System.Diagnostics.Stopwatch.StartNew();
        _effectiveVideoFps = videoPreferences.FramesPerSecond; _lastVideoDelivery = 0;
        var metered = video is null ? null : _target as D92HidRenderTarget;
        void FrameCompleted(D92FrameTiming frame)
        {
            var measured = rate.Observe(rateClock.Elapsed.TotalSeconds, video?.IsPaused == true);
            if (measured is null) return;
            if(_deliveredVideoTiming is {} timing)measured=measured with {JpegQuality=timing.JpegQuality,JpegBytes=timing.JpegBytes,QualityReason=timing.QualityReason};
            _effectiveVideoFps = measured.EffectiveFps;
            _ = _dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_session, session) && !session.IsCancellationRequested)
                    VideoPerformanceText = DescribeVideo(measured);
            });
        }
        if (metered is not null) metered.FrameCompleted += FrameCompleted;
        // Explicit, process-local diagnostic opt-in. Never capture user images by default.
        var captureDirectory = Environment.GetEnvironmentVariable("DESKDISPLAY_USB_CAPTURE_DIRECTORY");
        var capturePrefix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var captured = 0;
        ThemeOutputGeometry? reportedGeometry = null;
        // Session identity prevents queued status from changing a newer session.
        void StateChanged(D92OutputState state) => _ = _dispatcher.InvokeAsync(() =>
        {
            if (_disposed || !ReferenceEquals(_session, session) || session.IsCancellationRequested) return;
            IsWaitingForDevice = state.Status == "WaitingForDevice";
            if (state.Status is "WaitingForDevice" or "Connected") rate.Reset();
            Status = state.Status switch
            {
                "Connected" => "已连接，正在发送画面…",
                "Streaming" => $"USB 正在发送{WorkspaceViewModel.Name(Scene)} · 已发送 {state.Frames} 帧 · 连接 {state.Connections} 次",
                "WaitingForDevice" => "等待所选 USB 设备；重连后会自动恢复。",
                "BlockedByCraft" => "检测到 Craft 正在运行，USB 输出已停止。请退出 Craft 后重新开始。",
                "Failed" => OutputTroubleshooting.DescribeFailure("USB 输出",state.Error),
                _ => "USB 输出已停止；屏幕可能保留最后一帧。"
            };
            _main.Output.Report(Status,state.Status is "Failed" or "BlockedByCraft");
            if(state.Status=="Streaming") { _main.Output.SetRetry(null); if(state.Frames==1)_videoReady?.TrySetResult(true); }
            if(state.Status=="Streaming" && state.Frames==1 && ContentAccepted is { } accepted)_ = RecordAcceptedAsync(accepted,config.SerialNumber,Scene);
        }, DispatcherPriority.Normal);
        _target.StateChanged += StateChanged;
        try
        {
            timerResolution = new(video is not null && videoPreferences.FramesPerSecond > 10);
            pipeline = video is null ? null : new(video, videoPreferences.FramesPerSecond > 10,
                firstVideoFrame is null ? null : new(firstVideoFrame, video.PreviewFrame!, video.LastTiming!));
            _videoPipeline = pipeline;
            await Task.Run(() => _target.RunAsync(config.SerialNumber,
                ct => pipeline is not null ? RenderVideoAsync(pipeline, session, ct) : _dispatcher.InvokeAsync(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    ApplyCanvas();
                    var geometry = ResolveGeometry(config);
                    if (reportedGeometry != geometry)
                    {
                        reportedGeometry = geometry;
                        _logger.Information("USB effective canvas {Width}x{Height}, rotation {Rotation}; saved configuration {SavedWidth}x{SavedHeight}, rotation {SavedRotation}", geometry.Width, geometry.Height, geometry.Rotation, config.Width, config.Height, config.RotationDegrees);
                    }
                    var jpeg = renderer.Render(Scene, geometry.Rotation);
                    if (!string.IsNullOrWhiteSpace(captureDirectory) && captured < 3)
                    {
                        captured++;
                        try
                        {
                            Directory.CreateDirectory(captureDirectory);
                            var path = Path.Combine(captureDirectory, $"{capturePrefix}-{captured}.jpg");
                            File.WriteAllBytes(path, jpeg);
                            _logger.Information("USB diagnostic frame saved {Path}; Bytes={Bytes} Canvas={Width}x{Height} Rotation={Rotation}",
                                path, jpeg.Length, Scene.PixelWidth, Scene.PixelHeight, config.RotationDegrees);
                        }
                        catch (Exception ex) { _logger.Warning(ex, "Cannot save opted-in USB diagnostic frame"); }
                    }
                    return jpeg;
                }, DispatcherPriority.Background, ct).Task,
                video is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1d / videoPreferences.FramesPerSecond), session.Token));
            await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        catch (Exception ex) { _logger.Error(ex, "USB scene output failed"); Status = "USB 输出失败："+ex.Message;_main.Output.Report(Status,true);if(Scene is VideoFrameScene failedVideo){var path=failedVideo.Path;_main.Output.SetRetry(async()=>{if(SelectedSerial!=config.SerialNumber)throw new InvalidOperationException("目标设备已改变，请重新应用。");await RestoreVideoAsync(path);});}else RegisterRetry(config.SerialNumber,Scene); }
        finally
        {
            _videoReady?.TrySetResult(false);
            if (metered is not null) metered.FrameCompleted -= FrameCompleted;
            if(pipeline is not null)try { await pipeline.DisposeAsync(); } catch(Exception cleanupError) { _logger.Warning(cleanupError,"Video pipeline cleanup failed"); }
            if(ReferenceEquals(_videoPipeline,pipeline))_videoPipeline=null;
            if(video is not null)try { await video.DisposeAsync(); } catch(Exception cleanupError) { _logger.Warning(cleanupError,"Video decoder cleanup failed"); }
            timerResolution?.Dispose();
            if(ReferenceEquals(_video,video))_video=null;
            _target.StateChanged -= StateChanged;
            _running = false; _active = null; _session = null;_outputScene=null;
            IsWaitingForDevice = false;
            session.Dispose();
            _main.UpdatePreview();
            Notify();
        }
    }

    public void RequestStop()
    {
        ++_intent;
        _videoPreparing?.Cancel();
        _session?.Cancel();
        if (_running) Status = "USB 输出已停止；屏幕可能保留最后一帧。";
        OnPropertyChanged(nameof(CanApply));
    }
    public bool OwnsOutputForSerial(string serial) => _running && _active?.SerialNumber==serial && _session is not null;
    public async Task<bool> StopOwnedSerialAsync(string serial)
    {
        if(!OwnsOutputForSerial(serial))return false;
        var running=_run;
        RequestStop();
        // Await only the captured run. Never cancel a newer session after this one completes.
        await running;
        return true;
    }
    public async Task StopAsync() { var serial=_active?.SerialNumber;RequestStop(); await _operation;await _videoOperation; RequestStop(); await _run;if(!_closing && serial is not null && ContentAccepted is { } accepted)await RecordAcceptedAsync(accepted,serial,null);_main.Output.Report(Status); }
    private async Task RecordAcceptedAsync(Func<string,Scene?,Task> accepted,string serial,Scene? scene)
    {try{await accepted(serial,scene);}catch(Exception ex){_logger.Warning(ex,"Output accepted but history could not be recorded");}}
    private void RegisterRetry(string serial,Scene scene)=>_main.Output.SetRetry(async()=>
    {
        if(SelectedSerial!=serial)throw new InvalidOperationException("目标设备已改变，请重新选择内容并应用。");
        await ApplySceneAsync(scene);
    });
    public async Task PrepareForCloseAsync() { _closing = true; Notify(); await StopAsync(); await _videoPreferenceSave; }
    public bool ApplyCanvas()
    {
        if (!_running || _active is null) return false;
        var geometry = ResolveGeometry(_active);
        Scene.SetCanvasSize(geometry.Width, geometry.Height);
        return ReferenceEquals(Scene,_main.Scene);
    }
    private ThemeOutputGeometry ResolveGeometry(UsbOutputSettings config) => Scene is ThemeScene theme
        ? ThemeOutputGeometry.Resolve(config.Width, config.Height, config.RotationDegrees, theme.Project.Orientation)
        : new(config.Width, config.Height, config.RotationDegrees);
    private static bool Supported(Scene scene) => scene is ThemeScene theme ? !theme.ContainsVideo : scene is ClockScene or ImageScene or PerformanceScene or DashboardScene or StockScene or RetroCameraClockScene;
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Scene)) return;
        if(!_running)OnPropertyChanged(nameof(Scene));
    }
    private Task BeginOperation(Func<Task> action)
    {
        if (!CanEdit) return Task.CompletedTask;
        _busy = true; Notify();
        return _operation = ExecuteAsync(action);
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        finally { _busy = false; Notify(); }
    }
    private void Notify()
    {
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanApply));
        NotifyVideo();
        RefreshCommand.NotifyCanExecuteChanged(); StartCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _closing = true;
        RequestStop();
        _main.PropertyChanged -= OnMainChanged;
    }
}

