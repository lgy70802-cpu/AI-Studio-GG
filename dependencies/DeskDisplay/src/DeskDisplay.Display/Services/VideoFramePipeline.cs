using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace DeskDisplay.Display.Services;

public sealed record EncodedVideoFrame(byte[] Bytes, BitmapSource Preview, VideoSampleTiming Timing)
{ public long PreparedAt { get; } = Stopwatch.GetTimestamp(); }

/// <summary>Single consumer, at most one prepared frame. The caller owns the decoder.</summary>
public sealed class VideoFramePipeline(VideoJpegFrameSource source, bool prepareAhead = true, EncodedVideoFrame? first = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _life = new();
    private Task<EncodedVideoFrame>? _pending;
    private EncodedVideoFrame? _first = first;
    private VideoSampleTiming? _latestPrepared=first?.Timing;
    public VideoSampleTiming? LatestPreparedTiming=>Volatile.Read(ref _latestPrepared);
    public bool LookAheadPending=>Volatile.Read(ref _pending) is {IsCompleted:false};
    private bool _disposed;
    private Task? _dispose;
    private readonly object _sync = new();
    private async Task<EncodedVideoFrame> Prepare(CancellationToken token)
    {
        var bytes = await source.RenderAsync(token).ConfigureAwait(false);
        var timing=source.LastTiming!;Volatile.Write(ref _latestPrepared,timing);
        return new(bytes, source.PreviewFrame!, timing);
    }
    public async Task<EncodedVideoFrame> ReadAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _life.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var frame = _first;
            _first = null;
            if (frame is null)
            {
                var pending = _pending; _pending = null;
                frame = await (pending ?? Prepare(linked.Token)).ConfigureAwait(false);
                // A disconnected device must resume from current playback, not an old prepared frame.
                if (pending is not null && !source.IsPaused && Stopwatch.GetElapsedTime(frame.PreparedAt) > TimeSpan.FromMilliseconds(150))
                    frame = await Prepare(linked.Token).ConfigureAwait(false);
            }
            linked.Token.ThrowIfCancellationRequested();
            if (prepareAhead && !source.IsPaused) _pending = Prepare(_life.Token);
            return frame;
        }
        finally { _gate.Release(); }
    }
    public async Task SetPausedAsync(bool paused)
    {
        await _gate.WaitAsync(_life.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending is { } pending) { _pending = null; await pending.ConfigureAwait(false); }
            _first = null;
            await source.SetPausedAsync(paused).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public ValueTask DisposeAsync()
    { lock (_sync) return new(_dispose ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        _life.Cancel();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_pending is { } pending)
                try { await pending.ConfigureAwait(false); } catch (OperationCanceledException) when (_life.IsCancellationRequested) { }
            _pending = null; _first = null;Volatile.Write(ref _latestPrepared,null);
        }
        finally { _gate.Release(); _life.Dispose(); }
    }
}

/// <summary>Balanced process timer request, scoped to an active high-rate output session.</summary>
public sealed class VideoTimerResolution : IDisposable
{
    private bool _active;
    public VideoTimerResolution(bool enabled)
    { if (enabled) { if (timeBeginPeriod(1) != 0) throw new InvalidOperationException("Cannot enable video timer precision."); _active = true; } }
    public void Dispose() { if (_active) { _active = false; timeEndPeriod(1); } }
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
}
