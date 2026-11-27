using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) =>
        {
            if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
        }));
    }
    private void SidebarMouseUp(object sender, MouseButtonEventArgs e)
    {
        var container = ItemsControl.ContainerFromElement(SidebarNavigation, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container?.DataContext is NavigationItem { Page: AppPage.Online }) OpenOnlineMenu(container);
        else OnlineResourcePopup.IsOpen = false;
    }
    private void SidebarKeyDown(object sender, KeyEventArgs e)
    {
        if (SidebarNavigation.SelectedItem is NavigationItem { Page: AppPage.Online } && e.Key is Key.Right or Key.Enter or Key.Space)
        { if (SidebarNavigation.ItemContainerGenerator.ContainerFromItem(SidebarNavigation.SelectedItem) is UIElement item) { OpenOnlineMenu(item); e.Handled = true; } }
    }
    public void OpenOnlineMenu(UIElement anchor) { OnlineResourcePopup.PlacementTarget = anchor; OnlineResourcePopup.IsOpen = true; }
    private void OnlineMenuOpened(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (!OnlineResourcePopup.IsOpen) return;
        var buttons = MenuButtons(OnlineMenuPanel).ToArray();
        var selected = (DataContext as MainViewModel)?.Cloud?.ResourceKind;
        foreach (var button in buttons) { var active = button.Tag is OnlineResourceKind kind && kind == selected; button.Background = active ? (Brush)FindResource("Brush.PrimarySoft") : Brushes.Transparent; button.Foreground = (Brush)FindResource(active ? "Brush.Primary" : "Brush.Text"); }
        (buttons.FirstOrDefault(button => button.Tag is OnlineResourceKind kind && kind == selected) ?? buttons.FirstOrDefault())?.Focus();
    });
    private void OnlineMenuKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { OnlineResourcePopup.IsOpen = false; SidebarNavigation.Focus(); e.Handled = true; } }
    private void ChooseOnlineResource(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OnlineResourceKind kind } && DataContext is MainViewModel { Cloud: not null } main) { main.Cloud.ResourceKind = kind; main.SelectedPage = AppPage.Online; }
        OnlineResourcePopup.IsOpen = false;
    }
    private static IEnumerable<Button> MenuButtons(DependencyObject parent)
    {
        if (parent is Button button) yield return button;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) foreach (var child in MenuButtons(VisualTreeHelper.GetChild(parent, index))) yield return child;
    }

}
