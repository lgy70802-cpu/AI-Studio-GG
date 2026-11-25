using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskDisplay.App.Views;

public sealed class CreatorColorPicker : Window
{
    public string SelectedColor { get; private set; }
    public CreatorColorPicker(string initial)
    {
        SelectedColor = initial; Title = "选择颜色"; Width = 360; Height = 350; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(244, 247, 251));
        foreach (var dictionary in new[] { "Colors", "Typography", "Controls" }) Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/DeskDisplay.App;component/Themes/{dictionary}.xaml", UriKind.Relative) });
        var root = new StackPanel { Margin = new(20) }; Content = root;
        var swatch = new Border { Height = 50, CornerRadius = new(8), Margin = new(0, 0, 0, 12) }; root.Children.Add(swatch);
        Color color; try { color = (Color)ColorConverter.ConvertFromString(initial); } catch { color = Colors.White; }
        var sliders = new List<Slider>(); var label = new TextBlock { Margin = new(0, 8, 0, 12) };
        foreach (var (name, value) in new[] { ("红", color.R), ("绿", color.G), ("蓝", color.B), ("透明度", color.A) })
        {
            var row = new DockPanel { Margin = new(0, 4, 0, 4) }; row.Children.Add(new TextBlock { Text = name, Width = 60 });
            var slider = new Slider { Minimum = 0, Maximum = 255, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true }; row.Children.Add(slider); root.Children.Add(row); sliders.Add(slider);
        }
        void Refresh() { var c = Color.FromArgb((byte)sliders[3].Value, (byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value); SelectedColor = c.ToString(); swatch.Background = new SolidColorBrush(c); label.Text = SelectedColor; }
        foreach (var slider in sliders) slider.ValueChanged += (_, _) => Refresh(); Refresh(); root.Children.Add(label);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; root.Children.Add(actions);
        var apply = new Button { Content = "应用", Padding = new(16, 6, 16, 6), IsDefault = true }; apply.Click += (_, _) => DialogResult = true; actions.Children.Add(apply);
        apply.SetResourceReference(StyleProperty, "PrimaryButton");
        var cancel = new Button { Content = "取消", Padding = new(16, 6, 16, 6), Margin = new(8, 0, 0, 0), IsCancel = true }; actions.Children.Add(cancel);
        cancel.SetResourceReference(StyleProperty, "SoftButton");
    }
}
