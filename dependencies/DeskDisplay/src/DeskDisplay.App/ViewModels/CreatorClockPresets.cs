using System.Windows;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;
using DeskDisplay.Display.Services;

namespace DeskDisplay.App.ViewModels;

public sealed record CreatorClockPresetCard(ClockComponentPreset Preset, BitmapSource Cover)
{
    public string Name => Preset.Name;
}
public sealed partial class CreatorViewModel
{
    private IReadOnlyList<CreatorClockPresetCard>? _clockPresetCards;
    public IReadOnlyList<CreatorClockPresetCard> ClockPresetCards => _clockPresetCards ??= ClockComponentPresets.All.Select(p =>
    {
        var layer = p.Create(); layer.Color = "#202C43"; layer.FontSize = 34;
        var size = ThemeTextMetrics.MeasureClock(layer); var factor = Math.Min(1, Math.Min(180 / size.Width, 68 / size.Height)); layer.FontSize *= factor; layer.Spacing *= factor;
        size = ThemeTextMetrics.MeasureClock(layer); layer.Width = size.Width; layer.Height = size.Height; layer.X = (200-size.Width)/2; layer.Y=(80-size.Height)/2;
        var project = new ThemeProject { Landscape = new() { Width = 200, Height = 80, Background = "#F4F7FB", Layers = [layer] } };
        using var scene = new ThemeScene(project); var view = new SceneView { Scene = scene, Width = 200, Height = 80 };
        view.Measure(new Size(200,80)); view.Arrange(new Rect(0,0,200,80)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(200,80,96,96,System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(view); bitmap.Freeze(); view.Scene = null;
        return new CreatorClockPresetCard(p,bitmap);
    }).ToArray();
    public IReadOnlyList<ClockFontChoice> ClockWeights { get; } = [new(200,"极细"),new(300,"轻细"),new(400,"常规"),new(700,"加粗"),new(800,"特粗")];
    public IReadOnlyList<ClockFontChoice> ClockStretches { get; } = [new(3,"窄体"),new(5,"标准"),new(7,"宽体")];
    public int ClockWeight { get => SelectedLayer is { } l ? l.Bold ? Math.Max(700,l.FontWeightValue) : l.FontWeightValue : 400; set { if(value is >=100 and <=900)Edit(l=>{l.FontWeightValue=value;l.Bold=value>=700;}); } }
    public int ClockStretch { get=>SelectedLayer?.FontStretchValue??5; set{if(value is >=1 and <=9)Edit(l=>l.FontStretchValue=value);} }
    public bool SupportsClockFont => !IsClock || ClockAppearance != DigitalClockAppearance.SevenSegment;
    public string ClockFaceHint => ClockAppearance == DigitalClockAppearance.SevenSegment ? "数码管使用固定字形，可调字号、颜色和间距。" : "更改格式或字号后，可用“适应内容”收紧边框。";
    public IReadOnlyList<ClockAppearanceChoice> ClockAppearanceChoices { get; } = [new(DigitalClockAppearance.Standard,"普通数字"),new(DigitalClockAppearance.SevenSegment,"数码管"),new(DigitalClockAppearance.SmallSeconds,"大时分小秒"),new(DigitalClockAppearance.Stacked,"竖排时分")];
    public DigitalClockAppearance ClockAppearance { get => SelectedLayer?.ClockAppearance ?? DigitalClockAppearance.Standard; set { if (Enum.IsDefined(value)) Edit(l => l.ClockAppearance = value); } }
}
public sealed record ClockAppearanceChoice(DigitalClockAppearance Value, string Label);

public sealed record ClockFontChoice(int Value, string Label);

