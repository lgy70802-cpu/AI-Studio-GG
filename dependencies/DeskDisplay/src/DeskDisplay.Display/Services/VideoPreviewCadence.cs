namespace DeskDisplay.Display.Services;

/// <summary>Bounds UI bitmap uploads independently of encoding and device delivery.</summary>
internal sealed class VideoPreviewCadence
{
    private double _last = double.NegativeInfinity;
    private object? _lastFrame;

    public bool TryPublish(double elapsedSeconds, object frame)
    {
        if (ReferenceEquals(frame, _lastFrame) || elapsedSeconds - _last < 1d / 15) return false;
        _last = elapsedSeconds;
        _lastFrame = frame;
        return true;
    }
}
