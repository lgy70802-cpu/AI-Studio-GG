using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;

namespace DeskDisplay.App.ViewModels;

public sealed partial class UsbOutputViewModel
{
    public const int VideoFramesPerSecond = 30;
    public IReadOnlyList<int> VideoFpsOptions { get; } = [5, 10, 20, 24, 30];
    public IReadOnlyList<int> VideoQualityOptions { get; } = [70, 80, 90];
    public IReadOnlyList<string> VideoModeOptions {get;}=["自定义","画质优先","流畅优先"];
    private VideoPlaybackMode _videoMode;
    public string VideoMode {get=>VideoModeOptions[(int)_videoMode];set {var index=VideoModeOptions.ToList().IndexOf(value);if(CanEdit&&index>=0&&SetProperty(ref _videoMode,(VideoPlaybackMode)index)){OnPropertyChanged(nameof(IsCustomVideoMode));OnPropertyChanged(nameof(VideoModeSummary));SaveVideoPreferences();}}}
    public bool IsCustomVideoMode=>_videoMode==VideoPlaybackMode.Custom;
    public string VideoModeSummary=>_videoMode switch{VideoPlaybackMode.Quality=>"目标24 FPS · 固定质量90 · 不自动降帧；下次播放生效",VideoPlaybackMode.Smooth=>"目标30 FPS · 质量60–90 · 字节预算调节，持续超载再降帧；下次播放生效",_=>"使用下方自定义参数；更改下次播放生效"};
    private int _videoJpegQuality = 90;
    public int VideoJpegQuality { get => _videoJpegQuality; set { if (CanEdit && value is >= 30 and <= 95 && SetProperty(ref _videoJpegQuality, value)) SaveVideoPreferences(); } }
    private int _videoFps = VideoFramesPerSecond;
    private Dictionary<string, UsbVideoPreferences> _videoPreferences = new(StringComparer.Ordinal);
    private Task _videoPreferenceSave = Task.CompletedTask;
    private bool _autoReduceVideoFps;
    public int VideoFps { get => _videoFps; set { if (CanEdit && VideoFpsOptions.Contains(value) && SetProperty(ref _videoFps, value)) SaveVideoPreferences(); } }
    public bool AutoReduceVideoFps { get => _autoReduceVideoFps; set { if (CanEdit && SetProperty(ref _autoReduceVideoFps, value)) SaveVideoPreferences(); } }
    public Task VideoPreferencesPending => _videoPreferenceSave;
    public UsbVideoPreferences GetVideoPreferences(string serial) => _videoPreferences.GetValueOrDefault(serial, new()).Resolve();
    private void LoadVideoPreferences()
    {
        var value = _selected is null ? new UsbVideoPreferences() : _videoPreferences.GetValueOrDefault(_selected,new()).Normalize();
        _videoMode=value.Mode;OnPropertyChanged(nameof(VideoMode));OnPropertyChanged(nameof(IsCustomVideoMode));OnPropertyChanged(nameof(VideoModeSummary));
        _videoFps = value.FramesPerSecond; _autoReduceVideoFps = value.AutoReduceFrameRate;
        _videoJpegQuality = value.JpegQuality; OnPropertyChanged(nameof(VideoJpegQuality));
        OnPropertyChanged(nameof(VideoFps)); OnPropertyChanged(nameof(AutoReduceVideoFps));
    }
    private void SaveVideoPreferences()
    {
        if (string.IsNullOrWhiteSpace(_selected)) return;
        var serial = _selected; var preference = new UsbVideoPreferences(VideoFps, AutoReduceVideoFps, VideoJpegQuality){Mode=_videoMode};
        _videoPreferences[serial] = preference;
        _videoPreferenceSave = PersistVideoPreferencesAsync(_videoPreferenceSave, serial, preference);
    }
    private async Task PersistVideoPreferencesAsync(Task previous, string serial, UsbVideoPreferences preference)
    {
        try { await previous; if (!await _settings.UpdateAsync(s => { s.UsbVideoPreferences ??= new(); s.UsbVideoPreferences[serial] = preference; })) throw new IOException("视频偏好未保存。"); }
        catch (Exception error) { _logger.Warning(error,"Video preferences save failed"); Status = "视频设置保存失败：" + error.Message; }
    }
    private string _videoPerformanceText = "尚未播放视频";
    public string VideoPerformanceText { get => _videoPerformanceText; private set => SetProperty(ref _videoPerformanceText, value); }
    private volatile int _effectiveVideoFps = 30;
    private long _lastVideoDelivery;
    private VideoFramePipeline? _videoPipeline;
    private VideoJpegFrameSource? _video;
    private VideoSampleTiming? _deliveredVideoTiming;
    private UsbVideoPreferences? _activeVideoPreferences;
    private static string DescribeVideo(VideoThroughput measured)=>$"实际发送 {measured.ActualFps:F1} FPS · 目标 {measured.RequestedFps} · 当前上限 {measured.EffectiveFps}"+(measured.JpegQuality>0?$" · JPEG Q{measured.JpegQuality} / {measured.JpegBytes/1000d:F0}KB · {measured.QualityReason}":"")+(measured.EffectiveFps<measured.RequestedFps?" · 持续吞吐不足，已降档":"");
    private CancellationTokenSource? _videoPreparing;
    private Task _videoOperation = Task.CompletedTask;
    private TaskCompletionSource<bool>? _videoReady;
    private int _previewQueued;
    private AsyncCommand? _videoPauseCommand;
    public bool IsVideoRunning => IsRunning && Scene is VideoFrameScene && _video is not null;
    public bool HasSelectedOutput => IsRunning || _main.Devices.Managed?.Cards.Any(c => c.IsActive && c.Device.Identity.SerialNumber == SelectedSerial) == true;
    private async Task ApplyVideoThemeAsync(ThemeScene theme)
    {
        if (_main.Devices.Managed is not { } managed) { Status = "设备会话服务尚未就绪。"; return; }
        var cards = managed.Cards.Where(c => c.Connected && c.Device.Identity.SerialNumber == SelectedSerial).ToArray();
        if (cards.Length != 1) { Status = "请连接并确认唯一目标设备。"; return; }
        var card = cards[0]; var revision = card.AcceptedOutputRevision;
        await managed.ApplySourceAsync(theme.Project.Clone(), [card.DeviceId], DeskDisplay.Core.Themes.LayoutAdaptation.Contain, true);
        Status = card.AcceptedOutputRevision != revision ? "视频主题正在输出，实际速度取决于图层和设备吞吐。" : card.Message;
    }
    public void ReportVideoThroughput(string serial, VideoThroughput measured)
    {
        _ = _dispatcher.InvokeAsync(() =>
        {
            if (serial == SelectedSerial || _main.Devices.Managed?.HomeOutputCard?.Device.Identity.SerialNumber == serial) VideoPerformanceText = DescribeVideo(measured);
        });
    }
    public bool IsVideoPaused => _video?.IsPaused == true;
    public string VideoPauseLabel => _video?.IsPaused == true ? "继续视频" : "暂停视频";
    public async Task SetVideoPausedAsync(bool paused)
    { if (_videoPipeline is { } pipeline) { await pipeline.SetPausedAsync(paused); NotifyVideo(); } }
    public AsyncCommand VideoPauseCommand => _videoPauseCommand ??= new(async () =>
    {
        if (_video is not { } video) return;
        await SetVideoPausedAsync(!video.IsPaused);
    }, () => IsVideoRunning && !_busy && !_closing, _logger);

