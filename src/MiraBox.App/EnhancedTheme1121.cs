using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace MiraBox.App;

public partial class MainWindow
{
    static readonly Dictionary<string, FontFamily> EditorFonts = new();
    static FontFamily EnhancedFont(string key)
    {
        if (EditorFonts.TryGetValue(key, out var cached)) return cached;
        var name = key switch { "round" => "Varela Round", "tech" => "Orbitron", "mono" => "Share Tech Mono",
            "cond" => "Bebas Neue", "serif" => "Playfair Display", "song" => "SimSun", "kai" => "KaiTi",
            "hei" => "Microsoft YaHei", _ => "Segoe UI" };
        var family = key is "round" or "tech" or "mono" or "cond" or "serif" ?
            new FontFamily(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Editor", "Fonts") + "/"), "./#" + name) : new FontFamily(name);
        EditorFonts[key] = family;
        return family;
    }

    FrameworkElement EnhancedStyledText(JsonElement element, string text, double size, int weight, double defaultSpacing = 0, double lineHeight = 1.08)
    {
        var family = EnhancedFont(JsText(element, "font"));
        var ink = ColorBrush(JsText(element, "color", "#ffffff"));
        var spacing = JsNumber(element, "ls", defaultSpacing * 100) / 100;
        var vertical = JsFlag(element, "vert");
        var effect = JsText(element, "fx");
        FrameworkElement view;
        if (!vertical && spacing == 0 && effect != "outline")
            view = new TextBlock { Text = text, FontFamily = family, FontSize = size, FontWeight = FontWeight.FromOpenTypeWeight(weight),
                Foreground = ink, TextAlignment = JsText(element, "align") switch { "left" => TextAlignment.Left, "right" => TextAlignment.Right, _ => TextAlignment.Center },
                LineHeight = size * lineHeight, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        else
        {
            var lines = text.Replace("\r", "").Split('\n');
            var geometries = new List<(Geometry Geometry, double Advance)>();
            var widths = new List<double>();
            var typeface = new Typeface(family, FontStyles.Normal, FontWeight.FromOpenTypeWeight(weight), FontStretches.Normal);
            foreach (var line in lines)
            {
                var glyphs = new List<Geometry>();
                double advance = 0;
                var characters = StringInfo.GetTextElementEnumerator(line);
                while (characters.MoveNext())
                {
                    var glyph = new FormattedText((string)characters.Current, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, ink, 1);
                    var geometry = glyph.BuildGeometry(vertical ? new Point(0, advance) : new Point(advance, 0));
                    glyphs.Add(geometry);
                    advance += (vertical ? size : glyph.WidthIncludingTrailingWhitespace) + size * spacing;
                }
                var group = new GeometryGroup(); foreach (var glyph in glyphs) group.Children.Add(glyph);
                geometries.Add((group, advance)); widths.Add(advance);
            }
            var largest = Math.Max(size, widths.DefaultIfEmpty(0).Max());
            var drawing = new DrawingGroup();
            for (var i = 0; i < geometries.Count; i++)
            {
                var g = geometries[i].Geometry;
                var alignment = JsText(element, "align", "center");
                var offset = alignment == "left" ? 0 : (largest - widths[i]) * (alignment == "right" ? 1 : .5);
                g.Transform = vertical ? new TranslateTransform((lines.Length - i - 1) * size * lineHeight, 0) : new TranslateTransform(offset, i * size * lineHeight);
                drawing.Children.Add(new GeometryDrawing(effect == "outline" ? null : ink, effect == "outline" ? new Pen(ink, size * .03) : null, g));
            }
            view = new EditorDrawing(drawing, vertical ? lines.Length * size * lineHeight : largest,
                vertical ? largest : lines.Length * size * lineHeight);
        }
        ApplyEditorEffect(view, effect, ink, size);
        return view;
    }

    static void ApplyEditorEffect(FrameworkElement view, string effect, Brush ink, double size)
    {
        if (effect is "glow" or "shadow") view.Effect = new DropShadowEffect {
            Color = effect == "glow" && ink is SolidColorBrush solid ? solid.Color : Colors.Black,
            ShadowDepth = effect == "glow" ? 0 : size * .06, BlurRadius = size * (effect == "glow" ? .55 : .18),
            Opacity = effect == "glow" ? .9 : .6, Direction = 270 };
    }

    sealed class EditorDrawing(Drawing drawing, double width, double height) : FrameworkElement
    {
        protected override System.Windows.Size MeasureOverride(System.Windows.Size available) => new(Math.Max(0, width), Math.Max(0, height));
        protected override void OnRender(DrawingContext context) => context.DrawDrawing(drawing);
    }

    FrameworkElement EnhancedDecoration(JsonElement element, double width, double height)
    {
        var w = Math.Max(.01, JsNumber(element, "w", 30) * width / 100);
        var h = Math.Max(.01, JsNumber(element, "h", 8) * height / 100);
        var minimum = Math.Min(width, height);
        var ink = ColorBrush(JsText(element, "color", "#ffffff"));
        var shape = JsText(element, "sh", "rect");
        if (shape == "rect") return new Border { Width = w, Height = h, Background = ink, CornerRadius = new CornerRadius(JsNumber(element, "radius") * minimum / 100) };
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            var bw = JsNumber(element, "bw", .8) * minimum / 100;
            if (shape == "dash") for (var x = 0d; x < w; x += w * .05) dc.DrawRectangle(ink, null, new Rect(x, 0, Math.Min(w - x, w * .05 * .62), h));
            else if (shape == "dots") for (var x = h * 1.1; x < w + h; x += h * 2.2) dc.DrawEllipse(ink, null, new Point(x, h / 2), h * .46, h * .46);
            else if (shape == "double") { dc.DrawRectangle(ink, null, new Rect(0, 0, w, h * .32)); dc.DrawRectangle(ink, null, new Rect(0, h * .68, w, h * .32)); }
            else if (shape == "ring") dc.DrawEllipse(null, new Pen(ink, bw), new Point(w / 2, h / 2), Math.Max(0, (w - bw) / 2), Math.Max(0, (h - bw) / 2));
            else if (shape == "brackets") foreach (var (x, y, sx, sy) in new[] { (0d, 0d, 1d, 1d), (w, 0d, -1d, 1d), (0d, h, 1d, -1d), (w, h, -1d, -1d) }) {
                var pen = new Pen(ink, bw); dc.DrawLine(pen, new Point(x + sx * bw * 9, y), new Point(x, y)); dc.DrawLine(pen, new Point(x, y), new Point(x, y + sy * bw * 9)); }
            else if (shape == "dotline") { var radius = bw * 1.7; var gap = minimum * .014;
                dc.DrawEllipse(ink, null, new Point(w / 2, h / 2), radius, radius);
                dc.DrawRectangle(ink, null, new Rect(0, 0, Math.Max(0, w / 2 - radius - gap), h));
                var start = w / 2 + radius + gap; dc.DrawRectangle(ink, null, new Rect(start, 0, Math.Max(0, w - start), h)); }
        }
        drawing.ClipGeometry = new RectangleGeometry(new Rect(0, 0, w, h));
        return new EditorDrawing(drawing, w, h);
    }

    FrameworkElement EnhancedAnalogClock(JsonElement element, DateTimeOffset time, double minimum)
    {
        var side = JsNumber(element, "size", 30) * minimum / 100 * 2.6;
        var ink = ColorBrush(JsText(element, "color", "#ffffff"));
        var accent = ColorBrush(JsText(element, "accent", "#ff6a4d"));
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            var dial = JsText(element, "dial", "tick");
            if ((!element.TryGetProperty("ring", out var ring) || ring.ValueKind != JsonValueKind.False) && dial != "bare") dc.DrawEllipse(null, new Pen(ink, 1.4), new Point(50, 50), 48, 48);
            if (dial is "tick" or "dot" or "bare")
            {
                var count = dial == "tick" ? 60 : dial == "dot" ? 12 : 4;
                for (var i = 0; i < count; i++) {
                    dc.PushTransform(new RotateTransform(i * 360d / count, 50, 50));
                    if (dial == "dot") dc.DrawEllipse(ink, null, new Point(50, 9), i % 3 == 0 ? 2.8 : 1.8, i % 3 == 0 ? 2.8 : 1.8);
                    else { var big = dial == "bare" || i % 5 == 0; dc.PushOpacity(big ? 1 : .55); dc.DrawLine(new Pen(ink, big ? 2.2 : .9), new Point(50, 5), new Point(50, big ? 12 : 8)); dc.Pop(); }
                    dc.Pop();
                }
            }
            else for (var i = 0; i < 12; i++) {
                var labels = new[] { "XII", "I", "II", "III", "IIII", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
                var text = dial == "roman" ? labels[i] : (i == 0 ? 12 : i).ToString(CultureInfo.InvariantCulture);
                var font = new Typeface(EnhancedFont(JsText(element, "font")), FontStyles.Normal, dial == "roman" ? FontWeights.Normal : FontWeights.SemiBold, FontStretches.Normal);
                var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, dial == "roman" ? text.Length > 2 ? 7 : 9 : 11, ink, 1);
                var a = i * Math.PI / 6; dc.DrawText(ft, new Point(50 + 36 * Math.Sin(a) - ft.Width / 2, 50 - 36 * Math.Cos(a) - ft.Height / 2));
            }
            var hands = JsText(element, "hands", "line");
            Hand((time.Hour % 12 + time.Minute / 60d) * 30, hands == "taper" ? 24 : 22, hands == "taper" ? 2.4 : hands == "round" ? 4.6 : 3.2);
            Hand((time.Minute + time.Second / 60d) * 6, hands == "taper" ? 36 : 34, hands == "taper" ? 1.8 : hands == "round" ? 3.2 : 2.2);
            if (JsFlag(element, "sec")) { dc.PushTransform(new RotateTransform(time.Second * 6, 50, 50)); dc.DrawLine(new Pen(accent, 1), new Point(50, 58), new Point(50, 10)); dc.Pop(); }
            dc.DrawEllipse(JsFlag(element, "sec") ? accent : ink, null, new Point(50, 50), 2.4, 2.4);
            void Hand(double angle, double length, double thickness) {
                dc.PushTransform(new RotateTransform(angle, 50, 50));
                if (hands == "taper") { var geometry = new StreamGeometry(); using (var path = geometry.Open()) { path.BeginFigure(new Point(50 - thickness * .9, 54), true, true); path.LineTo(new Point(50 + thickness * .9, 54), true, false); path.LineTo(new Point(50 + thickness * .35, 50 - length), true, false); path.LineTo(new Point(50 - thickness * .35, 50 - length), true, false); } dc.DrawGeometry(ink, null, geometry); }
                else dc.DrawLine(new Pen(ink, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(50, hands == "round" ? 50 : 54), new Point(50, 50 - length));
                dc.Pop();
            }
        }
        drawing.Transform = new ScaleTransform(side / 100, side / 100);
        var view = new EditorDrawing(drawing, side, side);
        ApplyEditorEffect(view, JsText(element, "fx"), ink, side / 2.6);
        return view;
    }
}
