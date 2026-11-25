using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDisplay.App.Views;

namespace DeskDisplay.DisplayTests;
internal static class MainWindowChromeTests
{
    public static async Task RunAsync()
    {
        var window = new MainWindow();
        try
        {
            window.Show(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var hwnd = new WindowInteropHelper(window).Handle;
            GetWindowRect(hwnd, out var rect);
            int Hit(int x, int y) => (int)SendMessage(hwnd, 0x84, 0, (nint)((ushort)x | ((uint)(ushort)y << 16)));
            var centerX = (rect.Left + rect.Right) / 2; var centerY = (rect.Top + rect.Bottom) / 2;
            Console.WriteLine($"CHROME: rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}; work={SystemParameters.WorkArea}; min={window.MinWidth}x{window.MinHeight}; title={Hit(centerX, rect.Top + 24)}; left={Hit(rect.Left + 2, centerY)}; right={Hit(rect.Right - 2, centerY)}; bottom={Hit(centerX, rect.Bottom - 2)}");
            Check(Hit(centerX, rect.Top + 24) == 2, "Blank title area remains a native draggable caption");
            foreach (var (x, y, expected) in new[] { (rect.Left + 2, centerY, 10), (rect.Right - 2, centerY, 11), (centerX, rect.Top + 2, 12), (centerX, rect.Bottom - 2, 15), (rect.Left + 2, rect.Top + 2, 13), (rect.Right - 2, rect.Top + 2, 14), (rect.Left + 2, rect.Bottom - 2, 16), (rect.Right - 2, rect.Bottom - 2, 17) })
                Check(Hit(x, y) == expected, $"Native resize region {expected} is not intercepted by sidebar or title controls");
            window.Width = 1000; window.Height = 680;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(Math.Abs(window.ActualWidth - 1000) < 2 && Math.Abs(window.ActualHeight - 680) < 2, "Main window can shrink below the former 1100 by 740 minimum");
            var maximize = Descendants<Button>(window).Single(b => b.Command == SystemCommands.MaximizeWindowCommand);
            var buttonPoint = maximize.PointToScreen(new Point(maximize.ActualWidth / 2, maximize.ActualHeight / 2));
            Check(Hit((int)buttonPoint.X, (int)buttonPoint.Y) == 1, "Window command buttons remain clickable client controls inside the draggable title bar");
            maximize.Command.Execute(null); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(window.WindowState == WindowState.Maximized, "Custom maximize button still invokes native maximize");
            maximize.Command.Execute(null); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(window.WindowState == WindowState.Normal, "Same title button restores a maximized window");
        }
        finally { window.Close(); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var nested in Descendants<T>(child)) yield return nested; } }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wparam, nint lparam);
}
