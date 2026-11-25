using System.Runtime.InteropServices;
using DeskDisplay.Core.Models;
using LibreHardwareMonitor.Hardware;
using Serilog;

namespace DeskDisplay.Sensors;

internal sealed class LibreHardwareSensorReader : ISensorReader
{
    private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true };
    private readonly ILogger _logger;
    private readonly HashSet<string> _faults = [];
    private ulong _idle, _kernel, _user;
    private bool _hasTimes;
    public LibreHardwareSensorReader(ILogger logger)
    {
        _logger = logger;
        try { _computer.Open(); }
        catch (Exception exception) { logger.Warning(exception, "Some hardware sensors could not initialize; using available devices and Windows fallbacks"); }
    }
    public SensorSnapshot Read()
    {
        var readings = new List<(HardwareType Type, string Id, string Name, SensorType Kind, double Value)>();
        foreach (var hardware in _computer.Hardware) ReadHardware(hardware, readings);
        double? Select(HardwareType type, SensorType kind, params string[] names)
        {
            foreach (var name in names)
            {
                var values = readings.Where(r => r.Type == type && r.Kind == kind && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(r => r.Value).ToArray();
                if (values.Length > 0) return values.Average();
            }
            return null;
        }
        var cpuLoadFallback = ReadCpuLoad();
        var gpu = readings.Where(r => r.Type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .OrderBy(r => r.Type == HardwareType.GpuIntel ? 1 : 0).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        double? GpuValue(SensorType kind, params string[] names)
        {
            foreach (var name in names)
            {
                var value = readings.Where(r => r.Id == gpu.Id && r.Kind == kind && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(r => (double?)r.Value).FirstOrDefault();
                if (value.HasValue) return value;
            }
            return null;
        }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        double? memoryFallback = GlobalMemoryStatusEx(ref memory) ? memory.MemoryLoad : null;
        return new SensorSnapshot
        {
            CpuLoad = Select(HardwareType.Cpu, SensorType.Load, "CPU Total") ?? cpuLoadFallback,
            CpuTemperature = Select(HardwareType.Cpu, SensorType.Temperature, "CPU Package", "Core (Tctl/Tdie)", "Core (Tdie)", "Core Average", "Core Max"),
            GpuLoad = GpuValue(SensorType.Load, "GPU Core", "D3D 3D"),
            GpuTemperature = GpuValue(SensorType.Temperature, "GPU Core"),
            MemoryLoad = Select(HardwareType.Memory, SensorType.Load, "Memory") ?? memoryFallback
        };
    }
    private void ReadHardware(IHardware hardware, List<(HardwareType Type, string Id, string Name, SensorType Kind, double Value)> readings)
    {
        var id = hardware.Identifier.ToString();
        try
        {
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                try
                {
                    if (sensor.Value is not { } value || !float.IsFinite(value)) continue;
                    if (sensor.SensorType == SensorType.Load && value is < 0 or > 100) continue;
                    if (sensor.SensorType == SensorType.Temperature && value is < -50 or > 200) continue;
                    readings.Add((hardware.HardwareType, id, sensor.Name, sensor.SensorType, value));
                }
                catch (Exception exception) { LogOnce(sensor.Identifier.ToString(), exception); }
            }
            _faults.Remove(id);
        }
        catch (Exception exception) { LogOnce(id, exception); }
        foreach (var child in hardware.SubHardware) ReadHardware(child, readings);
    }
    private void LogOnce(string id, Exception exception)
    {
        if (_faults.Add(id)) _logger.Warning(exception, "Hardware sensor unavailable: {SensorId}", id);
    }
    private double? ReadCpuLoad()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        double? value = null;
        if (_hasTimes && kernel >= _kernel && user >= _user && idle >= _idle)
        {
            var total = kernel - _kernel + user - _user;
            if (total > 0) value = Math.Clamp(100d * (1d - (double)(idle - _idle) / total), 0, 100);
        }
        _idle = idle; _kernel = kernel; _user = user; _hasTimes = true;
        return value;
    }
    public void Dispose() => _computer.Close();
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
}
