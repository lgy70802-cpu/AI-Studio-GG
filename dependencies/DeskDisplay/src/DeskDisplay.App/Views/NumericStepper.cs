using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DeskDisplay.App.Views;

public sealed class NumericStepper : Grid
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericStepper), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get; set; } = -32768;
    public double Maximum { get; set; } = 32768;
    public double Step { get; set; } = 1;
    public NumericStepper()
    {
        Margin = new Thickness(0, 3, 0, 8); ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new() { Width = new(22) });
        var text = new TextBox { Padding = new(7, 5, 2, 5), ToolTip = "支持 +、-、*、/ 和括号，如 1920/2；小数使用点", Background = System.Windows.Media.Brushes.Transparent };
        text.SetBinding(TextBox.TextProperty, new Binding(nameof(Value)) { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, StringFormat = "0.##", Converter = new ExpressionConverter(this) }); Children.Add(text);
        var buttons = new StackPanel(); SetColumn(buttons, 1); Children.Add(buttons);
        foreach (var (label, delta) in new[] { ("+", 1), ("−", -1) })
        {
            var button = new Button { Content = label, Padding = new(0), FontSize = 10, Focusable = false };
            button.SetResourceReference(StyleProperty, "SoftButton");
            button.Click += (_, _) => { text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); if (double.IsFinite(Value)) SetCurrentValue(ValueProperty, Math.Clamp(Value + Step * delta, Minimum, Maximum)); };
            buttons.Children.Add(button);
        }
    }
    private sealed class ExpressionConverter(NumericStepper owner) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => ((double)value).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => DeskDisplay.Core.Models.NumericExpression.TryEvaluate(value?.ToString() ?? "", out var result) ? Math.Clamp(result, owner.Minimum, owner.Maximum) : DependencyProperty.UnsetValue;
    }

}
