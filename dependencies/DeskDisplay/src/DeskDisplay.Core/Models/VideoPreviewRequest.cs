namespace DeskDisplay.Core.Models;

/// <summary>Preview demand is independent from device output cadence and dimensions.</summary>
public sealed record VideoPreviewRequest(bool Enabled, int MaximumWidth, int MaximumHeight, int FramesPerSecond)
{
    public static VideoPreviewRequest Disabled { get; } = new(false, 320, 180, 1);
    public static VideoPreviewRequest LegacySnapshot { get; } = new(true, 8192, 8192, 1);
    public void Validate()
    {
        if (MaximumWidth is < 1 or > 8192 || MaximumHeight is < 1 or > 8192 || FramesPerSecond is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(MaximumWidth));
    }
}
