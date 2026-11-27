using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using DeskDisplay.Core.Interfaces;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace DeskDisplay.Display.Services;

/// <summary>Continuous native playback with pull-based GPU readback. No managed decoded-frame queue.</summary>
public sealed class WindowsLiveVideoFrameSource : IBufferedLiveVideoFrameSource
{
    private readonly MediaPlayer _player = RentPlayer();
    // MediaPlayer is Agile / ThreadingModel.Both. Retain a bounded set of empty,
    // paused players between sessions instead of recreating their native queues
    // at every playlist change. Frame buffers and MediaSource are never cached.
    // https://learn.microsoft.com/uwp/api/windows.media.playback.mediaplayer
    private const int MaximumIdlePlayers = 4;
    private static readonly object PlayerGate = new();
    private static readonly Stack<MediaPlayer> IdlePlayers = new();
    private static bool PlayerPoolClosing;
    static WindowsLiveVideoFrameSource() => AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseIdlePlayers();
    private static MediaPlayer RentPlayer()
    {
        lock (PlayerGate)
        {
            if (PlayerPoolClosing) throw new ObjectDisposedException("Native video player pool");
            if (IdlePlayers.TryPop(out var player)) return player;
        }
        return new() { IsVideoFrameServerEnabled = true, IsMuted = true, IsLoopingEnabled = true, AutoPlay = false };
    }
    private static void CloseIdlePlayers()
    {
        MediaPlayer[] players;
        lock (PlayerGate) { PlayerPoolClosing = true; players = IdlePlayers.ToArray(); IdlePlayers.Clear(); }
        foreach (var player in players)
            try { player.Dispose(); }
            catch (Exception e) { Trace.TraceWarning("Native video shutdown: {0}", e.Message); }
    }
    private void ReturnPlayer(bool reusable)
    {
        lock (PlayerGate)
        {
            if (reusable && !PlayerPoolClosing && IdlePlayers.Count < MaximumIdlePlayers) { IdlePlayers.Push(_player); return; }
        }
        _player.Dispose();
    }
    private readonly SemaphoreSlim _available = new(0, 1);
    private readonly object _signal = new();
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MediaSource? _media;
    private VideoFrame? _surface;
    private VideoFrame? _readback;
    private Exception? _failure;
    private bool _disposed;
    private int _width, _height;
    private TimeSpan? _lastReadPosition;
    private long _completedLoops;
    public long CompletedLoops => Interlocked.Read(ref _completedLoops);
    public bool CaptureDiagnostics { get; set; }
    public VideoNativeReadTiming? LastReadTiming { get; private set; }
    public TimeSpan Duration { get; private set; }
    public TimeSpan Position { get { ObjectDisposedException.ThrowIf(_disposed, this); return _player.PlaybackSession.Position; } }
    public int BufferLength => checked(_width*_height*4);
    public void SetPaused(bool paused) { ObjectDisposedException.ThrowIf(_disposed, this); if (paused) _player.Pause(); else _player.Play(); }

