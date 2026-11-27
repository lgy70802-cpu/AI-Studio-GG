using DeskDisplay.Core.Models;
using System.IO;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    private string _projectOrientation = "全部方向", _assetSubcategory = "全部子分类";
    private bool _lockAspect;
    public string ProjectOrientation { get => _projectOrientation; set { if (SetProperty(ref _projectOrientation, value)) OnPropertyChanged(nameof(VisibleProjects)); } }
    public bool LockAspect { get => _lockAspect; set => SetProperty(ref _lockAspect, value); }
    private void SetConstrainedSize(double width, double height)
    {
        if (SelectedLayer is not { } l || !double.IsFinite(width) || !double.IsFinite(height) || width is < 1 or > 16384 || height is < 1 or > 16384) { Status = "尺寸已达到范围限制。"; return; }
        SetGeometry(new(l.X, l.Y, width, height));
    }
    public string AssetSubcategory { get => _assetSubcategory; set { if (SetProperty(ref _assetSubcategory, value)) NotifyMaterials(); } }
    public IReadOnlyList<string> AssetSubcategories => new[] { "全部子分类" }.Concat(LibraryAssets.Where(i => AssetCategory == "全部分类" || i.Category == AssetCategory).Select(i => i.Subcategory).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order()).ToArray();
    public ActionCommand GroupCommand { get; private set; } = null!;
    public ActionCommand UngroupCommand { get; private set; } = null!;
    public AsyncCommand SaveAsCommand { get; private set; } = null!;
    public bool CanScaleSelection => CanEdit && SelectedLayers.Count > 1 && SelectedLayers.All(l => !l.Locked);
    public ThemeBounds SelectionBounds => ThemeGeometry.Union(SelectedLayers);
    private void InitializeAdvanced()
    {
        GroupCommand = new(_ => GroupSelection(), _ => CanScaleSelection);
        UngroupCommand = new(_ => UngroupSelection(), _ => CanEdit && SelectedLayers.Any(l => l.GroupId is not null) && SelectedLayers.All(l => !l.Locked));
        SaveAsCommand = new(() => RunAsync(SaveAsAsync), () => CanEdit && _editing, _logger);
    }
    public void GroupSelection()
    {
        if (!CanScaleSelection) return;
        var members = SelectedLayers.ToArray(); var group = Guid.NewGuid();
        Change(() => { foreach (var l in members) l.GroupId = group; }); Status = "已组合，可通过撤销恢复。";
    }
    public void UngroupSelection()
    {
        if (!CanEdit || SelectedLayers.Any(l => l.Locked)) return;
        var groups = SelectedLayers.Where(l => l.GroupId is not null).Select(l => l.GroupId).ToHashSet();
        if (groups.Count == 0) return;
        Change(() => { foreach (var l in Project.ActiveLayout.Layers.Where(l => groups.Contains(l.GroupId))) l.GroupId = null; }); Status = "已取消组合。";
    }
    public void ScaleSelection(IReadOnlyList<ThemeLayer> originals, ThemeBounds target, bool preserveAspect = true)
    {
        if (!CanScaleSelection || originals.Count != SelectedLayers.Count || originals.Any(l => !IsLayerSelected(l.Id))) return;
        var scaled = ThemeGeometry.ScaleGroup(originals, target, preserveAspect);
        if (scaled is null) { Status = "缩放已达到图层尺寸或文字范围限制。"; return; }
        Change(() => { foreach (var result in scaled) { var l = Project.ActiveLayout.Layers.Single(l => l.Id == result.Id); l.X = result.X; l.Y = result.Y; l.Width = result.Width; l.Height = result.Height; l.FontSize = result.FontSize; l.Spacing = result.Spacing; l.Fill = result.Fill; } });
    }
    public void ResizeLayerFromCorner(ThemeLayer original, ThemeBounds target, bool preserveAspect = true)
    {
        if (!CanEditSelection || SelectedLayer is not { Locked: false } layer || original.Id != layer.Id) return;
        if (!double.IsFinite(target.X) || !double.IsFinite(target.Y) || !double.IsFinite(target.Width) || !double.IsFinite(target.Height) || target.Width is < 1 or > 16384 || target.Height is < 1 or > 16384 || Math.Abs(target.X) > 32768 || Math.Abs(target.Y) > 32768) return;
        var text = layer.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday;
        var factor = Math.Min(target.Width / original.Width, target.Height / original.Height);
        var font = text ? original.FontSize * factor : layer.FontSize;
        var spacing = text ? original.Spacing * factor : layer.Spacing;
        if (!double.IsFinite(font) || font is < 1 or > 2000 || !double.IsFinite(spacing) || Math.Abs(spacing) > 200) { Status = "缩放已达到文字范围限制。"; return; }
        var fill = !preserveAspect && layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper ? ImageDisplayMode.Stretch : original.Fill;
        if (layer.Fill == fill && layer.X == target.X && layer.Y == target.Y && layer.Width == target.Width && layer.Height == target.Height && layer.FontSize == font && layer.Spacing == spacing) return;
        Change(() => { layer.X = target.X; layer.Y = target.Y; layer.Width = target.Width; layer.Height = target.Height; layer.FontSize = font; layer.Spacing = spacing; layer.Fill = fill; });
    }
    public async Task SaveAsAsync()
    {
        var copy = Project.Clone(); copy.Id = Guid.NewGuid(); copy.Name = copy.Name[..Math.Min(copy.Name.Length, 156)] + " 新副本";
        await OpenProjectAsync(copy); _dirty = true; Changed(false); await SaveAsync(); Status = "已另存为新项目，原项目保留。";
    }
    public async Task ReplaceSelectedAssetAsync(WallpaperAsset asset, bool keepCrop = true)
    {
        if (!CanEditSelection || !IsImage || SelectedLayer is not { } l) throw new InvalidOperationException("请先单选图片或壁纸图层。");
        if (!File.Exists(asset.Path)) throw new FileNotFoundException("素材文件不存在。", asset.Path);
        Change(() => { l.AssetId = asset.Id; l.Path = asset.Path; l.Name = asset.Name; l.IsVideo = asset.IsVideo; l.AssetOrigin = string.IsNullOrWhiteSpace(asset.CloudResourceId) ? ThemeAssetOrigin.Local : ThemeAssetOrigin.Cloud; l.CloudResourceId = asset.CloudResourceId; if (!keepCrop) { l.ImageZoom = 1; l.CropX = l.CropY = .5; } });
        await ReloadAssetsAsync(); Status = "已替换素材，位置与样式保持不变。";
    }
    private IEnumerable<Guid> ExpandGroups(IEnumerable<Guid> ids)
    {
        var selected = ids.ToHashSet(); var groups = Project.ActiveLayout.Layers.Where(l => selected.Contains(l.Id) && l.GroupId is not null).Select(l => l.GroupId).ToHashSet();
        return Project.ActiveLayout.Layers.Where(l => selected.Contains(l.Id) || l.GroupId is not null && groups.Contains(l.GroupId)).Select(l => l.Id).ToArray();
    }
    private void NotifyAdvanced()
    {
        NotifyFinishing();
        GroupCommand?.Notify(); UngroupCommand?.Notify(); SaveAsCommand?.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanScaleSelection)); OnPropertyChanged(nameof(SelectionBounds));
    }
}
