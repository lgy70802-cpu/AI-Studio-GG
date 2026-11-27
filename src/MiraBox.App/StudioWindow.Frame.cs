using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    void InitializeWindowFrame()
    {
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(16),
            GlassFrameThickness = new Thickness(1),
            UseAeroCaptionButtons = false
        });
        var frame = new Border { Background = Brushes.White, Child = new Grid { Children = { pluginHost, browser } } };
        void UpdateFrame()
        {
            frame.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
            frame.BorderBrush = new SolidColorBrush(IsActive
                ? Color.FromRgb(197, 192, 212)
                : Color.FromRgb(226, 224, 233));
        }
        Activated += (_, _) => UpdateFrame();
        Deactivated += (_, _) => UpdateFrame();
        StateChanged += (_, _) => UpdateFrame();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessages);
            var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            if (GetWindowRect(handle, out var bounds))
            {
                var width = Math.Min(bounds.Right - bounds.Left, Math.Max(1, area.Width - 24));
                var height = Math.Min(bounds.Bottom - bounds.Top, Math.Max(1, area.Height - 24));
                var scale = GetDpiForWindow(handle) / 96.0;
                MinWidth = Math.Min(MinWidth, width / scale);
                MinHeight = Math.Min(MinHeight, height / scale);
                Width = width / scale;
                Height = height / scale;
                SetWindowPos(handle, IntPtr.Zero, area.Left + (area.Width - width) / 2,
                    area.Top + (area.Height - height) / 2, width, height, 0x14);
            }
            UpdateWindowCorners();
        };
        SizeChanged += (_, _) => UpdateWindowCorners();
        StateChanged += (_, _) => UpdateWindowCorners();
        UpdateFrame();
        Content = frame;
    }

    void UpdateWindowCorners()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var preference = WindowState == WindowState.Maximized ? 1 : 2;
        // Windows 11 provides compositor-rounded corners; older Windows uses a window region.
        if (DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int)) == 0) return;
        if (WindowState != WindowState.Normal)
        {
            SetWindowRgn(handle, IntPtr.Zero, true);
            return;
        }
        if (!GetWindowRect(handle, out var bounds)) return;
        var diameter = (int)Math.Round(24 * GetDpiForWindow(handle) / 96.0);
        var region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left + 1,
            bounds.Bottom - bounds.Top + 1, diameter, diameter);
        if (region != IntPtr.Zero && SetWindowRgn(handle, region, true) == 0) DeleteObject(region);
    }

    object WindowDiagnostics()
    {
        var handle = new WindowInteropHelper(this).Handle;
        GetWindowRect(handle, out var bounds);
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var area = screen.WorkingArea;
        return new
        {
            state = WindowState.ToString(), dpi = GetDpiForWindow(handle),
            bounds = new { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom },
            workArea = new { left = area.Left, top = area.Top, right = area.Right, bottom = area.Bottom },
            monitors = System.Windows.Forms.Screen.AllScreens.Length
        };
    }

    IntPtr WindowMessages(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0024) return IntPtr.Zero;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var area = screen.WorkingArea;
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        info.MaxPosition = new NativePoint { X = area.Left - screen.Bounds.Left, Y = area.Top - screen.Bounds.Top };
        info.MaxSize = new NativePoint { X = area.Width, Y = area.Height };
        var scale = GetDpiForWindow(handle) / 96.0;
        info.MinTrackSize = new NativePoint { X = (int)Math.Ceiling(MinWidth * scale), Y = (int)Math.Ceiling(MinHeight * scale) };
        Marshal.StructureToPtr(info, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowBounds { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out WindowBounds bounds);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr handle, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
