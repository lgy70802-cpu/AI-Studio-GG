using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class CreatorMaterialBrowser : Window
{
    private readonly BrowserModel _model;
    public CreatorMaterialBrowser(CreatorViewModel editor)
    {
        InitializeComponent(); _model = new(editor); DataContext = _model; Loaded += (_, _) => Dispatcher.BeginInvoke(() => FindScroll(AssetsList)?.ScrollToVerticalOffset(editor.MaterialBrowserOffset));
        Closed += (_, _) => { editor.MaterialBrowserOffset = FindScroll(AssetsList)?.VerticalOffset ?? 0; _model.Dispose(); };
    }
    private async void Add(object sender, RoutedEventArgs e) => await Apply(false);
    private async void Replace(object sender, RoutedEventArgs e) => await Apply(true);
    private async Task Apply(bool replace)
    {
        if (_model.Selected is not { } selected) return;
        if (!selected.IsAvailable) { MessageBox.Show(this, "文件失效，请先在素材检查中修复路径。", "素材未应用"); return; }
        if (!replace && _model.Editor.Project.ActiveLayout.Layers.Count >= 100) { MessageBox.Show(this, "当前布局已达到100个图层上限，请先移除部分图层。", "素材未应用"); return; }
        IsEnabled = false;
        try { if (replace) await _model.Editor.ReplaceSelectedAssetAsync(selected.Asset, _model.KeepCrop); else { _model.Editor.AddAsset(selected.Asset); await _model.Editor.PendingOperation; } Close(); }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "素材未应用"); }
        finally { IsEnabled = true; }
    }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        if (parent is ScrollViewer found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) if (FindScroll(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
    private async void DoubleAdd(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(AssetsList, e.OriginalSource as DependencyObject) is ListBoxItem && _model.HasSelection) await Apply(false);
    }
    private void PreviewAsset(object sender, RoutedEventArgs e)
    {
        if (_model.Selected is not { IsAvailable: true } item) return;
        var panel = new DockPanel { Margin = new(16) }; var caption = new TextBlock { Text = item.Name + " · " + item.Metadata + (item.Asset.IsVideo ? " · 动态素材封面" : ""), Margin = new(0, 0, 0, 12) }; DockPanel.SetDock(caption, Dock.Top); panel.Children.Add(caption);
        var image = new Image { Stretch = Stretch.Uniform }; image.SetBinding(Image.SourceProperty, new Binding(nameof(WallpaperItemViewModel.DetailImage)) { Source = item }); panel.Children.Add(image);
        try { new Window { Title = "素材大图预览", Content = panel, Owner = this, Width = Math.Min(1100, SystemParameters.WorkArea.Width), Height = Math.Min(700, SystemParameters.WorkArea.Height), MinWidth = 400, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(244, 247, 251)) }.ShowDialog(); }
        finally { BindingOperations.ClearBinding(image, Image.SourceProperty); item.ReleaseDetailImage(); }
    }
    private sealed class BrowserModel : ObservableObject, IDisposable
    {
        private int _page = 1; private WallpaperItemViewModel? _selected;
        public CreatorViewModel Editor { get; }
        public IReadOnlyList<WallpaperItemViewModel> Items => Editor.FilteredAssets.Skip((_page - 1) * 12).Take(12).ToArray();
        public string PageLabel => $"{_page} / {Math.Max(1, (Editor.FilteredAssets.Count + 11) / 12)}";
        public WallpaperItemViewModel? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) { OnPropertyChanged(nameof(HasSelection)); OnPropertyChanged(nameof(CanReplace)); } } }
        public bool HasSelection => Selected?.IsAvailable == true;
        public bool KeepCrop { get; set; } = true;
        public bool CanReplace => HasSelection && Editor.CanEditSelection && Editor.IsImage;
        public ActionCommand Previous { get; }
        public ActionCommand Next { get; }
        public BrowserModel(CreatorViewModel editor)
        {
            Editor = editor; if (editor.MaterialBrowserSavedFilter == editor.MaterialBrowserFilterKey) _page = Math.Clamp(editor.MaterialBrowserPage, 1, Math.Max(1, (editor.FilteredAssets.Count + 11) / 12)); else editor.MaterialBrowserOffset = 0;
            _selected = Items.FirstOrDefault(i => i.Asset.Id == editor.MaterialBrowserSelection);
            Previous = new(_ => { _page--; Refresh(); }, _ => _page > 1); Next = new(_ => { _page++; Refresh(); }, _ => _page * 12 < Editor.FilteredAssets.Count); editor.PropertyChanged += Changed;
        }
        private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(CreatorViewModel.FilteredAssets)) { _page = 1; Editor.MaterialBrowserOffset = 0; Refresh(); } }
        private void Refresh() { Selected = null; OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(PageLabel)); Editor.TrimMaterialImages(Items); Previous.Notify(); Next.Notify(); }
        public void Dispose() { Editor.MaterialBrowserPage = _page; Editor.MaterialBrowserSelection = Selected?.Asset.Id; Editor.MaterialBrowserSavedFilter = Editor.MaterialBrowserFilterKey; Editor.PropertyChanged -= Changed; Editor.TrimMaterialImages([]); }
    }
}
