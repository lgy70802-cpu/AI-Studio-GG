namespace DeskDisplay.Core.Models;

/// <summary>A snapshot of an active display. Bounds are desktop pixels, not WPF DIPs.</summary>
public sealed record DisplayInfo(
    string DeviceName,
    string FriendlyName,
    string? DeviceId,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary);
