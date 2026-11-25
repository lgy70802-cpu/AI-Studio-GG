using DeskDisplay.Core.Models;
using System.IO;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    private readonly HashSet<Guid> _selection = [];
    private ThemeLayer[] _copiedLayers = [];
    private string _assetOrientation = "全部方向", _assetCategory = "全部分类", _assetOrigin = "全部来源", _projectSort = "最近修改", _projectSearch = "";
    private ThemeCard? _renamingProject;
    private string _renameProjectText = "";
    public IReadOnlyList<ThemeLayer> SelectedLayers => Project.ActiveLayout.Layers.Where(l => _selection.Contains(l.Id) || l.Id == _selectedId).ToArray();
    public bool IsLayerSelected(Guid id) => _selection.Contains(id) || _selectedId == id;
    public int SelectionCount => SelectedLayers.Count;
    public string SelectionSummary => SelectionCount > 1 ? $"已选择 {SelectionCount} 个图层" : "";
    public bool IsSingleSelection => SelectionCount == 1;
    public bool IsMultiSelection => SelectionCount > 1;
    public ActionCommand ArrangeCommand { get; private set; } = null!;
    public ActionCommand RenameProjectCommand { get; private set; } = null!;
    public ActionCommand ConfirmRenameCommand { get; private set; } = null!;
    public ActionCommand CancelRenameCommand { get; private set; } = null!;
    public bool IsRenamingProject => _renamingProject is not null;
    public string RenameProjectText { get => _renameProjectText; set { if (SetProperty(ref _renameProjectText, value)) ConfirmRenameCommand.Notify(); } }
    public IReadOnlyList<string> AssetOrientations { get; } = ["全部方向", "横屏 4:1", "竖屏 1:4"];
    public IReadOnlyList<string> AssetOrigins { get; } = ["全部来源", "本地素材", "云端已下载"];
    public IReadOnlyList<string> AssetCategories => new[] { "全部分类" }.Concat(LibraryAssets.Select(i => i.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order()).ToArray();
    public string AssetOrientation { get => _assetOrientation; set { if (SetProperty(ref _assetOrientation, value)) NotifyMaterials(); } }
    public string AssetCategory { get => _assetCategory; set { if (SetProperty(ref _assetCategory, value)) { _assetSubcategory = "全部子分类"; OnPropertyChanged(nameof(AssetSubcategory)); NotifyMaterials(); } } }
    public string AssetOrigin { get => _assetOrigin; set { if (SetProperty(ref _assetOrigin, value)) NotifyMaterials(); } }
    public IReadOnlyList<string> ProjectSorts { get; } = ["最近修改", "名称升序", "横屏优先", "竖屏优先"];
    public string ProjectSort { get => _projectSort; set { if (SetProperty(ref _projectSort, value)) OnPropertyChanged(nameof(VisibleProjects)); } }
    public string ProjectSearch { get => _projectSearch; set { if (SetProperty(ref _projectSearch, value)) OnPropertyChanged(nameof(VisibleProjects)); } }
    public IReadOnlyList<ThemeCard> VisibleProjects
    {
        get
        {
            var items = Projects.Where(p => (ProjectCategoryFilter == "全部分类" || p.Project.Category == ProjectCategoryFilter) && (p.Name.Contains(ProjectSearch, StringComparison.OrdinalIgnoreCase) || p.Project.Category.Contains(ProjectSearch, StringComparison.OrdinalIgnoreCase)) && (ProjectOrientation == "全部方向" || (p.Project.Orientation == ThemeOrientation.Portrait) == (ProjectOrientation == "竖屏 1:4")));
            return (ProjectSort switch { "名称升序" => items.OrderBy(p => p.Name), "横屏优先" => items.OrderBy(p => p.Project.Orientation).ThenByDescending(p => p.Project.ModifiedAt), "竖屏优先" => items.OrderByDescending(p => p.Project.Orientation).ThenByDescending(p => p.Project.ModifiedAt), _ => items.OrderByDescending(p => p.Project.ModifiedAt) }).ToArray();
        }
    }
    public IReadOnlyList<string> ColorPresets { get; } = ["#FFFFFF", "#202C43", "#4C85F6", "#8C9AB1", "#31B98D", "#FFBF69"];
    private bool MatchesAsset(WallpaperItemViewModel i) =>
        (AssetOrientation == "全部方向" || i.IsPortrait == (AssetOrientation == "竖屏 1:4")) &&
        (AssetCategory == "全部分类" || i.Category == AssetCategory) &&
        (AssetSubcategory == "全部子分类" || i.Subcategory == AssetSubcategory) &&
        (AssetOrigin == "全部来源" || string.IsNullOrWhiteSpace(i.Asset.CloudResourceId) == (AssetOrigin == "本地素材"));
    private void InitializeEditingTools()
    {
        ArrangeCommand = new(p => ArrangeSelection(p?.ToString() ?? ""), _ => CanEdit && SelectedLayers.Count(l => !l.Locked) >= 2);
        RenameProjectCommand = new(p => { if (p is ThemeCard card) { _renamingProject = card; RenameProjectText = card.Name; OnPropertyChanged(nameof(IsRenamingProject)); } }, _ => CanEdit);
        ConfirmRenameCommand = new(_ => { if (_renamingProject is { } card) Track(RunAsync(async () => { await RenameProjectAsync(card.Project.Id, RenameProjectText); _renamingProject = null; OnPropertyChanged(nameof(IsRenamingProject)); })); }, _ => CanEdit && !string.IsNullOrWhiteSpace(RenameProjectText) && RenameProjectText.Trim().Length <= 160);
        CancelRenameCommand = new(_ => { _renamingProject = null; OnPropertyChanged(nameof(IsRenamingProject)); });
    }
    public async Task RenameProjectAsync(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) throw new ArgumentException("名称需要1至160个字符。");
        var project = (await _store.ListAsync()).Single(p => p.Id == id);
        project.Name = name.Trim(); project.ModifiedAt = DateTimeOffset.Now; await _store.SaveAsync(project); await RefreshProjectsAsync();
    }
    public async Task DeleteSavedProjectAsync(Guid id) { await _store.DeleteAsync(id); await RefreshProjectsAsync(); }
    public async Task DuplicateSavedProjectAsync(ThemeCard card)
    {
        var copy = card.Project.Clone(); copy.Id = Guid.NewGuid(); copy.Name = card.Name[..Math.Min(card.Name.Length, 157)] + " 副本"; copy.ModifiedAt = DateTimeOffset.Now;
        await _store.SaveAsync(copy); if (File.Exists(card.CoverPath)) File.Copy(card.CoverPath, _store.CoverPath(copy.Id)); await RefreshProjectsAsync();
    }
    public void SelectLayers(IEnumerable<Guid> ids)
    {
        var expanded = ExpandGroups(ids); _selection.Clear(); foreach (var id in expanded.Where(id => Project.ActiveLayout.Layers.Any(l => l.Id == id))) _selection.Add(id);
        _selectedId = Project.ActiveLayout.Layers.LastOrDefault(l => _selection.Contains(l.Id))?.Id; NotifySelection();
    }
    public void SelectCanvasLayer(ThemeLayer? layer, bool additive)
    {
        if (!additive) { SelectLayers(layer is null ? [] : new[] { layer.Id }); return; }
        var ids = SelectedLayers.Select(l => l.Id).ToHashSet();
        if (layer is not null) { var members = ExpandGroups([layer.Id]); if (ids.Contains(layer.Id)) ids.ExceptWith(members); else ids.UnionWith(members); }
        SelectLayers(ids);
    }
    public void FocusSelectedLayer(ThemeLayer layer)
    {
        if (!IsLayerSelected(layer.Id)) return;
        _selectedId = layer.Id; NotifySelection();
    }
    public void SelectRegion(double x, double y, double width, double height, bool additive)
    {
        var ids = additive ? SelectedLayers.Select(l => l.Id).ToHashSet() : [];
        foreach (var l in Project.ActiveLayout.Layers.Where(l => l.Visible && !l.Locked && l.Kind != ThemeElementKind.Wallpaper))
        {
            var b = ThemeGeometry.VisualBounds(l);
            if (b.X >= x && b.Y >= y && b.X + b.Width <= x + width && b.Y + b.Height <= y + height) ids.Add(l.Id);
        }
        SelectLayers(ids);
    }
    public void MoveSelection(double dx, double dy)
    {
        var layers = SelectedLayers.Where(l => !l.Locked).ToArray();
        if (!CanEdit || layers.Length == 0 || !double.IsFinite(dx) || !double.IsFinite(dy) || dx == 0 && dy == 0) return;
        // Clamp one shared delta to preserve relative spacing at the coordinate limits.
        dx = Math.Clamp(dx, layers.Max(l => -32768 - l.X), layers.Min(l => 32768 - l.X));
        dy = Math.Clamp(dy, layers.Max(l => -32768 - l.Y), layers.Min(l => 32768 - l.Y));
        Change(() => { foreach (var l in layers) { l.X += dx; l.Y += dy; } });
    }
    public void ArrangeSelection(string operation)
    {
        var layers = SelectedLayers.Where(l => !l.Locked).ToArray();
        if (!CanEdit || layers.Length < 2) return;
        var bounds = layers.ToDictionary(l => l.Id, ThemeGeometry.VisualBounds);
        var left = bounds.Values.Min(b => b.X); var top = bounds.Values.Min(b => b.Y);
        var right = bounds.Values.Max(b => b.X + b.Width); var bottom = bounds.Values.Max(b => b.Y + b.Height);
        if (operation is "水平等距" or "垂直等距")
        {
            if (layers.Length < 3) { Status = "等距分布至少需要3个未锁定图层。"; return; }
            var horizontal = operation == "水平等距";
            var sorted = layers.OrderBy(l => horizontal ? bounds[l.Id].X : bounds[l.Id].Y).ToArray();
            var span = horizontal ? right - left : bottom - top;
            var gap = (span - sorted.Sum(l => horizontal ? bounds[l.Id].Width : bounds[l.Id].Height)) / (layers.Length - 1);
            var cursor = horizontal ? left : top;
            Change(() => { foreach (var l in sorted) { var b = bounds[l.Id]; if (horizontal) l.X += cursor - b.X; else l.Y += cursor - b.Y; cursor += (horizontal ? b.Width : b.Height) + gap; } }); return;
        }
        if (operation is not ("左对齐" or "水平居中" or "右对齐" or "顶对齐" or "垂直居中" or "底对齐")) return;
        Change(() => { foreach (var l in layers) { var b = bounds[l.Id]; switch (operation) { case "左对齐": l.X += left - b.X; break; case "水平居中": l.X += (left + right - b.Width) / 2 - b.X; break; case "右对齐": l.X += right - b.X - b.Width; break; case "顶对齐": l.Y += top - b.Y; break; case "垂直居中": l.Y += (top + bottom - b.Height) / 2 - b.Y; break; case "底对齐": l.Y += bottom - b.Y - b.Height; break; } } });
    }
    private void CopySelection() { _copiedLayers = SelectedLayers.Select(CloneLayer).ToArray(); _clipboard = _copiedLayers.FirstOrDefault(); PasteCommand.Notify(); }
    private void PasteSelection()
    {
        var copies = _copiedLayers.Length > 0 ? _copiedLayers.Select(CloneLayer).ToArray() : _clipboard is null ? [] : new[] { CloneLayer(_clipboard) };
        if (copies.Length == 0 || Project.ActiveLayout.Layers.Count + copies.Length > 100) { Status = "粘贴后不能超过100个图层。"; return; }
        var groupMap = copies.Where(l => l.GroupId is not null).Select(l => l.GroupId!.Value).Distinct().ToDictionary(g => g, _ => Guid.NewGuid());
        Change(() => { var z = Project.ActiveLayout.Layers.Select(l => l.ZIndex).DefaultIfEmpty(0).Max(); foreach (var l in copies) { l.Id = Guid.NewGuid(); if (l.GroupId is Guid group) l.GroupId = groupMap[group]; l.X = Math.Clamp(l.X + 20, -32768, 32768); l.Y = Math.Clamp(l.Y + 20, -32768, 32768); l.ZIndex = ++z; Project.ActiveLayout.Layers.Add(l); } });
        SelectLayers(copies.Select(l => l.Id)); Track(ReloadAssetsAsync());
    }
    private void DeleteSelection()
    {
        var ids = SelectedLayers.Where(l => !l.Locked).Select(l => l.Id).ToHashSet();
        if (ids.Count == 0) return;
        Change(() => Project.ActiveLayout.Layers.RemoveAll(l => ids.Contains(l.Id))); SelectLayers([]); RefreshLayers(); Track(ReloadAssetsAsync());
    }
    private void NotifyEditingTools()
    {
        NotifyAdvanced();
        foreach (var row in LayerRows) row.NotifySelected();
        foreach (var p in new[] { nameof(SelectedLayers), nameof(SelectionCount), nameof(SelectionSummary), nameof(IsMultiSelection), nameof(IsSingleSelection) }) OnPropertyChanged(p);
        ArrangeCommand?.Notify(); RenameProjectCommand?.Notify(); ConfirmRenameCommand?.Notify();
    }
}
