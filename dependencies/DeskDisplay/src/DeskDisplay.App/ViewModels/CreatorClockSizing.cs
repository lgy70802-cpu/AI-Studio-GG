using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    internal void ReportDevicePreview(string message) => Status=message;
    private static void GrowClockBounds(ThemeLayer layer)
    {
        if(layer.Kind!=ThemeElementKind.DigitalClock)return;
        var size=ThemeTextMetrics.MeasureClock(layer);
        var width=Math.Max(layer.Width,Math.Min(16384,size.Width));
        var height=Math.Max(layer.Height,Math.Min(16384,size.Height));
        layer.X=Math.Clamp(layer.X+(layer.Width-width)/2,-32768,32768);
        layer.Y=Math.Clamp(layer.Y+(layer.Height-height)/2,-32768,32768);
        layer.Width=width;layer.Height=height;
    }
    public IReadOnlyList<ClockComponentPreset> ClockPresets => ClockComponentPresets.All;
    private ActionCommand? _addClockPreset;
    public ActionCommand AddClockPresetCommand => _addClockPreset ??= new(p =>
    {
        var preset = p is CreatorClockPresetCard card ? card.Preset : p as ClockComponentPreset;
        if (preset is null || !CanEdit || Project.ActiveLayout.Layers.Count >= 100) return;
        var layer = preset.Create(); layer.FontSize = IsPortrait ? 80 : 120;
        SizeNewClock(layer); Insert(layer);
    }, _ => CanEdit);
    private ActionCommand? _fitClockContent;
    public ActionCommand FitClockContentCommand => _fitClockContent ??= new(_ =>
    {
        if (SelectedLayer is not { Kind: ThemeElementKind.DigitalClock, Locked: false } layer) return;
        var size = ThemeTextMetrics.MeasureClock(layer);
        var x = layer.X + (layer.Width - size.Width) / 2;
        var y = layer.Y + (layer.Height - size.Height) / 2;
        if (size.Width > 16384 || size.Height > 16384 || Math.Abs(x) > 32768 || Math.Abs(y) > 32768)
        { Status = "当前字号或字符间距超出边框范围，请先减小后再适应内容。"; return; }
        Change(() =>
        {
            // Preserve the visual centre, including rotated layers; never fit on every tick.
            layer.X = x; layer.Y = y;
            layer.Width = size.Width; layer.Height = size.Height;
        });
    }, _ => CanEditSelection && IsClock);

    private void SizeNewClock(ThemeLayer layer)
    {
        var layout = Project.ActiveLayout;
        var size = ThemeTextMetrics.MeasureClock(layer);
        var scale = Math.Min(1, Math.Min(layout.Width * .9 / size.Width, layout.Height * .9 / size.Height));
        if (scale < 1) { layer.FontSize = Math.Max(1, layer.FontSize * scale); size = ThemeTextMetrics.MeasureClock(layer); }
        layer.Width = size.Width; layer.Height = size.Height;
        layer.X = (layout.Width - layer.Width) / 2;
        layer.Y = (layout.Height - layer.Height) / 2;
    }
}


