using System.IO;
using System.Windows;
using System.Windows.Media;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using DeskDisplay.Core.Interfaces;
using Serilog;

namespace DeskDisplay.Display.Services;

/// <summary>One native WPF player; FPS conversion happens once, never as per-frame CPU readback.</summary>
public sealed class WindowsVideoPlaybackService(ILogger logger) : IVideoPlaybackService, IVideoTransport
{
    private MediaPlayer? _player;
    private string? _temporaryPath;
    private CancellationTokenSource? _operation;
    private bool _disposed;
    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    public MediaPlayer? Player => _player;
    public string Status { get; private set; } = "请选择本地 MP4/H.264 视频。";
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public TimeSpan Position => _player?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _player?.NaturalDuration is { HasTimeSpan: true } duration ? duration.TimeSpan : TimeSpan.Zero;
    public void Pause() => Control(() => { _player?.Pause(); IsPaused = _player is not null; SetStatus("视频已暂停。"); });
    public void Resume() => Control(() => { _player?.Play(); IsPaused = false; SetStatus($"静音循环播放 · {FramesPerSecond} FPS"); });
    public void Seek(TimeSpan position) => Control(() => { if (_player is not null && Duration > TimeSpan.Zero) _player.Position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Duration.TotalSeconds)); });
    private void Control(Action action)
    {
        _dispatcher.VerifyAccess();
        if (_disposed || _player is null) return;
        try { action(); }
        catch (Exception exception) { Fail(exception); }
    }
    public int FramesPerSecond { get; private set; } = 30;
    public event EventHandler? StateChanged;
    internal string? TemporaryPath => _temporaryPath;
    internal int LoopCount { get; private set; }

    public async Task OpenAsync(string path, int framesPerSecond, CancellationToken cancellationToken = default)
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (framesPerSecond is not (30 or 60)) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        Stop();
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operation = operation;
        string? pendingPath = null;
        try
        {
            FramesPerSecond = framesPerSecond;
            LoopCount = 0;
            if (!string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("当前仅支持本地 MP4/H.264 视频。");
            if (!File.Exists(path)) throw new FileNotFoundException("视频不存在或已被删除。", path);
            SetStatus($"正在准备 {framesPerSecond} FPS 视频，可切换场景取消…");
            var source = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(operation.Token);
            var properties = await source.Properties.GetVideoPropertiesAsync().AsTask(operation.Token);
            if (properties.Width == 0 || properties.Height == 0) throw new InvalidDataException("文件不包含可播放视频。");
            var folder = Path.Combine(Path.GetTempPath(), "DeskDisplay", "Video");
            Directory.CreateDirectory(folder);
            pendingPath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".mp4");
            var storageFolder = await StorageFolder.GetFolderFromPathAsync(folder).AsTask(operation.Token);
            var destination = await storageFolder.CreateFileAsync(Path.GetFileName(pendingPath)).AsTask(operation.Token);
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            profile.Audio = null; // No audio track, in addition to muting the playback device.
            var scale = Math.Min(1d, 1920d / Math.Max(properties.Width, properties.Height));
            profile.Video.Width = Math.Max(2u, (uint)(properties.Width * scale) / 2 * 2);
            profile.Video.Height = Math.Max(2u, (uint)(properties.Height * scale) / 2 * 2);
            profile.Video.FrameRate.Numerator = (uint)framesPerSecond;
            profile.Video.FrameRate.Denominator = 1;
            profile.Video.Bitrate = framesPerSecond == 60 ? 12_000_000u : 8_000_000u;
            var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true, AlwaysReencode = true };
            var prepared = await transcoder.PrepareFileTranscodeAsync(source, destination, profile).AsTask(operation.Token);
            if (!prepared.CanTranscode) throw new InvalidDataException($"Windows 无法准备此视频（{prepared.FailureReason}），请选择 MP4/H.264 文件。");
            await prepared.TranscodeAsync().AsTask(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            _temporaryPath = pendingPath;
            pendingPath = null;
            _player = new MediaPlayer { IsMuted = true, Volume = 0 };
            _player.MediaOpened += OnOpened;
            _player.MediaEnded += OnEnded;
            _player.MediaFailed += OnFailed;
            _player.Open(new Uri(_temporaryPath));
            _player.Play();
            SetStatus("正在打开视频…");
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.Error(exception, "Video preparation failed for {VideoPath} at {FPS} FPS", path, framesPerSecond);
            if (!ReferenceEquals(_operation, operation)) return;
            ReleasePlayer();
            SetStatus(exception is FileNotFoundException or InvalidDataException ? exception.Message : "视频准备失败，请检查文件、磁盘空间及 Windows 媒体组件。可重新选择视频。");
        }
        finally
        {
            DeleteTemporary(pendingPath);
            if (ReferenceEquals(_operation, operation)) _operation = null;
            operation.Dispose();
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _player)) return;
        try
        {
            if (!_player!.HasVideo) throw new InvalidDataException("文件没有视频画面。");
            IsPlaying = true;
            SetStatus($"静音循环播放 · {FramesPerSecond} FPS");
        }
        catch (Exception exception) { Fail(exception); }
    }
    private void OnEnded(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _player)) return;
        try { _player!.Position = TimeSpan.Zero; _player.Play(); LoopCount++; }
        catch (Exception exception) { Fail(exception); }
    }
    private void OnFailed(object? sender, ExceptionEventArgs e)
    {
        if (ReferenceEquals(sender, _player)) Fail(e.ErrorException);
    }
    private void Fail(Exception exception)
    {
        logger.Error(exception, "Shared video playback failed");
        ReleasePlayer();
        SetStatus("视频播放失败，已释放播放资源。请重新选择 MP4/H.264 视频。");
    }
    public void Stop()
    {
        _dispatcher.VerifyAccess();
        _operation?.Cancel();
        ReleasePlayer();
        SetStatus("视频已停止。");
    }
    private void ReleasePlayer()
    {
        var player = _player;
        _player = null;
        IsPlaying = false;
        IsPaused = false;
        if (player is not null)
        {
            player.MediaOpened -= OnOpened;
            player.MediaEnded -= OnEnded;
            player.MediaFailed -= OnFailed;
            try { player.Stop(); player.Close(); }
            catch (Exception exception) { logger.Error(exception, "Failed to close native video player"); }
        }
        DeleteTemporary(_temporaryPath);
        _temporaryPath = null;
    }
    private void DeleteTemporary(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); }
        catch (Exception exception) { logger.Warning(exception, "Cannot delete temporary video {Path}", path); }
    }
    private void SetStatus(string status)
    {
        Status = status;
        try { StateChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception) { logger.Error(exception, "Video state subscriber failed"); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        StateChanged = null;
    }
}
