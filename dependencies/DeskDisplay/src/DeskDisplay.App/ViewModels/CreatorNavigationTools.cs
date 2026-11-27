using DeskDisplay.Core.Models;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    public event EventHandler? LeavingEditorLayout;
    public ThemeEditorViewport? EditorViewport { get; private set; }
    public int ViewportRevision { get; private set; }
    private Task _viewportTask = Task.CompletedTask;
    private readonly Dictionary<(Guid, ThemeOrientation, Guid?), long> _viewportVersions = [];
    private long _viewportVersion;
    private bool _restoringEditorState;
    public ActionCommand LocateOutsideCommand { get; private set; } = null!;
    public ActionCommand BringInsideCommand { get; private set; } = null!;
    public ActionCommand BringFrontCommand { get; private set; } = null!;
    public ActionCommand SendBackCommand { get; private set; } = null!;
    public event EventHandler? FocusCanvasSelection;
    private void InitializeNavigationTools()
    {
        LocateOutsideCommand = new(_ => LocateOutside(), _ => CanEdit && _editing);
        BringInsideCommand = new(_ => BringSelectionInside(), _ => CanEdit && SelectedLayers.Count > 0 && SelectedLayers.All(l => !l.Locked));
        BringFrontCommand = new(_ => MoveSelectionToEdge(true), _ => CanStackSelection);
        SendBackCommand = new(_ => MoveSelectionToEdge(false), _ => CanStackSelection);
    }
    public void CaptureEditorViewport(double zoom, double horizontal, double vertical, ThemeWorkbenchState? workbench = null)
    {
        if (_disposed || !_editing || _restoringEditorState) return;
        var state = new ThemeEditorViewport(Project.Id, Orientation, zoom, horizontal, vertical, SelectedLayers.Select(l => l.Id).ToArray(), SelectedLayer?.Id, Project.SelectedLayoutId) { Workbench = workbench ?? EditorViewport?.Workbench };
        state.Validate();
        if (EditorViewport is { } old && old.ProjectId == state.ProjectId && old.Orientation == state.Orientation && old.LayoutId == state.LayoutId && old.Zoom == state.Zoom && old.Horizontal == state.Horizontal && old.Vertical == state.Vertical && old.FocusedLayer == state.FocusedLayer && old.Selection.SequenceEqual(state.Selection) && System.Text.Json.JsonSerializer.Serialize(old.Workbench) == System.Text.Json.JsonSerializer.Serialize(state.Workbench)) return;
        EditorViewport = state;
        var revision = ++_viewportVersion; _viewportVersions[(state.ProjectId, state.Orientation, state.LayoutId)] = revision;
        _viewportTask = SaveViewportLaterAsync(state, revision, _viewportTask);
    }
    private async Task SaveViewportLaterAsync(ThemeEditorViewport snapshot, long revision, Task previous)
    {
        try
        {
            await Task.Delay(250);
            if (_disposed || _viewportVersions[(snapshot.ProjectId, snapshot.Orientation, snapshot.LayoutId)] != revision) return;
            await previous;
            if (!_disposed && _viewportVersions[(snapshot.ProjectId, snapshot.Orientation, snapshot.LayoutId)] == revision) await _store.SaveViewportAsync(snapshot);
        }
        catch (Exception e) { _logger.Warning(e, "Cannot persist creator viewport"); }
    }
    public async Task FlushEditorViewportAsync()
    {
        while (true) { var pending = _viewportTask; await pending; if (ReferenceEquals(pending, _viewportTask)) return; }
    }
    private async Task RestoreLayoutViewportAsync() { await FlushEditorViewportAsync(); await RestoreEditorViewportAsync(); await ReloadAssetsAsync(); }
    private async Task RestoreEditorViewportAsync()
    {
        var id = Project.Id; var orientation = Orientation; var layoutId = Project.SelectedLayoutId; var state = await _store.ReadViewportAsync(id, orientation, layoutId);
        if (id != Project.Id || orientation != Orientation || layoutId != Project.SelectedLayoutId || _disposed) return;
        _restoringEditorState = true;
        try {
        EditorViewport = state;
        if (state is not null)
        {
            if (state.Workbench is { } workbench) { _collapsedGroups.Clear(); foreach (var group in workbench.CollapsedGroups) _collapsedGroups.Add(group); LeftTabIndex = workbench.LeftTab; SelectedMaterial = ResolveMaterialChoice(workbench.Material); RefreshLayers(); }
            _selection.Clear(); foreach (var layerId in state.Selection.Where(candidate => Project.ActiveLayout.Layers.Any(l => l.Id == candidate))) _selection.Add(layerId);
            _selectedId = state.FocusedLayer is Guid focus && _selection.Contains(focus) ? focus : Project.ActiveLayout.Layers.LastOrDefault(l => _selection.Contains(l.Id))?.Id;
            NotifySelection();
        }
        ViewportRevision++; OnPropertyChanged(nameof(ViewportRevision));
        } finally { _restoringEditorState = false; }
    }
    public void LocateOutside()
    {
        var layout = Project.ActiveLayout;
        var outside = Layers.Where(l => { var b = ThemeGeometry.VisualBounds(l); return b.X < 0 || b.Y < 0 || b.X + b.Width > layout.Width || b.Y + b.Height > layout.Height; }).ToArray();
        if (outside.Length == 0) { Status = "没有画布外元素。"; return; }
        var layer = outside[(Array.FindIndex(outside, l => l.Id == SelectedLayer?.Id) + 1) % outside.Length];
        if (layer.GroupId is Guid group) _collapsedGroups.Remove(group);
        RefreshLayers(); SelectLayers([layer.Id]); FocusSelectedLayer(layer); FocusCanvasSelection?.Invoke(this, EventArgs.Empty);
        Status = "已定位画布外元素：" + layer.Name + (layer.Locked ? "（已锁定）" : "，可移回画布");
    }
    public void BringSelectionInside()
    {
        var members = SelectedLayers; if (!CanEdit || members.Count == 0 || members.Any(l => l.Locked)) return;
        var bounds = ThemeGeometry.Union(members); var layout = Project.ActiveLayout;
        var x = bounds.Width > layout.Width ? (layout.Width - bounds.Width) / 2 : Math.Clamp(bounds.X, 0, layout.Width - bounds.Width);
        var y = bounds.Height > layout.Height ? (layout.Height - bounds.Height) / 2 : Math.Clamp(bounds.Y, 0, layout.Height - bounds.Height);
        MoveSelection(x - bounds.X, y - bounds.Y); FocusCanvasSelection?.Invoke(this, EventArgs.Empty);
        Status = "已移回画布，保留尺寸与相对位置。";
    }
    private bool CanStackSelection
    {
        get { var ids = ExpandGroups(SelectedLayers.Select(l => l.Id)).ToHashSet(); return CanEdit && ids.Count > 0 && Project.ActiveLayout.Layers.Where(l => ids.Contains(l.Id)).All(l => !l.Locked); }
    }
    public void MoveSelectionToEdge(bool front)
    {
        var selected = ExpandGroups(SelectedLayers.Select(l => l.Id)).ToHashSet(); var list = Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).ToList();
        var block = list.Where(l => selected.Contains(l.Id)).ToArray(); if (!CanEdit || block.Length == 0 || block.Any(l => l.Locked)) return;
        var rest = list.Where(l => !selected.Contains(l.Id)).ToArray(); var ordered = front ? rest.Concat(block).ToArray() : block.Concat(rest).ToArray();
        if (list.SequenceEqual(ordered)) return;
        Change(() => { for (var i = 0; i < ordered.Length; i++) ordered[i].ZIndex = i; });
    }
    private void NotifyNavigationTools() { LocateOutsideCommand?.Notify(); BringInsideCommand?.Notify(); BringFrontCommand?.Notify(); SendBackCommand?.Notify(); }
}

