namespace DeskDisplay.Core.Interfaces;

/// <summary>Owned immutable BGRA8 bytes. SamplePosition is a sampled media-clock time, not an exact decoder PTS.</summary>
public sealed record VideoPixelFrame(int Width, int Height, int Stride, TimeSpan SamplePosition, ReadOnlyMemory<byte> Pixels);

public interface IVideoFrameSource : IAsyncDisposable
{
    TimeSpan Duration { get; }
    Task<VideoPixelFrame> ReadAsync(TimeSpan position, int maxWidth, int maxHeight, CancellationToken cancellationToken);
}

/// <summary>The owner serializes reads and waits for them to finish before disposal.</summary>
public interface ILiveVideoFrameSource : IAsyncDisposable
{
    TimeSpan Duration { get; }
    TimeSpan Position { get; }
    Task<VideoPixelFrame> ReadLatestAsync(CancellationToken cancellationToken);
}

/// <summary>Metadata for pixels written into caller-owned storage; contains no borrowed memory references.</summary>
public sealed record VideoBufferFrame(int Width, int Height, int Stride, TimeSpan SamplePosition, int ByteLength);

/// <summary>
/// Optional serialized readback into caller storage. The caller keeps the destination valid and exclusively
/// writable until the task completes. Implementations must not retain it after completion, including failure.
/// Existing immutable ReadLatestAsync callers retain their original ownership contract.
/// </summary>
public interface IBufferedLiveVideoFrameSource : ILiveVideoFrameSource
{
    int BufferLength { get; }
    Task<VideoBufferFrame> ReadLatestIntoAsync(Memory<byte> destination, CancellationToken cancellationToken);
}

public static class VideoFrameClock
{
    public static TimeSpan LoopPosition(TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        return TimeSpan.FromTicks(elapsed.Ticks % duration.Ticks);
    }
}
