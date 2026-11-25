using DeskDisplay.Core.Models;
using System.Windows.Media;

namespace DeskDisplay.App.ViewModels;

public enum CreatorLeaveChoice { Save, Discard, Cancel }

public sealed partial class CreatorViewModel
{
    private int _leftTabIndex;
    public int LeftTabIndex { get => _leftTabIndex; set => SetProperty(ref _leftTabIndex, Math.Clamp(value, 0, 1)); }
    public bool NoSelection => !HasSelection;
    public bool SupportsSharedFont => IsMultiSelection && SelectedLayers.All(l => l.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday);
    public bool SupportsSharedColor => IsMultiSelection && SelectedLayers.All(l => l.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday or ThemeElementKind.Rectangle or ThemeElementKind.Ellipse);
    public string PropertyTitle => IsMultiSelection ? $"{SelectionCount} 个图层" : SelectedLayer?.Name ?? "画布";
    public string GeometryFeedback { get { var b = SelectionBounds; return HasSelection ? $"X {b.X:0.#}  Y {b.Y:0.#}   {b.Width:0.#} × {b.Height:0.#}" : LayoutDimensions; } }
    public string LandscapeLayoutLabel => $"横屏 {Project.Landscape.Width:0}×{Project.Landscape.Height:0}";
    public string PortraitLayoutLabel => $"竖屏 {Project.Portrait.Width:0}×{Project.Portrait.Height:0}";
    public double CanvasWidth { get => Project.ActiveLayout.Width; set => ResizeCanvas(value, CanvasHeight); }
    public double CanvasHeight { get => Project.ActiveLayout.Height; set => ResizeCanvas(CanvasWidth, value); }
    public string CanvasBackground
    {
        get => Project.ActiveLayout.Background;
        set
        {
            if (value == CanvasBackground || string.IsNullOrWhiteSpace(value)) return;
            try { _ = ColorConverter.ConvertFromString(value); Change(() => Project.ActiveLayout.Background = value); }
            catch (Exception e) when (e is FormatException or NotSupportedException) { Status = "请输入有效画布颜色，例如 #152239。"; }
        }
    }
    private void ResizeCanvas(double width, double height)
    {
        if (!CanEdit || !double.IsFinite(width) || !double.IsFinite(height) || width is < 1 or > 8192 || height is < 1 or > 8192 || width == CanvasWidth && height == CanvasHeight) return;
        // A layout keeps its orientation. Create an independent layout to change orientation.
        if ((width >= height) != (Orientation == ThemeOrientation.Landscape)) { Status = "请切换或创建另一方向的布局。"; return; }
        LeavingEditorLayout?.Invoke(this, EventArgs.Empty);
        Change(() => { var l = Project.ActiveLayout; l.Width = width; l.Height = height; l.VerticalGuides.RemoveAll(v => v > width); l.HorizontalGuides.RemoveAll(v => v > height); });
    }
    public Func<ThemeProject, CreatorLeaveChoice>? ConfirmLeave { get; set; }
    private async Task ResolveUnsavedAsync()
    {
        if (!_dirty) return;
        var choice = ConfirmLeave?.Invoke(Project) ?? CreatorLeaveChoice.Save;
        if (choice == CreatorLeaveChoice.Cancel) throw new OperationCanceledException("已取消离开，编辑内容已保留。");
        if (choice == CreatorLeaveChoice.Save) { await SaveAsync(); return; }
        // Wait out any in-flight recovery write before explicitly removing the discarded draft.
        try { await _draftTask; } catch (Exception e) { _logger.Warning(e, "Draft write failed before explicit discard"); }
        await _store.DeleteDraftAsync(Project.Id);
        await RefreshDraftsAsync();
        _dirty = false; SaveFeedback = "已放弃本次未保存修改"; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DirtyIndicator));
    }
    private void NotifyUsability()
    {
        foreach (var name in new[] { nameof(NoSelection), nameof(SupportsSharedFont), nameof(SupportsSharedColor), nameof(PropertyTitle), nameof(GeometryFeedback), nameof(CanvasWidth), nameof(CanvasHeight), nameof(CanvasBackground), nameof(LandscapeLayoutLabel), nameof(PortraitLayoutLabel) }) OnPropertyChanged(name);
    }
}