    private WindowsLiveVideoFrameSource()
    {
        _player.MediaOpened += Opened; _player.MediaFailed += Failed; _player.VideoFrameAvailable += Available;
    }
    public static async Task<WindowsLiveVideoFrameSource> OpenAsync(string path, int maxWidth, int maxHeight, CancellationToken token)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("视频不存在。", path);
        if (!new[] { ".mp4", ".m4v", ".mov", ".webm", ".mkv" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("不支持的视频格式。");
        if (maxWidth is < 1 or > 8192 || maxHeight is < 1 or > 8192 || (long)maxWidth * maxHeight > 16_777_216) throw new ArgumentOutOfRangeException(nameof(maxWidth));
        var result = new WindowsLiveVideoFrameSource();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(token);
            var info = await file.Properties.GetVideoPropertiesAsync().AsTask(token);
            result._media = MediaSource.CreateFromStorageFile(file); result._player.Source = result._media; result._player.Play();
            await result._opened.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
            var sourceWidth = info.Width == 0 ? result._player.PlaybackSession.NaturalVideoWidth : info.Width;
            var sourceHeight = info.Height == 0 ? result._player.PlaybackSession.NaturalVideoHeight : info.Height;
            var duration = info.Duration > TimeSpan.Zero ? info.Duration : result._player.PlaybackSession.NaturalDuration;
            if (sourceWidth == 0 || sourceHeight == 0) throw new InvalidDataException("视频尺寸无效。");
            var scale = Math.Min(1d, Math.Min(maxWidth / (double)sourceWidth, maxHeight / (double)sourceHeight));
            result._width = Math.Max(1, (int)Math.Floor(sourceWidth * scale)); result._height = Math.Max(1, (int)Math.Floor(sourceHeight * scale)); result.Duration = duration;
            result._surface = VideoFrame.CreateAsDirect3D11SurfaceBacked(DirectXPixelFormat.B8G8R8A8UIntNormalized, result._width, result._height);
            result._readback = new VideoFrame(BitmapPixelFormat.Bgra8,result._width,result._height,BitmapAlphaMode.Ignore);
            return result;
        }
        catch { await result.DisposeAsync(); throw; }
    }
    private void Opened(MediaPlayer sender, object args) => _opened.TrySetResult();
    private void Failed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _failure = new InvalidDataException("Windows 视频解码失败：" + args.ErrorMessage);
        _opened.TrySetException(_failure); Signal();
    }
    private void Available(MediaPlayer sender, object args) => Signal();
    private void Signal()
    {
        lock (_signal) { if (!_disposed && _available.CurrentCount == 0) _available.Release(); }
    }
    public async Task<VideoPixelFrame> ReadLatestAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);cancellationToken.ThrowIfCancellationRequested();
        var pixels=new byte[BufferLength];
        var frame=await ReadLatestIntoAsync(pixels,cancellationToken);
        return new(frame.Width,frame.Height,frame.Stride,frame.SamplePosition,pixels);
    }

    public async Task<VideoBufferFrame> ReadLatestIntoAsync(Memory<byte> destination,CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if(destination.Length<BufferLength||!MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)destination,out var segment)||segment.Array is null)
            throw new ArgumentException("Readback requires a sufficiently large array-backed destination.",nameof(destination));
        if (_failure is not null) throw new InvalidOperationException("视频帧源不可用。", _failure);
        // Reuse a newer coalesced event that arrived while encoding/sending, but never one
        // from before startup or a loop/seek backwards. No decoded frame queue is created.
        var looped = _lastReadPosition is not null && Position < _lastReadPosition;
        var stamp = CaptureDiagnostics ? Stopwatch.GetTimestamp() : 0;
        if (_lastReadPosition is null || looped)
            while (_available.Wait(0)) { }
        if (!await _available.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)) throw new TimeoutException("Windows 视频播放器未提供新画面。");
        var waitMs = CaptureDiagnostics ? Stopwatch.GetElapsedTime(stamp).TotalMilliseconds : 0;
        if (_failure is not null) throw new InvalidOperationException("视频帧源不可用。", _failure);
        cancellationToken.ThrowIfCancellationRequested();
        var position = Position;
        // A loop can occur while awaiting the next native event, not only before the wait.
        if (_lastReadPosition is { } previous && position < previous) Interlocked.Increment(ref _completedLoops);
        if(CaptureDiagnostics)stamp=Stopwatch.GetTimestamp();
        _player.CopyFrameToVideoSurface(_surface!.Direct3DSurface);
        var surfaceMs=CaptureDiagnostics?Stopwatch.GetElapsedTime(stamp).TotalMilliseconds:0;
        if(CaptureDiagnostics)stamp=Stopwatch.GetTimestamp();
        await _surface.CopyToAsync(_readback!).AsTask(cancellationToken);
        var bitmap = _readback!.SoftwareBitmap;
        var readbackMs=CaptureDiagnostics?Stopwatch.GetElapsedTime(stamp).TotalMilliseconds:0;
        if(CaptureDiagnostics)stamp=Stopwatch.GetTimestamp();
        var stride = checked(_width * 4); bitmap.CopyToBuffer(segment.Array.AsBuffer(segment.Offset,BufferLength));
        LastReadTiming=CaptureDiagnostics?new(waitMs,surfaceMs,readbackMs,Stopwatch.GetElapsedTime(stamp).TotalMilliseconds):null;
        _lastReadPosition = position;
        return new(_width, _height, stride, position, BufferLength);
    }
    public ValueTask DisposeAsync()
    {
        lock (_signal) { if (_disposed) return ValueTask.CompletedTask; _disposed = true; }
        _player.MediaOpened -= Opened; _player.MediaFailed -= Failed; _player.VideoFrameAvailable -= Available;
        var reusable = false;
        try { _player.Pause(); _player.Source = null; reusable = _failure == null; }
        finally
        {
            try { ReturnPlayer(reusable); }
            finally
            {
                IDisposable?[] resources = [_media, _surface, _readback, _available];
                _media = null; _surface = _readback = null;
                Exception? failure = null;
                foreach (var resource in resources)
                    try { resource?.Dispose(); } catch (Exception e) { failure ??= e; }
                if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
        return ValueTask.CompletedTask;
    }
}
