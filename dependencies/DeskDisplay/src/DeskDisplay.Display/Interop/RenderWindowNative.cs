using System.ComponentModel;
using System.Runtime.InteropServices;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Display.Interop;

internal static class RenderWindowNative
{
    internal const int DpiChanged = 0x02E0;
    internal const int WindowPosChanging = 0x0046;
    internal const uint NoActivate = 0x0010;
    internal const uint NoOwnerZOrder = 0x0200;
    internal const uint NoMove = 0x0002;
    internal const uint NoSize = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPos
    {
        public nint Hwnd, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out NativeMethods.Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hwnd, out NativeMethods.Rect rect);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref int value, uint size);

    internal static NativeMethods.Rect ResolveActiveBounds(DisplayInfo target)
    {
        if (target.IsPrimary || target.Width <= 0 || target.Height <= 0)
            throw new InvalidOperationException("A connected non-primary display with valid bounds is required.");
        NativeMethods.MonitorInfo? found = null;
        Exception? callbackError = null;
        bool Callback(nint handle, nint hdc, ref NativeMethods.Rect bounds, nint data)
        {
            try
            {
                var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
                if (!NativeMethods.GetMonitorInfo(handle, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (string.Equals(info.DeviceName, target.DeviceName, StringComparison.OrdinalIgnoreCase)) found = info;
                return true;
            }
            catch (Exception exception) { callbackError = exception; return false; }
        }
        if (!NativeMethods.EnumDisplayMonitors(0, 0, Callback, 0))
            throw new InvalidOperationException("Cannot validate render target bounds", callbackError ?? new Win32Exception(Marshal.GetLastWin32Error()));
        if (found is not { } monitor || (monitor.Flags & 1) != 0)
            throw new InvalidOperationException("The selected secondary display is no longer available.");
        var rect = monitor.Monitor; // Full monitor area, deliberately not rcWork (taskbar-excluded area).
        if (rect.Left != target.X || rect.Top != target.Y || rect.Right - rect.Left != target.Width || rect.Bottom - rect.Top != target.Height)
            throw new InvalidOperationException("Display bounds changed since enumeration; refresh before opening.");
        return rect;
    }

    internal static void PlacePixels(nint hwnd, NativeMethods.Rect bounds)
    {
        if (!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(hwnd), new nint(-4)))
            throw new InvalidOperationException("RenderWindow requires Per-Monitor V2 DPI awareness.");
        if (!SetWindowPos(hwnd, new nint(-1), bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
                NoActivate | NoOwnerZOrder))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!GetWindowRect(hwnd, out var actual) || !GetClientRect(hwnd, out var client))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!actual.Equals(bounds) || client.Right - client.Left != bounds.Right - bounds.Left || client.Bottom - client.Top != bounds.Bottom - bounds.Top)
            throw new InvalidOperationException("Native window/client bounds do not exactly cover the target display.");
    }

    internal static void DisableRoundedCorners(nint hwnd, ILogger logger)
    {
        var doNotRound = 1; // DWMWCP_DONOTROUND; ignored by Windows versions without this DWM attribute.
        var result = DwmSetWindowAttribute(hwnd, 33, ref doNotRound, sizeof(int));
        if (result < 0) logger.Debug("DWM corner preference unavailable: {HResult}", result);
    }
}
