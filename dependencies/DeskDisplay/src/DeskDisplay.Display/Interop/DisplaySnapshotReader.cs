using System.ComponentModel;
using System.Runtime.InteropServices;
using DeskDisplay.Core.Models;
using Serilog;
using static DeskDisplay.Display.Interop.NativeMethods;

namespace DeskDisplay.Display.Interop;

internal sealed class DisplaySnapshotReader(ILogger logger)
{
    public IReadOnlyList<DisplayInfo> Read(CancellationToken cancellationToken)
    {
        // Keep native bounds in pixels even if this library is called from another DPI context.
        // The scope is synchronous; restore on this same worker thread before returning it to the pool.
        var previousContext = SetThreadDpiAwarenessContext(new nint(-4)); // PER_MONITOR_AWARE_V2
        if (previousContext == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var monitors = ReadMonitors();
            try
            {
                return ReadActivePaths(monitors, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                logger.Warning(exception, "DisplayConfig unavailable; using GDI monitor enumeration");
                return monitors.SelectMany(ReadFallbackDevices).ToArray();
            }
        }
        finally
        {
            if (SetThreadDpiAwarenessContext(previousContext) == 0)
                logger.Error(new Win32Exception(Marshal.GetLastWin32Error()), "Failed to restore thread DPI context");
        }
    }

    private static List<MonitorInfo> ReadMonitors()
    {
        var monitors = new List<MonitorInfo>();
        Exception? callbackException = null;
        bool Callback(nint handle, nint hdc, ref Rect bounds, nint data)
        {
            try
            {
                var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(handle, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                monitors.Add(info);
                return true;
            }
            catch (Exception exception)
            {
                // Never allow a managed exception to unwind through a native callback.
                callbackException = exception;
                return false;
            }
        }

        var success = EnumDisplayMonitors(0, 0, Callback, 0);
        if (callbackException is not null) throw new InvalidOperationException("Monitor changed during enumeration", callbackException);
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
        return monitors;
    }

    private IReadOnlyList<DisplayInfo> ReadActivePaths(List<MonitorInfo> monitors, CancellationToken token)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            Check(GetDisplayConfigBufferSizes(OnlyActivePaths, out var pathCount, out var modeCount));
            var paths = new PathInfo[Math.Max(pathCount, 1)];
            var modes = new ModeInfo[Math.Max(modeCount, 1)];
            var result = QueryDisplayConfig(OnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (result == InsufficientBuffer) continue; // A hot-plug can invalidate the buffer sizes.
            Check(result);
            var displays = new List<DisplayInfo>();
            for (var index = 0; index < pathCount; index++)
            {
                token.ThrowIfCancellationRequested();
                var path = paths[index];
                var source = new SourceDeviceName { Header = Header<SourceDeviceName>(1, path.Source.AdapterId, path.Source.Id) };
                Check(GetSourceDeviceName(ref source));
                var monitor = monitors.FirstOrDefault(item => string.Equals(item.DeviceName, source.Name, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(monitor.DeviceName))
                    throw new InvalidOperationException("Display topology changed between native snapshots.");

                var target = new TargetDeviceName { Header = Header<TargetDeviceName>(2, path.Target.AdapterId, path.Target.Id) };
                var nameResult = GetTargetDeviceName(ref target);
                if (nameResult != 0)
                    logger.Warning(new Win32Exception(nameResult), "Cannot read target metadata for {DeviceName} target {TargetId}", source.Name, path.Target.Id);

                var bounds = monitor.Monitor;
                if (path.Source.ModeInfoIndex < modeCount)
                {
                    var mode = modes[path.Source.ModeInfoIndex];
                    if (mode.InfoType == 1 && mode.Width > 0 && mode.Height > 0)
                        bounds = new Rect { Left = mode.X, Top = mode.Y, Right = checked(mode.X + (int)mode.Width), Bottom = checked(mode.Y + (int)mode.Height) };
                }
                // Do not attach a different clone target's interface path if CCD metadata is missing.
                var friendlyName = string.IsNullOrWhiteSpace(target.FriendlyName) ? source.Name : target.FriendlyName;
                var deviceId = string.IsNullOrWhiteSpace(target.DevicePath) ? null : target.DevicePath;
                displays.Add(Create(source.Name, friendlyName, deviceId, bounds, (monitor.Flags & 1) != 0));
            }

            // An empty or mixed snapshot during a transition is retried by the caller via change events.
            if (displays.Count == 0 && monitors.Count > 0)
                throw new InvalidOperationException("Active paths are temporarily unavailable.");
            return displays.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        throw new Win32Exception(InsufficientBuffer, "Display topology kept changing during enumeration.");
    }

    private static IEnumerable<DisplayInfo> ReadFallbackDevices(MonitorInfo monitor)
    {
        var found = false;
        for (uint index = 0; ; index++)
        {
            var device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            // EDD_GET_DEVICE_INTERFACE_NAME supplies an interface path rather than a display ordinal.
            if (!EnumDisplayDevices(monitor.DeviceName, index, ref device, 1)) break;
            if ((device.StateFlags & 1) == 0) continue; // DISPLAY_DEVICE_ACTIVE
            found = true;
            yield return Create(monitor.DeviceName,
                string.IsNullOrWhiteSpace(device.DeviceString) ? monitor.DeviceName : device.DeviceString,
                string.IsNullOrWhiteSpace(device.DeviceId) ? null : device.DeviceId,
                monitor.Monitor, (monitor.Flags & 1) != 0);
        }
        if (!found) yield return Create(monitor.DeviceName, monitor.DeviceName, null, monitor.Monitor, (monitor.Flags & 1) != 0);
    }

    private static DisplayInfo Create(string name, string friendly, string? id, Rect bounds, bool primary) =>
        new(name, friendly, id, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, primary);

    private static DeviceInfoHeader Header<T>(uint type, Luid adapterId, uint id) =>
        new() { Type = type, Size = (uint)Marshal.SizeOf<T>(), AdapterId = adapterId, Id = id };

    private static void Check(int result)
    {
        if (result != 0) throw new Win32Exception(result);
    }
}
