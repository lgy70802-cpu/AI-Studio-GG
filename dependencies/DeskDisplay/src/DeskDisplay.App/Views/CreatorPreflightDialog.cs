using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public sealed class CreatorPreflightDialog : Window
{
    public CreatorPreflightDialog(CreatorViewModel editor, bool includeLegacyTarget = false)
    {
        Title = "主题检查"; Width = 620; Height = 430; MinWidth = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(244, 247, 251));
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative) });
        var panel = new DockPanel { Margin = new(20) }; Content = panel;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
        var back = new Button { Content = "返回编辑", IsCancel = true, Style = (Style)FindResource("SoftButton") }; actions.Children.Add(back);
        var next = new Button { Content = "继续预览并检查设备", Style = (Style)FindResource("PrimaryButton"), Margin = new(8, 0, 0, 0) }; actions.Children.Add(next);
        var issues = editor.InspectTheme(includeLegacyTarget); next.IsEnabled = !issues.Any(i => i.Blocking);
        next.Click += (_, _) => { if (!editor.InspectTheme(includeLegacyTarget).Any(i => i.Blocking)) DialogResult = true; };
        var list = new StackPanel(); panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        list.Children.Add(new TextBlock { Text = "主题检查", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 14) });
        list.Children.Add(new TextBlock { Text = includeLegacyTarget ? "当前目标：" + editor.OutputTarget + "；多设备请在按设备预览中逐台核对。" : "素材与布局检查；设备能力将在预览中核对。", TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) });
        if (issues.Count == 0) list.Children.Add(new TextBlock { Text = "素材与布局检查通过，下一步核对目标设备能力。", TextWrapping = TextWrapping.Wrap });
        foreach (var issue in issues)
        {
            var button = new Button { Content = new TextBlock { Text = issue.Label + (issue.LayerId is not null ? "  › 定位" : ""), TextWrapping = TextWrapping.Wrap }, Style = (Style)FindResource("SoftButton"), Margin = new(0, 0, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Left, IsEnabled = issue.LayerId is not null };
            button.Click += (_, _) => { if (issue.LayerId is Guid id) { editor.LocatePreflightIssue(id); DialogResult = false; } }; list.Children.Add(button);
        }
    }
}
