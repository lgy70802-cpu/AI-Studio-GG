using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace MiraBox.App;

public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)]
    struct LastInputInfo { public uint Size; public uint Tick; }
    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LastInputInfo info);
    readonly Dictionary<string, long> displayWakeTicks = [];

    void TickDisplayPower()
    {
        var input = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref input)) return;
        var idle = unchecked((uint)Environment.TickCount - input.Tick) / 1000d;
        foreach (var serial in HardwareSessionSerials())
        {
            if (HardwareOutputFor(serial) is not { Identification: false } output) continue;
            var options = hardwareOrientations.GetOptions(serial);
            var sinceWake = displayWakeTicks.TryGetValue(serial, out var wake)
                ? (Environment.TickCount64 - wake) / 1000d : double.MaxValue;
            output.DisplayBrightness = options.DisplayBrightness;
            output.DisplayStandby = options.StandbyMinutes > 0 &&
                Math.Min(idle, sinceWake) >= options.StandbyMinutes * 60;
        }
    }

    Border BuildDisplayPowerSettings(string serial)
    {
        var options = hardwareOrientations.GetOptions(serial);
        var panel = new StackPanel();
        panel.Children.Add(HomeText("显示与待机", 18, bold: true));
        panel.Children.Add(HomeText("画面亮度", 13));
        var brightness = new Slider { Minimum = 0, Maximum = 100, Value = options.DisplayBrightness,
            TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 10, 0, 8) };
        var value = HomeText($"{options.DisplayBrightness}%", 12);
        panel.Children.Add(brightness);
        panel.Children.Add(value);
        brightness.ValueChanged += (_, _) =>
        {
            var level = (int)brightness.Value;
            value.Text = $"{level}%";
            hardwareOrientations.SaveOptions(serial, hardwareOrientations.GetOptions(serial) with { DisplayBrightness = level });
            TickDisplayPower();
        };
        panel.Children.Add(HomeText("调暗发送画面，设备背光保持开启。视频调暗会增加处理开销。", 12, muted: true));
        panel.Children.Add(HomeText("电脑空闲后黑屏", 13));
        var minutes = new[] { 0, 1, 5, 10, 15, 30, 60 };
        var standby = new ComboBox { ItemsSource = minutes.Select(m => m == 0 ? "永不" : $"{m} 分钟").ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(minutes, options.StandbyMinutes)), Margin = new Thickness(0, 8, 0, 8) };
        standby.SelectionChanged += (_, _) =>
        {
            if (standby.SelectedIndex < 0) return;
            hardwareOrientations.SaveOptions(serial, hardwareOrientations.GetOptions(serial) with { StandbyMinutes = minutes[standby.SelectedIndex] });
            TickDisplayPower();
        };
        panel.Children.Add(standby);
        panel.Children.Add(HomeText("鼠标或键盘操作后自动恢复原内容。黑屏期间保持设备连接。", 12, muted: true));
        panel.Children.Add(HomeButton("立即唤醒", () =>
        {
            displayWakeTicks[serial] = Environment.TickCount64;
            TickDisplayPower();
        }, "SmallButton"));
        return HomeCard(panel);
    }
}
