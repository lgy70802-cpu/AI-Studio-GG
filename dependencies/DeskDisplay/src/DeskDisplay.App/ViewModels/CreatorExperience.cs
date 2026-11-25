using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed record CreatorMaterialTab(string Name, string Glyph);
public sealed record CreatorFillChoice(ImageDisplayMode Value, string Label);

public sealed class CreatorLayerRow(CreatorViewModel owner, ThemeLayer layer, bool isGroup = false) : ObservableObject
{
    public bool IsGroup => isGroup;
    public bool IsMember => !isGroup && layer.GroupId is not null;
    public System.Windows.Thickness Indent => new(IsMember ? 12 : 0, 0, 0, 0);
    public string ExpandLabel => layer.GroupId is Guid id && owner.IsGroupCollapsed(id) ? "▸" : "▾";
    public ActionCommand ExpandCommand { get; } = new(_ => { if (layer.GroupId is Guid id) owner.ToggleGroupExpanded(id); });
    public bool IsSelected { get => owner.IsLayerSelected(layer.Id); set { if (IsSelected != value) owner.SelectCanvasLayer(layer, true); } }
    public void NotifySelected() => OnPropertyChanged(nameof(IsSelected));
    public ThemeLayer Layer => layer;
    public Uri? PreviewUri
    {
        get
        {
            var path = owner.Scene.Children.GetValueOrDefault(layer.Id) is ImageScene image ? image.Path : layer.Path;
            return !isGroup && layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper && Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile ? uri : null;
        }
    }
    public string KindLabel => isGroup ? "组合" : layer.Kind switch { ThemeElementKind.Wallpaper => "壁纸", ThemeElementKind.Image => "图片", ThemeElementKind.DigitalClock => "时钟", ThemeElementKind.Date => "日期", ThemeElementKind.Weekday => "星期", ThemeElementKind.Text => "文字", ThemeElementKind.Rectangle or ThemeElementKind.Ellipse => "形状", _ => "插件" };
    public string Name { get => isGroup ? layer.GroupName : layer.Name; set { if (isGroup && layer.GroupId is Guid id) owner.RenameGroup(id, value); else owner.RenameLayer(layer.Id, value); } }
    public string Glyph => isGroup ? "▣" : layer.Kind switch { ThemeElementKind.Wallpaper or ThemeElementKind.Image => "▧", ThemeElementKind.DigitalClock or ThemeElementKind.CameraClock => "◷", ThemeElementKind.Date or ThemeElementKind.Weekday => "▦", ThemeElementKind.Text => "T", ThemeElementKind.Rectangle => "□", ThemeElementKind.Ellipse => "○", _ => "⚙" };
    public string VisibilityLabel => (isGroup && layer.GroupId is Guid id ? owner.GroupMembers(id).All(l => l.Visible) : layer.Visible) ? "◉" : "○";
    public string LockLabel => (isGroup && layer.GroupId is Guid id ? owner.GroupMembers(id).All(l => l.Locked) : layer.Locked) ? "锁" : "解";
    public string State => isGroup && layer.GroupId is Guid id ? $"{owner.GroupMembers(id).Count} 个图层" : !layer.Visible ? "已隐藏" : layer.Locked ? "已锁定" : "";
    public bool CanRename => isGroup && layer.GroupId is Guid id ? owner.GroupMembers(id).All(l => !l.Locked) : !layer.Locked;
    public ActionCommand ToggleVisibilityCommand { get; } = new(_ => { if (isGroup && layer.GroupId is Guid id) owner.ToggleGroupState(id, false); else owner.ToggleLayerVisibility(layer.Id); });
    public ActionCommand ToggleLockCommand { get; } = new(_ => { if (isGroup && layer.GroupId is Guid id) owner.ToggleGroupState(id, true); else owner.ToggleLayerLock(layer.Id); });
}

