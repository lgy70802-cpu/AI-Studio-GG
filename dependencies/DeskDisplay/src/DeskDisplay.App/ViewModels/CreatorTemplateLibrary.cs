using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Views;
using Microsoft.Win32;

namespace DeskDisplay.App.ViewModels;

public sealed record CreatorTemplateCard(ThemeTemplate Template, BitmapSource? Cover)
{
    public string Name => Template.Name;
    public string Details => Template.OrientationLabel + " · " + Template.Contents;
    public double Aspect => Template.Aspect;
}

public sealed partial class CreatorViewModel
{
    private PluginManagerViewModel? _templatePlugins;
    private CancellationTokenSource _templateLoad = new();
    private Task _templatePending = Task.CompletedTask;
    private WallpaperItemViewModel? _templateWallpaper;
    private string _templateFile = "", _templateError = "";
    private bool _templateLoading;
    private bool? _coverPluginState;
    public Task PendingTemplate => _templatePending;
    public IReadOnlyList<CreatorTemplateCard> TemplateCards { get; private set; } = [];
    public CreatorTemplateCard? SelectedTemplateCard { get => TemplateCards.FirstOrDefault(c => c.Template == SelectedTemplate); set { if (value is not null) SelectedTemplate = value.Template; } }
    public IReadOnlyList<WallpaperItemViewModel> TemplateWallpapers => LibraryAssets.Where(a => !a.Asset.IsVideo).ToArray();
    public bool HasTemplateWallpapers => TemplateWallpapers.Count > 0;
    public bool TemplateUsesBackground => SelectedTemplate.SupportsBackground;
    public bool TemplateNeedsCamera => SelectedTemplate.RequiresCamera;
    public bool TemplateAvailable => !_templateLoading && _templateError.Length == 0 && (!TemplateNeedsCamera || _main.Plugins?.CameraEnabled == true);
    public bool ShowTemplateScene => _templateError.Length == 0 && (!TemplateNeedsCamera || _main.Plugins?.CameraEnabled == true);
    public bool TemplatePreviewUnavailable => !ShowTemplateScene;
    public string TemplateBackgroundLabel => _templateWallpaper?.Name ?? (string.IsNullOrEmpty(_templateFile) ? "纯色背景（可选壁纸）" : Path.GetFileName(_templateFile));
    public string TemplateStatus => _templateError.Length > 0 ? _templateError : _templateLoading ? "正在加载背景…" : TemplateNeedsCamera && _main.Plugins?.CameraEnabled != true ? "请先在插件管理中启用复古相机翻页时钟。" : TemplateNeedsCamera ? "使用已启用的复古相机插件；竖屏为独立组合布局。" : SelectedTemplate.Style == "photo" ? "图片槽位需要在编辑器中选择素材。" : "横竖布局均可独立编辑。";
    public WallpaperItemViewModel? TemplateWallpaper
    {
        get => _templateWallpaper;
        set { if (value is not null && value.Asset.IsVideo || ReferenceEquals(value, _templateWallpaper)) return; _templateWallpaper = value; _templateFile = ""; OnPropertyChanged(); RefreshTemplatePreview(); }
    }
    public ActionCommand PickTemplateBackgroundCommand => _pickTemplateBackground ??= new(_ =>
    {
        var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (dialog.ShowDialog() == true) SetTemplateBackgroundFile(dialog.FileName);
    }, _ => CanEdit && TemplateUsesBackground);
    public ActionCommand ClearTemplateBackgroundCommand => _clearTemplateBackground ??= new(_ => { _templateWallpaper = null; _templateFile = ""; OnPropertyChanged(nameof(TemplateWallpaper)); RefreshTemplatePreview(); }, _ => CanEdit && TemplateUsesBackground);
    private ActionCommand? _pickTemplateBackground, _clearTemplateBackground;
    public void SetTemplateBackgroundFile(string path)
    {
        if (!CanEdit || !TemplateUsesBackground) return;
        _templateWallpaper = null; _templateFile = Path.GetFullPath(path); OnPropertyChanged(nameof(TemplateWallpaper)); RefreshTemplatePreview();
    }
    private void OpenTemplateLibrary()
    {
        _templatePicker = true; Status = ""; AttachTemplatePlugins(); BuildTemplateCovers(); RefreshTemplatePreview(); NotifyPage();
    }
    private void AttachTemplatePlugins()
    {
        if (ReferenceEquals(_templatePlugins, _main.Plugins)) return;
        if (_templatePlugins is not null) _templatePlugins.PropertyChanged -= TemplatePluginChanged;
        _templatePlugins = _main.Plugins;
        if (_templatePlugins is not null) _templatePlugins.PropertyChanged += TemplatePluginChanged;
    }
    private void TemplatePluginChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PluginManagerViewModel.CameraEnabled) || _disposed) return;
        if (_templatePicker) BuildTemplateCovers(); RefreshTemplatePreview();
    }
    private ThemeProject CreateSelectedTemplate()
    {
        var project = SelectedTemplate.Create();
        var path = _templateWallpaper?.Asset.Path ?? _templateFile;
        if (TemplateUsesBackground && !string.IsNullOrEmpty(path))
            foreach (var layout in project.AllLayouts)
            {
                foreach (var layer in layout.Layers) layer.ZIndex++;
                var asset = _templateWallpaper?.Asset;
                layout.Layers.Insert(0, new() { Kind = ThemeElementKind.Wallpaper, Name = "背景壁纸", Path = path, AssetId = asset?.Id, AssetOrigin = string.IsNullOrWhiteSpace(asset?.CloudResourceId) ? ThemeAssetOrigin.Local : ThemeAssetOrigin.Cloud, CloudResourceId = asset?.CloudResourceId ?? "", Width = layout.Width, Height = layout.Height, Locked = true, Fill = ImageDisplayMode.Uniform });
            }
        project.Validate(); return project;
    }
    private void RefreshTemplatePreview()
    {
        if (_disposed || TemplateScene is null) return;
        _templateLoad.Cancel(); _templateLoad.Dispose(); _templateLoad = new();
        _templateError = ""; _templateLoading = false;
        var project = CreateSelectedTemplate(); TemplateScene.ClearChildren(); TemplateScene.UpdateProject(project);
        if (TemplateNeedsCamera && _main.Plugins is { CameraEnabled: true } plugin)
            foreach (var layer in project.ActiveLayout.Layers.Where(l => l.Kind == ThemeElementKind.CameraClock)) TemplateScene.SetChild(layer.Id, plugin.CameraScene);
        var image = project.ActiveLayout.Layers.FirstOrDefault(l => l.Kind == ThemeElementKind.Wallpaper);
        _templatePending = image is null ? Task.CompletedTask : LoadTemplateImageAsync(project, image, _templateLoad.Token);
        NotifyTemplateLibrary();
    }
    private async Task LoadTemplateImageAsync(ThemeProject project, ThemeLayer layer, CancellationToken ct)
    {
        _templateLoading = true;
        try
        {
            var image = await new ImageSceneLoader().LoadAsync(layer.Path, layer.Fill, ct);
            if (!ct.IsCancellationRequested && !_disposed && ReferenceEquals(TemplateScene.Project, project)) TemplateScene.SetChild(layer.Id, image);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception e) { if (!ct.IsCancellationRequested) { _templateError = "背景加载失败：" + e.Message; _logger.Warning(e, "Cannot prepare template background"); } }
        finally { if (!ct.IsCancellationRequested && !_disposed) { _templateLoading = false; NotifyTemplateLibrary(); } }
    }
    private async Task UseSelectedTemplateAsync()
    {
        if (!TemplateAvailable) throw new InvalidOperationException(TemplateStatus);
        var copy = TemplateScene.Project.Clone(); copy.Id = Guid.NewGuid();
        await SaveIfDirtyAsync();
        if (!TemplateAvailable) throw new InvalidOperationException(TemplateStatus);
        await OpenProjectAsync(copy); _dirty = true; Changed(false);
    }
    private void BuildTemplateCovers()
    {
        var enabled = _main.Plugins?.CameraEnabled == true;
        if (TemplateCards.Count > 0 && _coverPluginState == enabled) return;
        TemplateCards = Templates.Select(template =>
        {
            using var scene = new ThemeScene(template.Create());
            if (template.RequiresCamera && _main.Plugins is { CameraEnabled: true } plugin)
                foreach (var layer in scene.Project.ActiveLayout.Layers.Where(l => l.Kind == ThemeElementKind.CameraClock)) scene.SetChild(layer.Id, plugin.CameraScene);
            var view = new SceneView { Scene = scene, AspectRatioOverride = template.Aspect, Width = 480 * Math.Min(1, template.Aspect), Height = 480 / Math.Max(1, template.Aspect) };
            view.Measure(new Size(view.Width, view.Height)); view.Arrange(new Rect(0, 0, view.Width, view.Height)); view.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)view.Width, (int)view.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); bitmap.Freeze(); view.Scene = null;
            return new CreatorTemplateCard(template, bitmap);
        }).ToArray();
        _coverPluginState = enabled;
        OnPropertyChanged(nameof(TemplateCards)); OnPropertyChanged(nameof(SelectedTemplateCard));
    }
    private void NotifyTemplateLibrary()
    {
        foreach (var p in new[] { nameof(SelectedTemplateCard), nameof(TemplateUsesBackground), nameof(TemplateNeedsCamera), nameof(TemplateAvailable), nameof(ShowTemplateScene), nameof(TemplatePreviewUnavailable), nameof(TemplateStatus), nameof(TemplateBackgroundLabel), nameof(TemplateAspect), nameof(TemplateWallpapers), nameof(HasTemplateWallpapers) }) OnPropertyChanged(p);
        UseTemplateCommand?.NotifyCanExecuteChanged(); _pickTemplateBackground?.Notify(); _clearTemplateBackground?.Notify();
    }
    private void DisposeTemplateLibrary()
    {
        if (_templatePlugins is not null) _templatePlugins.PropertyChanged -= TemplatePluginChanged;
        _templateLoad.Cancel(); _templateLoad.Dispose(); TemplateCards = [];
    }
}
