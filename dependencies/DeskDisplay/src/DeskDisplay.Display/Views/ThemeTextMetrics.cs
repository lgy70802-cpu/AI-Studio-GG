using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.Views;

/// <summary>Uses the same WPF font metrics as the output renderer; dimensions remain design units.</summary>
public static class ThemeTextMetrics
{
    private readonly record struct Key(string Text, string Font, double Size, bool Bold, int Weight, int Stretch, double Spacing, string Culture);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Key, Size> Cache = new();
    public static string[] Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    public static FormattedText Format(ThemeLayer layer, string text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily(layer.FontFamily), FontStyles.Normal, FontWeight.FromOpenTypeWeight(layer.Bold ? Math.Max(700, layer.FontWeightValue) : layer.FontWeightValue), FontStretch.FromOpenTypeStretch(layer.FontStretchValue)), layer.FontSize, Brushes.Black, 1);
    public static Size Measure(ThemeLayer layer)
    {
        var key = new Key(layer.Text, layer.FontFamily, layer.FontSize, layer.Bold, layer.FontWeightValue, layer.FontStretchValue, layer.Spacing, CultureInfo.CurrentCulture.Name);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var lines = Lines(layer.Text); var height = Format(layer, "Mg国").Height;
        var width = lines.Max(line =>
        {
            if (layer.Spacing == 0) return Format(layer, line).WidthIncludingTrailingWhitespace;
            var elements = StringInfo.GetTextElementEnumerator(line); var widths = new List<double>();
            while (elements.MoveNext()) widths.Add(Format(layer, elements.GetTextElement()).WidthIncludingTrailingWhitespace);
            return Math.Max(0, widths.Sum() + Math.Max(0, widths.Count - 1) * layer.Spacing);
        });
        var result = new Size(width, height * lines.Length);
        if (Cache.Count >= 128) Cache.Clear(); Cache[key] = result; return result;
    }
    /// <summary>Matches the renderer's fixed digit advance, reserving both AM/PM suffixes.</summary>
    public static Size MeasureClock(ThemeLayer layer)
    {
        if (layer.ClockAppearance != DigitalClockAppearance.Standard) return DigitalClockDrawing.Measure(layer);
        var digit = "0123456789".Max(c => Format(layer, c.ToString()).WidthIncludingTrailingWhitespace);
        var samples = new[] { layer.GetText(new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero)), layer.GetText(new DateTimeOffset(2026, 1, 1, 13, 59, 59, TimeSpan.Zero)) };
        var width = samples.Max(text => text.Sum(c => char.IsAsciiDigit(c) ? digit : Format(layer, c.ToString()).WidthIncludingTrailingWhitespace) + Math.Max(0, text.Length - 1) * layer.Spacing);
        var padding = Math.Max(4, layer.FontSize * .04) + (layer.Shadow ? 3 : 0);
        return new Size(Math.Max(1, Math.Ceiling(width + padding * 2)), Math.Ceiling(Format(layer, "0123456789APM:").Height + padding * 2));
    }
}


