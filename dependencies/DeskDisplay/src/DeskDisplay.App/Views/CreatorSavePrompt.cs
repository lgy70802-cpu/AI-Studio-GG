using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public sealed class CreatorSavePrompt : Window
{
    public CreatorLeaveChoice Choice { get; private set; } = CreatorLeaveChoice.Cancel;
    public CreatorSavePrompt(string name)
    {
        Title = "保存修改"; Width = 430; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 247, 251));
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative) });
        var panel = new StackPanel { Margin = new(24) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = $"保存“{name}”的修改？", TextWrapping = TextWrapping.Wrap, FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "恢复草稿与正式保存分开。放弃修改会移除本次草稿。", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new(0, 12, 0, 20) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(actions);
        foreach (var item in new[] { ("取消", CreatorLeaveChoice.Cancel), ("放弃修改", CreatorLeaveChoice.Discard), ("保存", CreatorLeaveChoice.Save) })
        {
            var button = new Button { Content = item.Item1, IsCancel = item.Item2 == CreatorLeaveChoice.Cancel, IsDefault = item.Item2 == CreatorLeaveChoice.Save, Margin = new(6, 0, 0, 0), Style = (Style)FindResource(item.Item2 == CreatorLeaveChoice.Save ? "PrimaryButton" : item.Item2 == CreatorLeaveChoice.Discard ? "DangerButton" : "SoftButton") };
            button.Click += (_, _) => { Choice = item.Item2; DialogResult = item.Item2 != CreatorLeaveChoice.Cancel; }; actions.Children.Add(button);
        }
    }
}
