using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    FrameworkElement BuildFlipClock(FlipClockSettings settings, Brush ink, double fontSize, bool live, DateTimeOffset? frameTime)
    {
        var digits = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"), FontSize = fontSize, FontWeight = FontWeights.Bold,
            Foreground = ink, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(digits, "翻页时钟时间");
        var face = new Border
        {
            Background = ColorBrush("#ED161E29"), BorderBrush = ColorBrush("#5F7185"), BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(fontSize * .24, fontSize * .08, fontSize * .24, fontSize * .08)
        };
        var display = new Grid();
        var period = new TextBlock
        {
            FontSize = fontSize * .22, Foreground = ink, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, -fontSize * .08, 0, 0), VerticalAlignment = VerticalAlignment.Top
        };
        display.Children.Add(digits);
        display.Children.Add(period);
        face.Child = display;

        void Refresh()
        {
            var now = frameTime ?? state.LocalTime;
            var time = settings.Display(now);
            if (digits.Text != time) digits.Text = time;
            var badge = settings.Period(now);
            if (period.Text != badge) period.Text = badge;
        }

        Refresh();
        if (live)
        {
            var update = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            update.Tick += (_, _) => Refresh();
            face.Loaded += (_, _) => { Refresh(); update.Start(); };
            face.Unloaded += (_, _) => update.Stop();
        }
        return face;
    }
}
