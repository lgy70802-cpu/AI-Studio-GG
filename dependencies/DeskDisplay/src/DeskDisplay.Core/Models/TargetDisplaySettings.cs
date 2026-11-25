namespace DeskDisplay.Core.Models;

/// <summary>Persisted identity and last observed desktop resolution; position is intentionally not an identity.</summary>
public sealed record TargetDisplaySettings
{
    public string? DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public int LastWidth { get; init; }
    public int LastHeight { get; init; }
}
