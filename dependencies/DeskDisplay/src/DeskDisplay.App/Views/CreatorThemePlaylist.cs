using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public sealed class CreatorThemePlaylist : Window
{
    public CreatorThemePlaylist(CreatorViewModel editor)
    {
        Title = "主题轮播"; Width = 900; Height = 600; MinWidth = 620; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; DataContext = editor; Background = System.Windows.Media.Brushes.White;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative) }); Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative) });
        var panel = CreatePanel(editor, this); Content = panel;
    }
    public static DockPanel CreatePanel(CreatorViewModel editor, Window? owner = null)
    {
        var panel = new DockPanel { Margin = new(20), DataContext=editor };
        var playback = new StackPanel { Margin = new(0, 0, 0, 12) }; DockPanel.SetDock(playback, Dock.Top); panel.Children.Add(playback);
        foreach (var (property, format) in new[] { (nameof(CreatorViewModel.ThemeCurrentName), "当前：{0}"), (nameof(CreatorViewModel.ThemeNextName), "下一候选：{0}（失效项会跳过）"), (nameof(CreatorViewModel.ThemeCountdown), "{0}") }) { var label = new TextBlock { Margin = new(0, 0, 0, 4) }; label.SetBinding(TextBlock.TextProperty, new Binding(property) { StringFormat = format }); playback.Children.Add(label); }
        var top = new WrapPanel { Margin = new(0, 0, 0, 14) }; DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var source = new ComboBox { ItemsSource = editor.Projects, DisplayMemberPath = "Name", SelectedIndex = editor.Projects.Count > 0 ? 0 : -1, Width = 250, Margin = new(0, 0, 10, 0) }; source.SetBinding(ItemsControl.ItemsSourceProperty,new Binding(nameof(CreatorViewModel.Projects))); top.Children.Add(source);
        var add = Make("添加已保存主题", () => { if (source.SelectedItem is ThemeCard card) editor.AddThemePlaylist(card.Project.Id); }); top.Children.Add(add);
        var target = new ComboBox { ItemsSource = editor.OutputTargets, Width = 150, Margin = new(12, 0, 0, 0) }; target.SetBinding(ComboBox.SelectedItemProperty, new Binding(nameof(editor.ThemePlaylistTarget)) { Mode = BindingMode.TwoWay }); top.Children.Add(target);
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); panel.Children.Add(bottom); var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) }; status.SetBinding(TextBlock.TextProperty, nameof(editor.ThemePlaylistStatus)); bottom.Children.Add(status);
        var buttons = new WrapPanel(); bottom.Children.Add(buttons);
        var grid = new DataGrid { ItemsSource = editor.ThemePlaylistItems, AutoGenerateColumns = false, CanUserSortColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, GridLinesVisibility = DataGridGridLinesVisibility.None, RowHeight = 40, ColumnHeaderHeight = 36 };
        grid.SetResourceReference(StyleProperty,"SoftDataGrid");source.SetResourceReference(StyleProperty,"SoftSelector");target.SetResourceReference(StyleProperty,"SoftSelector");
        grid.Columns.Add(new DataGridTextColumn { Header = "主题", Binding = new Binding(nameof(ThemePlaylistRow.Name)), IsReadOnly = true, MinWidth = 160, Width = new DataGridLength(1, DataGridLengthUnitType.Star) }); grid.Columns.Add(new DataGridTextColumn { Header = "停留秒数（10—86400）", Binding = new Binding(nameof(ThemePlaylistRow.Seconds)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = 180 }); grid.Columns.Add(new DataGridTextColumn { Header="可用性", Binding=new Binding(nameof(ThemePlaylistRow.Availability)), IsReadOnly=true, Width=160 }); panel.Children.Add(grid);
        buttons.Children.Add(Make("上移", () => { if (grid.SelectedItem is ThemePlaylistRow row) editor.MoveThemePlaylist(row.ProjectId, -1); })); buttons.Children.Add(Make("下移", () => { if (grid.SelectedItem is ThemePlaylistRow row) editor.MoveThemePlaylist(row.ProjectId, 1); })); buttons.Children.Add(Make("移除", () => { if (grid.SelectedItem is ThemePlaylistRow row) editor.RemoveThemePlaylist(row.ProjectId); }));
        var start = Make("开始轮播", () => { }); start.Click += async (_, _) => { start.IsEnabled = false; grid.CommitEdit(); try { await editor.ConfirmThemePlaylistStartAsync(owner ?? Window.GetWindow(panel)); } catch (Exception e) { MessageBox.Show(owner ?? Window.GetWindow(panel), e.Message, "轮播未启动"); } finally { start.IsEnabled = true; } }; buttons.Children.Add(start);
        var next = Make("下一项", () => { }); next.Click += async (_, _) => { try { await editor.AdvanceThemePlaylistAsync(); } catch (Exception e) { MessageBox.Show(owner ?? Window.GetWindow(panel), e.Message, "切换未完成"); } }; buttons.Children.Add(next);
        var pause = Make("暂停切换", editor.ToggleThemePause); pause.SetBinding(Button.ContentProperty, new Binding(nameof(CreatorViewModel.ThemePauseLabel))); pause.SetBinding(IsEnabledProperty, new Binding(nameof(CreatorViewModel.IsThemeRotating))); buttons.Children.Add(pause);
        buttons.Children.Add(Make("停止切换", editor.StopThemePlaylist)); if(owner is not null) buttons.Children.Add(Make("关闭管理", owner.Close));
        panel.Unloaded += (_, _) => { grid.CommitEdit(); grid.CommitEdit(DataGridEditingUnit.Row,true); };
        return panel;
    }
    private static Button Make(string text, Action action) { var button = new Button { Content = text, Margin = new(0, 0, 8, 8) };button.SetResourceReference(StyleProperty,text=="开始轮播"?"PrimaryButton":"SoftButton"); button.Click += (_, _) => action(); return button; }
}
