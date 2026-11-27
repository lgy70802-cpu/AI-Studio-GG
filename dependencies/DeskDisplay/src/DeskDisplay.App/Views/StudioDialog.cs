using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskDisplay.App.Views;

internal static class StudioDialog
{
    public static Window Create(string title, UIElement content, Window? owner, double width = 480)
    {
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 20)
        });
        body.Children.Add(content);
        var dialog = new Window
        {
            Title = title,
            Width = width,
            MaxHeight = SystemParameters.WorkArea.Height,
            SizeToContent = SizeToContent.Height,
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 49, 75)),
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
            FontSize = 13,
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };
        if (owner is not null) dialog.Owner = owner;
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative)
        });
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative)
        });
        return dialog;
    }

    public static void ShowError(string title, string message, Window? owner)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var close = new Button { Content = "知道了", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        panel.Children.Add(close);
        var dialog = Create(title, panel, owner);
        close.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        dialog.ShowDialog();
    }
}
