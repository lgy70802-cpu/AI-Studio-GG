using System.Windows;
using System.Windows.Media;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.Views;

/// <summary>Vector/text clock faces shared by editor, preview and output SceneView.</summary>
public static class DigitalClockDrawing
{
    private static readonly int[] Masks = [63, 6, 91, 79, 102, 109, 125, 7, 127, 111];
    private static ThemeLayer Scaled(ThemeLayer source, double factor) { var layer = source.Clone(); layer.FontSize *= factor; layer.Spacing *= factor; return layer; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Font, double Size, bool Bold, int Weight, int Stretch, string Culture), double> DigitCache = new();
    private static double DigitWidth(ThemeLayer layer)
    {
        var key = (layer.FontFamily, layer.FontSize, layer.Bold, layer.FontWeightValue, layer.FontStretchValue, System.Globalization.CultureInfo.CurrentCulture.Name);
        if (DigitCache.TryGetValue(key, out var value)) return value;
        value = "0123456789".Max(c => ThemeTextMetrics.Format(layer, c.ToString()).WidthIncludingTrailingWhitespace);
        if (DigitCache.Count >= 128) DigitCache.Clear(); DigitCache[key] = value; return value;
    }
    private static double Width(ThemeLayer layer, string text) => Math.Max(1, text.Sum(c => char.IsAsciiDigit(c) ? DigitWidth(layer) : ThemeTextMetrics.Format(layer, c.ToString()).WidthIncludingTrailingWhitespace) + Math.Max(0, text.Length - 1) * layer.Spacing);
    private static double Height(ThemeLayer layer) => ThemeTextMetrics.Format(layer, "0123456789APM:").Height;
    private static double SegmentAdvance(ThemeLayer layer, char c) => layer.FontSize * (char.IsAsciiDigit(c) ? .68 : c == ':' ? .22 : .65);
    private static double SegmentWidth(ThemeLayer layer, string text) => Math.Max(1, text.Sum(c => SegmentAdvance(layer, c)) + Math.Max(0, text.Length - 1) * layer.Spacing);
    private static (string Main, string Seconds) Split(string text) { var index = text.LastIndexOf(':'); return index < 0 ? (text, "") : (text[..index], text[(index + 1)..]); }
    private static Size ContentSize(ThemeLayer layer, string value)
    {
        if (layer.ClockAppearance == DigitalClockAppearance.SevenSegment) return new(SegmentWidth(layer, value), layer.FontSize);
        if (layer.ClockAppearance == DigitalClockAppearance.Stacked)
        {
            var lines = value.Split(':'); return new(lines.Max(l => Width(layer, l)), Height(layer) * lines.Length);
        }
        var parts = layer.ShowSeconds ? Split(value) : (value, ""); var small = Scaled(layer, .45);
        return new(Width(layer, parts.Item1) + (parts.Item2.Length == 0 ? 0 : layer.FontSize * .12 + Width(small, parts.Item2)), Height(layer));
    }
    public static Size Measure(ThemeLayer layer)
    {
        var samples = new[] { layer.GetText(new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero)), layer.GetText(new DateTimeOffset(2026, 1, 1, 13, 59, 59, TimeSpan.Zero)) }.Select(s => ContentSize(layer, s)).ToArray();
        var padding = Math.Max(4, layer.FontSize * .04) + (layer.Shadow ? 3 : 0);
        return new(Math.Ceiling(samples.Max(s => s.Width) + padding * 2), Math.Ceiling(samples.Max(s => s.Height) + padding * 2));
    }
    private static double Left(ThemeLayer layer, Rect rect, double width) => rect.X + (layer.Alignment == "居左" ? 0 : layer.Alignment == "居右" ? rect.Width - width : (rect.Width - width) / 2);
    private static void Run(DrawingContext dc, ThemeLayer layer, string text, Point origin, Brush brush)
    {
        var digit = DigitWidth(layer); var x = origin.X;
        foreach (var c in text)
        {
            var formatted = ThemeTextMetrics.Format(layer, c.ToString()); formatted.SetForegroundBrush(brush);
            var width = char.IsAsciiDigit(c) ? digit : formatted.WidthIncludingTrailingWhitespace;
            dc.DrawText(formatted, new(x + (width - formatted.WidthIncludingTrailingWhitespace) / 2, origin.Y)); x += width + layer.Spacing;
        }
    }
    public static void Draw(DrawingContext dc, ThemeLayer layer, string value, Rect rect, Brush brush)
    {
        var size = ContentSize(layer, value); var x = Left(layer, rect, size.Width); var y = rect.Y + (rect.Height - size.Height) / 2;
        void Paint(Brush color, double offset)
        {
            if (layer.ClockAppearance == DigitalClockAppearance.SevenSegment)
            {
                var at = x + offset; var top = y + offset; var h = layer.FontSize; var w = h * .58; var t = h * .075;
                foreach (var c in value)
                {
                    if (char.IsAsciiDigit(c))
                    {
                        var mask = Masks[c - '0'];
                        var boxes = new[] { new Rect(at + t, top, w - 2*t, t), new Rect(at + w-t, top+t, t, h/2-1.5*t), new Rect(at+w-t, top+h/2+t/2, t, h/2-1.5*t), new Rect(at+t, top+h-t, w-2*t, t), new Rect(at, top+h/2+t/2, t, h/2-1.5*t), new Rect(at, top+t, t, h/2-1.5*t), new Rect(at+t, top+h/2-t/2, w-2*t, t) };
                        for (var i=0; i<7; i++) if ((mask & (1<<i)) != 0) dc.DrawRoundedRectangle(color, null, boxes[i], t*.2, t*.2);
                    }
                    else if (c == ':') { dc.DrawEllipse(color, null, new(at+h*.07, top+h*.33), t/2, t/2); dc.DrawEllipse(color, null, new(at+h*.07, top+h*.67), t/2, t/2); }
                    else Run(dc, Scaled(layer, .65), c.ToString(), new(at, top+h*.25), color);
                    at += SegmentAdvance(layer, c) + layer.Spacing;
                }
            }
            else if (layer.ClockAppearance == DigitalClockAppearance.Stacked)
            {
                var lineY = y + offset;
                foreach (var line in value.Split(':')) { Run(dc, layer, line, new(Left(layer, rect, Width(layer, line))+offset, lineY), color); lineY += Height(layer); }
            }
            else
            {
                var parts = layer.ShowSeconds ? Split(value) : (value, "");
                Run(dc, layer, parts.Item1, new(x+offset,y+offset), color);
                if (parts.Item2.Length > 0) { var small = Scaled(layer, .45); Run(dc, small, parts.Item2, new(x+Width(layer,parts.Item1)+layer.FontSize*.12+offset,y+size.Height-Height(small)+offset), color); }
            }
        }
        if (layer.Shadow) Paint(Brushes.Black, 3);
        Paint(brush, 0);
    }
}


