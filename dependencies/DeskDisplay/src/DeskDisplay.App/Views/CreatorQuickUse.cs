using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;
public partial class CreatorView
{
    private async void UseSavedTheme(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main || sender is not Button { DataContext: ThemeCard card } button) return;
        button.IsEnabled = false;
        try { await SavedThemeUse.OpenAsync(main, card, Window.GetWindow(this)); }
        finally { button.IsEnabled = true; }
    }
    private void ShowSavedThemes(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.Creator?.ShowSavedThemes(); }
}