    public Task<bool> PlayVideoAsync(string path, bool preserveCarousel = false)
    {
        if(!CanApply || _main.Output.IsBusy) { Status="正在处理其他输出操作，请稍后重试。";return Task.FromResult(false); }
        var request = _main.Output.RunAsync("使用视频", () => PlayVideoCoreAsync(path, preserveCarousel));
        _videoOperation = request; return request;
    }
    internal Task<bool> RestoreVideoAsync(string path)
    {
        var request=_main.Output.RunAsync("恢复视频",()=>PlayVideoCoreAsync(path));_videoOperation=request;return request;
    }
    private async Task<bool> PlayVideoCoreAsync(string path, bool preserveCarousel = false)
    {
        if (!CanApply || !TryGetSettings(out var config)) return false;
        var intent = _intent;
        var previous = _running ? Scene : null;
        _busy = true; using var prepare = new CancellationTokenSource(); _videoPreparing = prepare; Notify();
        VideoJpegFrameSource? prepared = null;
        CancellationTokenSource? started = null;
        Func<Task>? restoreManaged = null; var releasedManaged = false;
        try
        {
            Status = "正在准备视频首帧，当前输出保持运行…";
            if (!File.Exists(path)) throw new FileNotFoundException("视频文件不存在。", path);
            var matches = await Task.Run(() => _enumerate().Count(d => d.SerialNumber == config.SerialNumber));
            if (matches != 1) throw new InvalidOperationException("请连接唯一匹配的 D92，目标设备不会自动改选。");
            var preferences=GetVideoPreferences(config.SerialNumber);
            prepared = await VideoJpegFrameSource.OpenAsync(path, config.Width, config.Height, config.RotationDegrees, quality: preferences.JpegQuality, token: prepare.Token, backend: VideoFrameBackend.ContinuousFrameServer,encodingPreferences:preferences);
            var first = await prepared.RenderAsync(prepare.Token);
            if (_closing || intent != _intent) return false;
            if (!await _settings.UpdateAsync(s => s.UsbOutput = config)) throw new IOException("设备设置保存失败。");
            if (!preserveCarousel) _main.Library?.StopRotation();
            _main.Creator?.StopThemePlaylist();
            restoreManaged = _main.Devices.Managed?.CaptureManagedRestore(config.SerialNumber);
            if (ReleaseManagedOutputAsync is { } release) { await release(config.SerialNumber); releasedManaged = true; }
            if (_closing || intent != _intent) return false;
            if (_session is { } old) { old.Cancel(); await _run; }
            if (_closing || intent != _intent) return false;
            _main.Devices.CloseRender();
            var scene = new VideoFrameScene(Path.GetFullPath(path)); scene.SetCanvasSize(config.Width, config.Height);
            var frame = prepared.PreviewFrame!; scene.Update(new WpfSceneImage(frame, frame.PixelWidth, frame.PixelHeight));
            _activeVideoPreferences=preferences;_deliveredVideoTiming=prepared.LastTiming;
            _active = config; _outputScene = scene; _video = prepared; prepared = null;
            _session = started = new(); _videoReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _running = true; IsWaitingForDevice = false; _main.UseSharedHomeScene(); Notify();
            _run = RunSessionAsync(_session, config, _video, first);
            if (await _videoReady.Task.WaitAsync(TimeSpan.FromSeconds(6), prepare.Token))
            { Status = $"视频正在循环播放 · 目标 {preferences.FramesPerSecond} FPS · 静音"; _main.Output.Report(Status); return true; }
            throw new IOException("视频首帧未能发送。");
        }
        catch (OperationCanceledException) when (prepare.IsCancellationRequested) { Status = "视频准备已取消。"; return false; }
        catch (Exception error)
        {
            _logger.Warning(error, "USB video application failed");
            if (started is not null && ReferenceEquals(_session, started)) { started.Cancel(); await _run; }
            var reason = "视频应用失败：" + error.Message;
            if (releasedManaged && restoreManaged is not null && intent == _intent && !_closing && !_running)
            {
                try { _busy = false; await restoreManaged(); reason += "；已恢复原设备主题。"; }
                catch (Exception restoreError) { _logger.Warning(restoreError,"Managed video replacement rollback failed"); reason += "；原主题恢复失败：" + restoreError.Message; }
            }
            if (previous is not null and not VideoFrameScene && intent == _intent && !_closing && !_running)
            {
                _busy = false; await StartSceneAsync(previous); reason += "；已尝试恢复原内容。";
            }
            Status = reason; _main.Output.Report(Status, true); return false;
        }
        finally
        {
            if (prepared is not null) await prepared.DisposeAsync();
            if (ReferenceEquals(_videoPreparing, prepare)) _videoPreparing = null;
            _busy = false; Notify();
        }
    }
    // Only one pending UI notification; HID rendering never waits for the home preview.
    private async Task<byte[]> RenderVideoAsync(VideoFramePipeline pipeline, CancellationTokenSource session, CancellationToken token)
    {
        if (_lastVideoDelivery != 0 && _effectiveVideoFps < (_activeVideoPreferences?.FramesPerSecond??VideoFps))
        {
            var wait = TimeSpan.FromSeconds(1d / _effectiveVideoFps) - Stopwatch.GetElapsedTime(_lastVideoDelivery);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, token).ConfigureAwait(false);
        }
        var encoded = await pipeline.ReadAsync(token).ConfigureAwait(false);
        _deliveredVideoTiming=encoded.Timing;
        _lastVideoDelivery = Stopwatch.GetTimestamp();
        if (Interlocked.Exchange(ref _previewQueued, 1) == 0)
            _ = _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (!_disposed && ReferenceEquals(_session, session) && !session.IsCancellationRequested && _outputScene is VideoFrameScene scene)
                        scene.Update(new WpfSceneImage(encoded.Preview, encoded.Preview.PixelWidth, encoded.Preview.PixelHeight));
                }
                finally { Interlocked.Exchange(ref _previewQueued, 0); }
            }, DispatcherPriority.Background);
        return encoded.Bytes;
    }
    private void NotifyVideo()
    {
        OnPropertyChanged(nameof(IsVideoRunning)); OnPropertyChanged(nameof(IsVideoPaused)); OnPropertyChanged(nameof(VideoPauseLabel));
        _videoPauseCommand?.NotifyCanExecuteChanged(); _main.RefreshNowPlaying();
    }
}
