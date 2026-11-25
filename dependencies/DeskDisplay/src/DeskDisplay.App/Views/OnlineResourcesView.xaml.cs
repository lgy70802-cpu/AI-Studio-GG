using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
namespace DeskDisplay.App.Views;
public partial class OnlineResourcesView : UserControl
{
    public OnlineResourcesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    private void OpenTools(object sender, RoutedEventArgs e) => OnlineToolsPopup.IsOpen = !OnlineToolsPopup.IsOpen;
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Cloud: not null } main) main.Cloud.ScrollRestoreRequested += RestoreScroll;
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Cloud: not null } main) main.Cloud.ScrollRestoreRequested -= RestoreScroll;
    }
    private void RestoreScroll(double offset) => Dispatcher.BeginInvoke(() => GalleryScroller.ScrollToVerticalOffset(offset));
    private void OnGalleryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is MainViewModel { Cloud: not null } main)
        {
            if (e.VerticalChange != 0) main.Cloud.UpdateScrollOffset(e.VerticalOffset);
            main.Cloud.UpdateViewport(e.ViewportWidth, e.ViewportHeight, e.VerticalOffset, GalleryScroller.ScrollableHeight);
        }
    }
}
