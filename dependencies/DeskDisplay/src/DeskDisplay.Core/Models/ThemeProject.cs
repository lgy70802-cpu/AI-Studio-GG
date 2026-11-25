using System.Text.Json;

namespace DeskDisplay.Core.Models;

public enum ThemeElementKind { Wallpaper, Image, DigitalClock, Date, Weekday, Text, Rectangle, Ellipse, StockPlugin, CameraClock }
public enum ThemeOrientation { Landscape, Portrait }
public enum ThemeAssetOrigin { Unknown, Local, Cloud }

/// <summary>Portable editable source. Asset IDs refer to the existing wallpaper catalog; paths are fallback references.</summary>
public sealed class ThemeProject
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "未命名主题";
    public string Category { get; set; } = "未分类";
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public ThemeOrientation Orientation { get; set; }
    public List<NamedThemeLayout> LayoutVersions { get; set; } = [];
    public Guid? SelectedLayoutId { get; set; }
    public ThemeLayout Landscape { get; set; } = new() { Width = 1920, Height = 480 };
    public ThemeLayout Portrait { get; set; } = new() { Width = 480, Height = 1920 };
    [System.Text.Json.Serialization.JsonIgnore]
    public ThemeLayout ActiveLayout => LayoutVersions.FirstOrDefault(l => l.Id == SelectedLayoutId)?.Layout ?? (Orientation == ThemeOrientation.Portrait ? Portrait : Landscape);
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<ThemeLayout> AllLayouts => new[] { Landscape, Portrait }.Concat(LayoutVersions.Select(v => v.Layout));
    public ThemeProject Clone()
    {
        var copy = (ThemeProject)MemberwiseClone(); copy.Landscape = Landscape?.Clone()!; copy.Portrait = Portrait?.Clone()!; copy.LayoutVersions = LayoutVersions.Select(v => v with { Layout = v.Layout.Clone() }).ToList(); return copy;
    }
    public void Validate()
    {
        if (Category is null || Category.Length > 80 || Version is not (1 or 2) || Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 160 || !Enum.IsDefined(Orientation)) throw new InvalidDataException("主题项目版本、标识或名称无效。");
        if (LayoutVersions is null || LayoutVersions.Count > 62 || LayoutVersions.Any(v => v is null || v.Id == Guid.Empty || string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > 80 || v.Revision < 1 || v.Layout is null) || LayoutVersions.Select(v => v.Id).Distinct().Count() != LayoutVersions.Count || SelectedLayoutId is not null && !LayoutVersions.Any(v => v.Id == SelectedLayoutId) || Version == 1 && (LayoutVersions.Count > 0 || SelectedLayoutId is not null)) throw new InvalidDataException("布局版本无效。");
        foreach (var layout in AllLayouts)
        {
            if (layout is null || !double.IsFinite(layout.Width) || !double.IsFinite(layout.Height) || layout.Width < 1 || layout.Height < 1 || layout.Width > 8192 || layout.Height > 8192 || string.IsNullOrWhiteSpace(layout.Background) || layout.Layers is null || layout.Layers.Count > 100 || layout.Layers.Any(l => l is null) || layout.Layers.Select(l => l.Id).Distinct().Count() != layout.Layers.Count) throw new InvalidDataException("画布或图层数量无效（最多100层）。");
            if (!double.IsFinite(layout.GridSpacing) || layout.GridSpacing is < 1 or > 500 || layout.VerticalGuides is null || layout.HorizontalGuides is null || layout.VerticalGuides.Count > 32 || layout.HorizontalGuides.Count > 32 || layout.VerticalGuides.Any(v => !double.IsFinite(v) || v < 0 || v > layout.Width) || layout.HorizontalGuides.Any(v => !double.IsFinite(v) || v < 0 || v > layout.Height)) throw new InvalidDataException("参考线无效（每个方向最多32条）。");
            foreach (var layer in layout.Layers)
            {
                if (!double.IsFinite(layer.ImageZoom) || layer.ImageZoom is < 1 or > 8 || !Enum.IsDefined(layer.AssetOrigin) || layer.CloudResourceId is null || layer.GroupName is null || layer.GroupName.Length > 160 || layer.Name is null || layer.Path is null || layer.Text is null || layer.Text.Length > 8192 || string.IsNullOrWhiteSpace(layer.FontFamily) || layer.Color is null || layer.Alignment is null || layer.Format is null || layer.FontWeightValue is < 100 or > 900 || layer.FontStretchValue is < 1 or > 9 || !Enum.IsDefined(layer.ClockAppearance) || !Enum.IsDefined(layer.VerticalAlignment) || !Enum.IsDefined(layer.Fill) || !double.IsFinite(layer.CropX) || !double.IsFinite(layer.CropY) || layer.CropX is < 0 or > 1 || layer.CropY is < 0 or > 1) throw new InvalidDataException("图层配置无效。");
                if (layer.Id == Guid.Empty || !Enum.IsDefined(layer.Kind) || !double.IsFinite(layer.X) || !double.IsFinite(layer.Y) || !double.IsFinite(layer.Width) || !double.IsFinite(layer.Height) || layer.Width < 1 || layer.Height < 1 || layer.Width > 16384 || layer.Height > 16384 || Math.Abs(layer.X) > 32768 || Math.Abs(layer.Y) > 32768 || !double.IsFinite(layer.Opacity) || layer.Opacity is < 0 or > 1 || !double.IsFinite(layer.Rotation) || !double.IsFinite(layer.FontSize) || layer.FontSize is < 1 or > 2000 || !double.IsFinite(layer.Spacing) || Math.Abs(layer.Spacing) > 200 || !double.IsFinite(layer.Brightness) || layer.Brightness is < 0 or > 1 || !double.IsFinite(layer.Blur) || layer.Blur != 0) throw new InvalidDataException("图层参数无效。");
            }
        }
    }
}

