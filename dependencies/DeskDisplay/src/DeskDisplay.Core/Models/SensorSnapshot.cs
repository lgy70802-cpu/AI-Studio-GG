namespace DeskDisplay.Core.Models;

public sealed class SensorSnapshot
{
    public double? CpuLoad { get; init; }
    public double? CpuTemperature { get; init; }
    public double? GpuLoad { get; init; }
    public double? GpuTemperature { get; init; }
    public double? MemoryLoad { get; init; }
}
