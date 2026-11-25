using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class CarouselManagementView : UserControl
{
    private Point _start;
    private Guid? _dragId;
    private LibraryViewModel? Library => (DataContext as MainViewModel)?.Library;
    public CarouselManagementView() => InitializeComponent();
    private async void CarouselTabChanged(object sender,SelectionChangedEventArgs e)
    {
        if(e.Source!=sender || sender is not TabControl {SelectedIndex:1} || ThemePlaylistHost.Content is not null || DataContext is not MainViewModel {Creator:{ } creator})return;
        try{await creator.LoadThemePlaylistAsync();ThemePlaylistHost.Content=CreatorThemePlaylist.CreatePanel(creator);}
        catch(Exception ex){Serilog.Log.Warning(ex,"Cannot open integrated theme playlist");MessageBox.Show("主题轮播读取失败："+ex.Message);}
    }
    private void SelectThemeTab(object sender,RoutedEventArgs e){if(Content is TabControl tabs)tabs.SelectedIndex=1;}
    private void AddSelected(object sender, RoutedEventArgs e) => Library?.AddCarouselItems(Candidates.SelectedItems.OfType<WallpaperItemViewModel>().Select(i => i.Asset.Id).ToArray());
    private void RemoveSelected(object sender, RoutedEventArgs e) => Library?.RemoveCarouselItems(Playlist.SelectedItems.OfType<WallpaperItemViewModel>().Select(i => i.Asset.Id).ToArray());
    private static T? Ancestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null) { if (source is T found) return found; source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source); }
        return null;
    }
    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Playlist);
        _dragId = Ancestor<Button>(e.OriginalSource as DependencyObject) is null ? (Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as WallpaperItemViewModel)?.Asset.Id : null;
    }
    private void DragMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragId is not Guid id) return;
        var point = e.GetPosition(Playlist);
        if (Math.Abs(point.X - _start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - _start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragId = null;
        DragDrop.DoDragDrop(Playlist, new DataObject("DeskDisplay.CarouselItem", id), DragDropEffects.Move);
    }
    private void DropItem(object sender, DragEventArgs e)
    {
        if (e.Data.GetData("DeskDisplay.CarouselItem") is Guid id && Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is WallpaperItemViewModel target)
        { Library?.MoveCarouselItem(id, target.Asset.Id); e.Handled = true; }
    }
}