public sealed record NamedThemeLayout(Guid Id, string Name, long Revision, ThemeLayout Layout, Guid? DerivedFrom = null);

public sealed class ThemeLayout
{
    public ThemeLayout Clone() { var copy = (ThemeLayout)MemberwiseClone(); copy.Layers = Layers?.Select(l => l?.Clone()!).ToList()!; copy.VerticalGuides = VerticalGuides?.ToList()!; copy.HorizontalGuides = HorizontalGuides?.ToList()!; return copy; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool ShowGrid { get; set; }
    public double GridSpacing { get; set; } = 20;
    public List<double> VerticalGuides { get; set; } = [];
    public List<double> HorizontalGuides { get; set; } = [];
    public string Background { get; set; } = "#152239";
    public List<ThemeLayer> Layers { get; set; } = [];
}

public sealed class ThemeLayer
{
    // All layer fields are value types or immutable strings. Clone new mutable fields explicitly if added.
    public ThemeLayer Clone() => (ThemeLayer)MemberwiseClone();
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = "组合";
    public Guid Id { get; set; } = Guid.NewGuid();
    public ThemeElementKind Kind { get; set; }
    public string Name { get; set; } = "图层";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 500;
    public double Height { get; set; } = 160;
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public double Opacity { get; set; } = 1;
    public double Rotation { get; set; }
    public Guid? AssetId { get; set; }
    public string Path { get; set; } = "";
    public bool IsVideo { get; set; }
    public ThemeAssetOrigin AssetOrigin { get; set; }
    public string CloudResourceId { get; set; } = "";
    public double ImageZoom { get; set; } = 1;
    public ImageDisplayMode Fill { get; set; } = ImageDisplayMode.Uniform;
    public double CropX { get; set; } = 0.5;
    public double CropY { get; set; } = 0.5;
    public double Brightness { get; set; } = 1;
    public double Blur { get; set; }
    public string Text { get; set; } = "文字";
    public string Format { get; set; } = "HH:mm";
    public DigitalClockAppearance ClockAppearance { get; set; }
    public bool Hour12 { get; set; }
    public bool ShowSeconds { get; set; }
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 100;
    public int FontWeightValue { get; set; } = 400;
    public int FontStretchValue { get; set; } = 5;
    public bool Bold { get; set; }
    public string Color { get; set; } = "#FFFFFF";
    public string Alignment { get; set; } = "居中";
    public ThemeVerticalAlignment VerticalAlignment { get; set; } = ThemeVerticalAlignment.Center;
    public bool AutoGrowTextHeight { get; set; }
    public double Spacing { get; set; }
    public bool Shadow { get; set; }
    public string GetText(DateTimeOffset now) => Kind switch
    {
        ThemeElementKind.DigitalClock => now.ToString((Hour12 ? (Format == "HH" ? "hh" : "hh:mm") : Format == "HH" ? "HH" : "HH:mm") + (ShowSeconds ? ":ss" : ""), System.Globalization.CultureInfo.InvariantCulture) + (Hour12 ? now.ToString(" tt", System.Globalization.CultureInfo.InvariantCulture) : ""),
        ThemeElementKind.Date => now.ToString("yyyy年MM月dd日"),
        ThemeElementKind.Weekday => new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" }[(int)now.DayOfWeek],
        _ => Text
    };
}
public enum ThemeVerticalAlignment { Top, Center, Bottom }

/// <summary>UI-neutral history: one gesture is one transaction. History is bounded and never snapshots decoded pixels.</summary>
public sealed class ThemeEditSession(ThemeProject project)
{
    private readonly List<ThemeProject> _undo = [], _redo = [];
    public ThemeProject Project { get; private set; } = project;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public void Checkpoint() { _undo.Add(Project.Clone()); if (_undo.Count > 80) _undo.RemoveAt(0); _redo.Clear(); }
    public void Undo() { if (!CanUndo) return; _redo.Add(Project.Clone()); Project = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); }
    public void Redo() { if (!CanRedo) return; _undo.Add(Project.Clone()); Project = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); }
}


