namespace DeskDisplay.Core.Models;

public sealed record UsbOutputSettings
{
    public string SerialNumber { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public int RotationDegrees { get; init; }
}