public sealed partial class CreatorViewModel
{
    public IReadOnlyList<CreatorFillChoice> FillChoices { get; } = [new(ImageDisplayMode.Fill, "铺满（裁剪）"), new(ImageDisplayMode.Uniform, "等比完整"), new(ImageDisplayMode.Stretch, "拉伸"), new(ImageDisplayMode.Center, "原尺寸居中")];
    private string _materialSearch = "", _saveFeedback = "尚未保存";
    private bool _saving;
    private ThemeOrientation _reportedOrientation;
    private double _reportedAspect = 4;
    private CreatorMaterialTab _selectedMaterial = new("图片", "▧");
    public IReadOnlyList<CreatorMaterialTab> MaterialChoices { get; } = [new("图片", "▧"), new("时钟", "◷"), new("插件", "⚙"), new("文字", "T"), new("形状", "□")];
    public CreatorMaterialTab ResolveMaterialChoice(string name) => MaterialChoices.FirstOrDefault(m => m.Name == (name is "壁纸" or "我的素材" ? "图片" : name)) ?? MaterialChoices[0];
    public CreatorMaterialTab SelectedMaterial { get => _selectedMaterial; set { if (value is not null && SetProperty(ref _selectedMaterial, value)) MaterialTab = value.Name; } }
    public string MaterialSearch { get => _materialSearch; set { if (SetProperty(ref _materialSearch, value)) NotifyMaterials(); } }
    private IReadOnlyList<WallpaperItemViewModel>? _filteredAssetCache;
    public IReadOnlyList<WallpaperItemViewModel> FilteredAssets => _filteredAssetCache ??= LibraryAssets.Reverse().Where(i => MatchesAsset(i) && (string.IsNullOrWhiteSpace(MaterialSearch) || $"{i.Name} {i.Tags} {i.Category} {i.Subcategory}".Contains(MaterialSearch, StringComparison.OrdinalIgnoreCase))).OrderByDescending(i => i.Asset.AddedAt).ToArray();
    public bool HasFilteredAssets => FilteredAssets.Count > 0;
    public bool IsMaterialEmpty => !HasFilteredAssets;
    public string MaterialEmptyText => LibraryAssets.Count == 0 ? "尚无素材，可导入本地图片或视频。" : "没有匹配的素材。";
    public bool CanEditSelection => CanEdit && IsSingleSelection && SelectedLayer is { Locked: false };
    public bool IsLandscape => Orientation == ThemeOrientation.Landscape;
    public bool IsPortrait => Orientation == ThemeOrientation.Portrait;
    public string LayoutHint => "横竖布局独立保存 · 复制元素不会覆盖另一布局";
    public bool SnapEnabled { get; set; } = true;
    public string SaveFeedback { get => _saveFeedback; private set {if(SetProperty(ref _saveFeedback, value))OnPropertyChanged(nameof(EditorSaveSummary));} }
    public string DirtyIndicator => IsDirty ? "● 未保存" : "";
    public bool IsSaving => _saving;
    public IReadOnlyList<CreatorLayerRow> LayerRows { get; private set; } = [];
    public CreatorLayerRow? SelectedLayerRow { get => LayerRows.FirstOrDefault(r => !r.IsGroup && r.Layer.Id == SelectedLayer?.Id) ?? LayerRows.FirstOrDefault(r => r.Layer.Id == SelectedLayer?.Id); set { if ((!_refreshingLayers || value is not null) && value?.Layer.Id != _selectedId) SelectCanvasLayer(value?.Layer, false); } }
    public ActionCommand CenterLayerCommand { get; private set; } = null!;
    public ActionCommand CopyToOtherLayoutCommand { get; private set; } = null!;
    private LibraryViewModel? _materialLibrary;
    private Guid? _latestMaterialId;
    public int MaterialImportRevision { get; private set; }
    private Guid? LatestMaterialId => LibraryAssets.Reverse().OrderByDescending(i => i.Asset.AddedAt).FirstOrDefault()?.Asset.Id;
    private void MaterialLibraryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LibraryViewModel.AllItems)) return;
        var latest = LatestMaterialId; var changed = latest is not null && latest != _latestMaterialId; _latestMaterialId = latest;
        OnPropertyChanged(nameof(LibraryAssets)); NotifyMaterials();
        if (changed && FilteredAssets.Any(i => i.Asset.Id == latest)) { MaterialImportRevision++; OnPropertyChanged(nameof(MaterialImportRevision)); }
    }
    private void InitializeExperience()
    {
        _materialLibrary = _main.Library; _latestMaterialId = LatestMaterialId; if (_materialLibrary is not null) _materialLibrary.PropertyChanged += MaterialLibraryChanged;
        CenterLayerCommand = new(_ => { if (SelectedLayer is { } l) SetGeometry(new((Project.ActiveLayout.Width - l.Width) / 2, (Project.ActiveLayout.Height - l.Height) / 2, l.Width, l.Height)); }, _ => CanEditSelection);
        CopyToOtherLayoutCommand = new(_ => CopyToOtherLayout(), _ => CanEdit && IsSingleSelection && SelectedLayer is not null);
    }
    private void NotifyMaterials() { _filteredAssetCache = null; foreach (var p in new[] { nameof(FilteredAssets), nameof(HasFilteredAssets), nameof(IsMaterialEmpty), nameof(MaterialEmptyText), nameof(AssetCategories), nameof(AssetSubcategories) }) OnPropertyChanged(p); }
    private void NotifyLayout()
    {
        if (_reportedOrientation == Orientation && _reportedAspect == Aspect) return;
        _reportedOrientation = Orientation; _reportedAspect = Aspect;
        foreach (var p in new[] { nameof(Orientation), nameof(Aspect), nameof(IsLandscape), nameof(IsPortrait), nameof(OrientationLabel) }) OnPropertyChanged(p);
    }
    public void RenameLayer(Guid id, string name)
    {
        var layer = Project.ActiveLayout.Layers.FirstOrDefault(l => l.Id == id);
        if (!CanEdit || layer is null || layer.Locked || string.IsNullOrWhiteSpace(name) || name.Length > 160 || name == layer.Name) return;
        Change(() => layer.Name = name.Trim());
    }
    public void ToggleLayerVisibility(Guid id) { var layer = Project.ActiveLayout.Layers.FirstOrDefault(l => l.Id == id); if (CanEdit && layer is not null) Change(() => layer.Visible = !layer.Visible); }
    public void ToggleLayerLock(Guid id) { var layer = Project.ActiveLayout.Layers.FirstOrDefault(l => l.Id == id); if (CanEdit && layer is not null) Change(() => layer.Locked = !layer.Locked); }
    public void SetGeometry(ThemeBounds bounds)
    {
        if (!CanEditSelection || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)) return;
        if (SelectedLayer is { } l && l.X == bounds.X && l.Y == bounds.Y && l.Width == bounds.Width && l.Height == bounds.Height) return;
        Change(() => { var layer = SelectedLayer!; layer.X = Math.Clamp(bounds.X, -32768, 32768); layer.Y = Math.Clamp(bounds.Y, -32768, 32768); layer.Width = Math.Clamp(bounds.Width, 1, 16384); layer.Height = Math.Clamp(bounds.Height, 1, 16384); });
    }
    public void CopyToOtherLayout()
    {
        if (!CanEdit || SelectedLayer is not { } selected) return;
        var source = Project.ActiveLayout; var target = IsLandscape ? Project.Portrait : Project.Landscape;
        if (target.Layers.Count >= 100) { Status = "另一布局已达到100个图层上限。"; return; }
        var copy = CloneLayer(selected); copy.Id = Guid.NewGuid(); copy.GroupId = null; copy.Name += " 副本";
        var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
        copy.Width = Math.Max(1, copy.Width * scale); copy.Height = Math.Max(1, copy.Height * scale); copy.FontSize = Math.Clamp(copy.FontSize * scale, 1, 2000); copy.Spacing *= scale;
        copy.X = (selected.X + selected.Width / 2) / source.Width * target.Width - copy.Width / 2;
        copy.Y = (selected.Y + selected.Height / 2) / source.Height * target.Height - copy.Height / 2;
        copy.ZIndex = target.Layers.Select(l => l.ZIndex).DefaultIfEmpty(0).Max() + 1;
        Change(() => target.Layers.Add(copy)); Status = "已复制到另一布局，原有图层保持不变。";
    }
}
