using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Library;
using DeskDisplay.App.Views;
using System.Windows;

namespace DeskDisplay.App.ViewModels;

public sealed record ThemeApplyCheck(bool CanApply, string Target, string Orientation, string Geometry, IReadOnlyList<string> Problems);
public sealed partial class CreatorViewModel
{
    public AsyncCommand CropImageCommand { get; private set; } = null!;
    public ActionCommand ConfirmApplyCommand { get; private set; } = null!;
    public ActionCommand BatchCommand { get; private set; } = null!;
    public double BatchOpacity { get; set; } = 1;
    public string BatchColor { get; set; } = "#FFFFFF";
    public string BatchFont { get; set; } = "Microsoft YaHei UI";
    public double BatchFontSize { get; set; } = 48;
    public string ProjectCategory { get => Project.Category; set { if (value is not null && value.Trim().Length <= 80 && Project.Category != value.Trim()) Change(() => Project.Category = string.IsNullOrWhiteSpace(value) ? "未分类" : value.Trim()); } }
    private string _categoryFilter = "全部分类";
    public IReadOnlyList<string> ProjectCategories => new[] { "全部分类" }.Concat(Projects.Select(p => p.Project.Category).Distinct().Order()).ToArray();
    public string ProjectCategoryFilter { get => _categoryFilter; set { if (SetProperty(ref _categoryFilter, value ?? "全部分类")) OnPropertyChanged(nameof(VisibleProjects)); } }
    public bool GridEnabled { get => Project.ActiveLayout.ShowGrid; set { if (value != GridEnabled) Change(() => Project.ActiveLayout.ShowGrid = value); } }
    public double GridSpacing { get => Project.ActiveLayout.GridSpacing; set { if (double.IsFinite(value) && value is >= 1 and <= 500 && value != GridSpacing) Change(() => Project.ActiveLayout.GridSpacing = value); } }
    private void InitializeStudioTools()
    {
        CropImageCommand = new(CropSelectedAsync, () => CanEdit && IsImage && SelectedLayer?.IsVideo == false && SelectedLayer.Locked == false, _logger);
        ConfirmApplyCommand = new(_ => { var dialog = new CreatorApplyConfirmation(this) { Owner = Application.Current?.MainWindow }; if (dialog.ShowDialog() == true) ApplyCommand.Execute(null); }, _ => CanEdit && _editing);
        BatchCommand = new(p => ApplyBatch(p?.ToString() ?? ""), _ => CanEdit && SelectedLayers.Count > 0);
    }
    private async Task CropSelectedAsync()
    {
        var id = SelectedLayer?.Id; await _assets;
        if (SelectedLayer is not { Locked: false, IsVideo: false } layer || layer.Id != id || !Scene.Children.TryGetValue(layer.Id, out var child) || child is not ImageScene image || image.Image is not WpfSceneImage pixels) { Status = "请先加载并选中可用的静态图片。"; return; }
        var dialog = new CreatorCropEditor(layer, pixels.Bitmap) { Owner = Application.Current?.MainWindow };
        if (dialog.ShowDialog() == true) ApplyImageCrop(layer.Id, dialog.Zoom, dialog.CropX, dialog.CropY, dialog.RestoreAspect ? (double)pixels.OriginalWidth / pixels.OriginalHeight : null);
    }
    public void ApplyImageCrop(Guid id, double zoom, double cropX, double cropY, double? aspect = null)
    {
        var layer = Project.ActiveLayout.Layers.FirstOrDefault(l => l.Id == id);
        if (!CanEdit || layer is null || layer.Locked || layer.IsVideo || layer.Kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper) || !double.IsFinite(zoom) || zoom is < 1 or > 8 || !double.IsFinite(cropX) || cropX is < 0 or > 1 || !double.IsFinite(cropY) || cropY is < 0 or > 1 || aspect is double requestedRatio && (!double.IsFinite(requestedRatio) || requestedRatio <= 0)) return;
        Change(() =>
        {
            if (aspect is double ratio) { var w = Math.Min(layer.Width, layer.Height * ratio); var h = w / ratio; if (w >= 1 && h >= 1) { layer.X += (layer.Width - w) / 2; layer.Y += (layer.Height - h) / 2; layer.Width = w; layer.Height = h; } }
            layer.Fill = ImageDisplayMode.Fill; layer.ImageZoom = zoom; layer.CropX = cropX; layer.CropY = cropY;
        });
    }
    public ThemeApplyCheck CheckThemeApplication()
    {
        var problems = new List<string>();
        if (OutputCheck.StartsWith("不能应用")) problems.Add(OutputCheck);
        foreach (var l in Project.ActiveLayout.Layers.Where(l => l.Visible))
        {
            if (l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper)
            { var path = LibraryAssets.FirstOrDefault(i => i.Asset.Id == l.AssetId)?.Asset.Path ?? l.Path; if (!System.IO.File.Exists(path)) problems.Add("素材缺失：" + l.Name); }
            if (l.Kind == ThemeElementKind.StockPlugin && _main.Plugins?.Enabled != true || l.Kind == ThemeElementKind.CameraClock && _main.Plugins?.CameraEnabled != true) problems.Add("插件未启用：" + l.Name);
        }
        string target;
        if (IsUsbTarget) { target = "D92 USB · " + (_main.Usb?.SelectedSerial ?? "未选择设备"); if (_main.Usb?.IsSelectedConnected != true) problems.Add("请先选择并连接 D92。"); if (_main.Usb is { CanApply: false }) problems.Add("USB 正在处理其他操作，请稍后再试。"); if (Project.ActiveLayout.Layers.Count(l => l.Visible && l.IsVideo) > 1) problems.Add("每个布局目前支持一个可见视频图层"); if (_main.Usb?.ValidateConfiguration() != true) problems.Add("USB 输出参数无效或不可用"); }
        else { target = _main.Devices.TargetName; if (_main.Devices.TargetDisplay is not { IsPrimary: false } || !_main.Devices.TestDisplayCommand.CanExecute(null)) problems.Add("未选择可用的非主屏输出目标"); }
        return new(problems.Count == 0, target, OrientationLabel, IsUsbTarget ? EffectiveUsbGeometry : _main.Devices.TargetResolution, problems.Distinct().ToArray());
    }
    public void ApplyBatch(string property)
    {
        if (!CanEdit) return;
        var candidates = SelectedLayers.Where(l => property is "lock" or "unlock" || !l.Locked).ToArray();
        bool Text(ThemeLayer l) => l.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday;
        var count = 0;
        if (property == "color") { try { if (System.Windows.Media.ColorConverter.ConvertFromString(BatchColor) is not System.Windows.Media.Color) throw new FormatException(); } catch { Status = "颜色格式无效。"; return; } }
        if (property == "font" && (string.IsNullOrWhiteSpace(BatchFont) || !double.IsFinite(BatchFontSize) || BatchFontSize is < 1 or > 2000) || property == "opacity" && (!double.IsFinite(BatchOpacity) || BatchOpacity is < 0 or > 1)) return;
        var changes = new List<(ThemeLayer Source, ThemeLayer Copy)>();
        foreach (var l in candidates)
        {
            var c = l.Clone();
            switch (property) { case "opacity": c.Opacity = BatchOpacity; break; case "color" when Text(l) || l.Kind is ThemeElementKind.Rectangle or ThemeElementKind.Ellipse: c.Color = BatchColor; break; case "font" when Text(l): c.FontFamily = BatchFont; c.FontSize = BatchFontSize; break; case "lock": c.Locked = true; break; case "unlock": c.Locked = false; break; default: continue; }
            if (c.Opacity != l.Opacity || c.Color != l.Color || c.FontFamily != l.FontFamily || c.FontSize != l.FontSize || c.Locked != l.Locked) changes.Add((l, c));
        }
        if (changes.Count > 0) Change(() => { foreach (var (l, c) in changes) { l.Opacity = c.Opacity; l.Color = c.Color; l.FontFamily = c.FontFamily; l.FontSize = c.FontSize; l.Locked = c.Locked; count++; } });
        Status = $"已更新 {count} 个图层，其他图层保持原值；可一次撤销。";
    }
    private void NotifyStudioTools()
    {
        ThemePlaylistCommand?.NotifyCanExecuteChanged(); CropImageCommand?.NotifyCanExecuteChanged(); ConfirmApplyCommand?.Notify(); BatchCommand?.Notify();
        foreach (var p in new[] { nameof(ProjectCategory), nameof(GridEnabled), nameof(GridSpacing) }) OnPropertyChanged(p);
    }
}
