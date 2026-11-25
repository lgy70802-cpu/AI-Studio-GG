using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Display.Views;

namespace DeskDisplay.App.Views;

public sealed class CreatorApplyConfirmation : Window
{
    private readonly CreatorViewModel _editor;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly Button _apply = new() { Content = "确认应用" };
    public ThemeApplyCheck? CurrentCheck { get; private set; }
    public CreatorApplyConfirmation(CreatorViewModel editor)
    {
        _editor = editor; Title = "确认应用主题"; Width = 900; Height = 620; MinWidth = 600; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = System.Windows.Media.Brushes.White;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative) }); Resources.MergedDictionaries.Add(new() { Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative) });
        var panel = new DockPanel { Margin = new(20) }; Content = panel;
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); panel.Children.Add(bottom); bottom.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; bottom.Children.Add(actions);
        var cancel = new Button { Content = "取消", IsCancel = true, Style = (Style)FindResource("SoftButton"), Margin = new(0, 0, 10, 0) }; actions.Children.Add(cancel); _apply.Style = (Style)FindResource("PrimaryButton"); actions.Children.Add(_apply);
        var host = new PreviewHost { AspectRatio = editor.Aspect }; host.Child = new SceneView { Scene = editor.Scene, AspectRatioOverride = editor.Aspect }; panel.Children.Add(host);
        _apply.Click += (_, _) => { RefreshCheck(); if (CurrentCheck?.CanApply == true) DialogResult = true; };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += Tick; _timer.Start(); Closed += (_, _) => { _timer.Stop(); _timer.Tick -= Tick; host.Child = null; };
        RefreshCheck();
    }
    private void Tick(object? sender, EventArgs e) => RefreshCheck();
    private void RefreshCheck() { CurrentCheck = _editor.CheckThemeApplication(); _status.Text = CurrentCheck.Target + " · " + CurrentCheck.Orientation + "\n" + CurrentCheck.Geometry + "\n" + (CurrentCheck.CanApply ? "软件检查通过，实体输出效果以实际设备为准。" : string.Join("\n", CurrentCheck.Problems)); _apply.IsEnabled = CurrentCheck.CanApply; }
}
