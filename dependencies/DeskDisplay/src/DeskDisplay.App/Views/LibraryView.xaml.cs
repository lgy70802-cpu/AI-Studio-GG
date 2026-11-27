using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class LibraryView : UserControl
{
    private void EditSavedTheme(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.SelectedPage = AppPage.Creator; }
    private async void UseSavedTheme(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main || sender is not Button button) return;
        var source = button.DataContext;
        if (source is not (ThemeCard or WallpaperItemViewModel)) return;
        button.IsEnabled = false;
        try { await QuickDeviceUse.ApplyAsync(main, source, Window.GetWindow(this), Equals(button.Tag, "choose")); }
        finally { button.IsEnabled = true; }
    }
    private async void FavoriteTheme(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Creator: { } creator } || sender is not Button { DataContext: ThemeCard card } button) return;
        button.IsEnabled = false;
        try { await creator.ToggleThemeFavoriteAsync(card); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Cannot save theme favorite"); MessageBox.Show("收藏保存失败：" + ex.Message); }
        finally { button.IsEnabled = true; }
    }
    private void PreviousThemes(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.Library?.ChangeThemePage(-1); }
    private void NextThemes(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.Library?.ChangeThemePage(1); }
    public LibraryView() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel { Library: not null } main) main.Library.ScrollRestoreRequested += Restore; }
    private void OnUnloaded(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel { Library: not null } main) main.Library.ScrollRestoreRequested -= Restore; }
    private void Restore(double offset) => Dispatcher.BeginInvoke(() => GalleryScroller.ScrollToVerticalOffset(offset));
    private void OnGalleryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainViewModel { Library: { IsGallery: true } library }) return;
        library.UpdateGalleryViewport(e.ViewportWidth, e.ViewportHeight, e.VerticalOffset);
        if (e.ExtentHeight <= e.ViewportHeight || e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 160) library.LoadMore();
    }
}
