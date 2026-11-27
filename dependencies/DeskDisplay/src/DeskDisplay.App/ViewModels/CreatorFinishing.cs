using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    private readonly HashSet<Guid> _collapsedGroups = [];
    private ThemeLayer? _styleClipboard;
    private double _groupAngle;
    public double GroupAngle { get => _groupAngle; set { if (SetProperty(ref _groupAngle, value)) RotateGroupCommand.Notify(); } }
    public ActionCommand RotateGroupCommand { get; private set; } = null!;
    public ActionCommand AlignCanvasCommand { get; private set; } = null!;
    public ActionCommand CopyStyleCommand { get; private set; } = null!;
    public ActionCommand PasteStyleCommand { get; private set; } = null!;
    public string SnapFeedback { get; private set; } = "";
    public string SelectionSizeLabel { get { var b = SelectionBounds; return $"设计范围：{b.Width:0.#} × {b.Height:0.#}"; } }
    public string EffectiveUsbGeometry
    {
        get
        {
            if (_main.Usb is not { } usb || !int.TryParse(usb.Width, out var width) || !int.TryParse(usb.Height, out var height) || width < 1 || height < 1 || usb.Rotation is not (0 or 90 or 180 or 270)) return "请先配置设备输出参数。";
            var g = ThemeOutputGeometry.Resolve(width, height, usb.Rotation, Orientation);
            return $"主题输出：{g.Width} × {g.Height} · 旋转 {g.Rotation}°（不覆盖设备配置）";
        }
    }
    // Per-editor browser state survives closing/reopening and menu navigation without a second asset store.
    public int MaterialBrowserPage { get; set; } = 1;
    public double MaterialBrowserOffset { get; set; }
    public Guid? MaterialBrowserSelection { get; set; }
    public string MaterialBrowserFilterKey => $"{MaterialSearch}\0{AssetOrientation}\0{AssetOrigin}\0{AssetCategory}\0{AssetSubcategory}";
    public string MaterialBrowserSavedFilter { get; set; } = "";
    public void TrimMaterialImages(IEnumerable<WallpaperItemViewModel> active) => _main.Library?.TrimTransientImages(active);
    private void InitializeFinishing()
    {
        RotateGroupCommand = new(_ => RotateSelection(GroupAngle), _ => CanScaleSelection && double.IsFinite(GroupAngle));
        AlignCanvasCommand = new(p => AlignToCanvas(p?.ToString() ?? ""), _ => CanEdit && SelectedLayers.Any(l => !l.Locked));
        CopyStyleCommand = new(_ => { _styleClipboard = SelectedLayer!.Clone(); PasteStyleCommand.Notify(); }, _ => IsSingleSelection);
        PasteStyleCommand = new(_ => PasteStyle(), _ => CanEdit && _styleClipboard is not null && SelectedLayers.Any(l => !l.Locked));
    }
    private IEnumerable<CreatorLayerRow> BuildLayerRows()
    {
        _collapsedGroups.IntersectWith(Project.ActiveLayout.Layers.Where(l => l.GroupId is not null).Select(l => l.GroupId!.Value));
        var seen = new HashSet<Guid>();
        foreach (var layer in Layers)
        {
            if (layer.GroupId is not Guid group) { yield return new(this, layer); continue; }
            if (!seen.Add(group)) continue;
            yield return new(this, layer, true);
            if (!_collapsedGroups.Contains(group)) foreach (var member in Layers.Where(l => l.GroupId == group)) yield return new(this, member);
        }
    }
    public bool IsGroupCollapsed(Guid id) => _collapsedGroups.Contains(id);
    public void ToggleGroupExpanded(Guid id) { if (!_collapsedGroups.Remove(id)) _collapsedGroups.Add(id); RefreshLayers(); }
    public IReadOnlyList<ThemeLayer> GroupMembers(Guid id) => Project.ActiveLayout.Layers.Where(l => l.GroupId == id).ToArray();
    public void RenameGroup(Guid id, string name)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) return;
        var members = GroupMembers(id); if (members.Count == 0 || members.Any(l => l.Locked) || members.All(l => l.GroupName == name.Trim())) return;
        Change(() => { foreach (var l in members) l.GroupName = name.Trim(); });
    }
    public void ToggleGroupState(Guid id, bool locking)
    {
        if (!CanEdit) return; var members = GroupMembers(id); if (members.Count == 0) return;
        var value = locking ? !members.All(l => l.Locked) : !members.All(l => l.Visible);
        Change(() => { foreach (var l in members) { if (locking) l.Locked = value; else l.Visible = value; } });
    }
    public void RotateSelection(double degrees)
    {
        if (!CanScaleSelection || !double.IsFinite(degrees)) return;
        degrees %= 360; if (degrees == 0) return;
        var members = SelectedLayers.ToArray(); var b = ThemeGeometry.Union(members); var cx = b.X + b.Width / 2; var cy = b.Y + b.Height / 2; var a = degrees * Math.PI / 180;
        var candidates = members.Select(l => { var c = l.Clone(); var x = l.X + l.Width / 2 - cx; var y = l.Y + l.Height / 2 - cy; c.X = cx + x * Math.Cos(a) - y * Math.Sin(a) - l.Width / 2; c.Y = cy + x * Math.Sin(a) + y * Math.Cos(a) - l.Height / 2; c.Rotation = (c.Rotation + degrees) % 360; return c; }).ToArray();
        if (candidates.Any(l => Math.Abs(l.X) > 32768 || Math.Abs(l.Y) > 32768)) { Status = "旋转超出坐标范围。"; return; }
        Change(() => { for (var i = 0; i < members.Length; i++) { members[i].X = candidates[i].X; members[i].Y = candidates[i].Y; members[i].Rotation = candidates[i].Rotation; } });
    }
    public void AlignToCanvas(string alignment)
    {
        var members = SelectedLayers.Where(l => !l.Locked).ToArray(); if (!CanEdit || members.Length == 0) return;
        var b = ThemeGeometry.Union(members); var layout = Project.ActiveLayout;
        var dx = alignment switch { "左" => -b.X, "右" => layout.Width - b.X - b.Width, "水平居中" or "居中" => (layout.Width - b.Width) / 2 - b.X, _ => 0 };
        var dy = alignment switch { "顶" => -b.Y, "底" => layout.Height - b.Y - b.Height, "垂直居中" or "居中" => (layout.Height - b.Height) / 2 - b.Y, _ => 0 };
        MoveSelection(dx, dy);
    }
    private void PasteStyle()
    {
        if (!CanEdit || _styleClipboard is not { } source) return; var targets = SelectedLayers.Where(l => !l.Locked).ToArray();
        if (targets.Length == 0) return;
        static bool Text(ThemeLayer l) => l.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday;
        static bool Image(ThemeLayer l) => l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper;
        static bool Color(ThemeLayer l) => Text(l) || l.Kind is ThemeElementKind.Rectangle or ThemeElementKind.Ellipse;
        Change(() =>
        {
            foreach (var l in targets)
            {
                l.Opacity = source.Opacity;
                if (Color(l) && Color(source)) l.Color = source.Color;
                if (Text(l) && Text(source)) { l.FontFamily = source.FontFamily; l.FontSize = source.FontSize; l.Bold = source.Bold; l.FontWeightValue = source.FontWeightValue; l.FontStretchValue = source.FontStretchValue; l.Alignment = source.Alignment; l.Spacing = source.Spacing; l.Shadow = source.Shadow; }
                if (Image(l) && Image(source)) { l.Fill = source.Fill; l.ImageZoom = source.ImageZoom; l.CropX = source.CropX; l.CropY = source.CropY; l.Brightness = source.Brightness; }
            }
        });
        Status = "已粘贴适用的外观样式，内容与素材引用保持不变。";
    }
    public void ReportSnap(bool snapped) { SnapFeedback = snapped ? "已吸附 · Alt 临时关闭" : ""; OnPropertyChanged(nameof(SnapFeedback)); }
    private void NotifyFinishing() { RotateGroupCommand?.Notify(); AlignCanvasCommand?.Notify(); CopyStyleCommand?.Notify(); PasteStyleCommand?.Notify(); OnPropertyChanged(nameof(EffectiveUsbGeometry)); OnPropertyChanged(nameof(SelectionSizeLabel)); }
}

