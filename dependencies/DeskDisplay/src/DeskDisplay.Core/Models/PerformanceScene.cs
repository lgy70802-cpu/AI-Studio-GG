namespace DeskDisplay.Core.Models;

public sealed class PerformanceScene : Scene
{
    public SensorSnapshot Values { get; private set; } = new();
    public bool IsSettled(SensorSnapshot target)
    {
        static bool Near(double? current, double? next) => current is null && next is null
            || current.HasValue && next.HasValue && Math.Abs(current.Value - next.Value) < 0.01;
        return Near(Values.CpuLoad, target.CpuLoad) && Near(Values.CpuTemperature, target.CpuTemperature)
            && Near(Values.GpuLoad, target.GpuLoad) && Near(Values.GpuTemperature, target.GpuTemperature)
            && Near(Values.MemoryLoad, target.MemoryLoad);
    }
    public void Update(SensorSnapshot target, TimeSpan elapsed)
    {
        var alpha = 1 - Math.Exp(-Math.Max(0, elapsed.TotalSeconds) / 0.35);
        double? Smooth(double? previous, double? value)
        {
            if (value is null || !double.IsFinite(value.Value)) return null;
            return previous is null ? value : previous + alpha * (value - previous);
        }
        Values = new SensorSnapshot
        {
            CpuLoad = Smooth(Values.CpuLoad, target.CpuLoad),
            CpuTemperature = Smooth(Values.CpuTemperature, target.CpuTemperature),
            GpuLoad = Smooth(Values.GpuLoad, target.GpuLoad),
            GpuTemperature = Smooth(Values.GpuTemperature, target.GpuTemperature),
            MemoryLoad = Smooth(Values.MemoryLoad, target.MemoryLoad)
        };
        OnPropertyChanged(nameof(Values));
    }
}
