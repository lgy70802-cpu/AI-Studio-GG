using System.Windows.Controls;
using System.Windows;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
    private void BrowseHomeLibrary(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main) main.SelectedPage = AppPage.Library;
    }

    private void BrowseDeviceLibrary(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main && sender is Button { DataContext: ManagedDeviceCard card }) { main.QuickDevice = card; main.SelectedPage = AppPage.Library; } }
    private void OpenRecent(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main && sender is Button { DataContext: HomeRecentEntry entry })
        {
            if(entry.IsTheme) UseRecent(sender,e);
            else main.OpenHomeResourceCommand.Execute(entry.Source);
        }
    }
    private async void UseRecent(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main || sender is not Button { DataContext: HomeRecentEntry entry } button) return;
        button.IsEnabled = false;
        try
        {
            await QuickDeviceUse.ApplyAsync(main, entry.Source, Window.GetWindow(this), Equals(button.Tag, "choose"));
        }
        finally { button.IsEnabled = true; }
    }
}
