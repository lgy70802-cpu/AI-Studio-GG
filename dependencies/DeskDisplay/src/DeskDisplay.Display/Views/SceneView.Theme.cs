using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.DataProviders;

namespace DeskDisplay.Display.Views;

public sealed partial class SceneView
{
    private readonly Dictionary<string, Brush> _themeBrushes = new(StringComparer.OrdinalIgnoreCase);
    private readonly record struct ThemeTextKey(string Text, string Font, double Size, bool Bold, int Weight, int Stretch, string Color, double Dpi, string Culture);
    private readonly Dictionary<ThemeTextKey, FormattedText> _themeTextCache = [];
    private FormattedText FormatThemeText(ThemeLayer layer, string text, string color)
    {
        var key = new ThemeTextKey(text, layer.FontFamily, layer.FontSize, layer.Bold, layer.FontWeightValue, layer.FontStretchValue, color, VisualTreeHelper.GetDpi(this).PixelsPerDip, CultureInfo.CurrentCulture.Name);
        if (_themeTextCache.TryGetValue(key, out var formatted)) return formatted;
        var font = new Typeface(new FontFamily(layer.FontFamily), FontStyles.Normal, FontWeight.FromOpenTypeWeight(layer.Bold ? Math.Max(700, layer.FontWeightValue) : layer.FontWeightValue), FontStretch.FromOpenTypeStretch(layer.FontStretchValue));
        formatted = new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font, layer.FontSize, ThemeBrush(color), key.Dpi);
        if (_themeTextCache.Count >= 512) _themeTextCache.Clear();
        _themeTextCache[key] = formatted; return formatted;
    }
    private Brush ThemeBrush(string color)
    {
        if (_themeBrushes.TryGetValue(color, out var cached)) return cached;
        try { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); if (_themeBrushes.Count >= 256) _themeBrushes.Clear(); _themeBrushes[color] = brush; return brush; }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or ArgumentException) { return Brushes.White; }
    }

    private void DrawTheme(DrawingContext dc, ThemeScene theme, Rect frame)
    {
        if (theme.OutputFrame is WpfSceneImage output)
        {
            var outputScale = Math.Min(frame.Width / output.Bitmap.PixelWidth, frame.Height / output.Bitmap.PixelHeight);
            var size = new Size(output.Bitmap.PixelWidth * outputScale, output.Bitmap.PixelHeight * outputScale);
            dc.DrawImage(output.Bitmap, new Rect(frame.X + (frame.Width - size.Width) / 2, frame.Y + (frame.Height - size.Height) / 2, size.Width, size.Height)); return;
        }
        var layout = theme.Project.ActiveLayout;
        var resolved = theme.ResolvedLayout;
        var canvasWidth = resolved?.SurfaceSize.Width ?? layout.Width;
        var canvasHeight = resolved?.SurfaceSize.Height ?? layout.Height;
        var scale = Math.Min(frame.Width / canvasWidth, frame.Height / canvasHeight);
        dc.PushClip(new RectangleGeometry(frame));
        dc.PushTransform(new TranslateTransform(frame.X + (frame.Width - canvasWidth * scale) / 2, frame.Y + (frame.Height - canvasHeight * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, canvasWidth, canvasHeight)));
        try
        {
            dc.DrawRectangle(ThemeBrush(layout.Background), null, new Rect(0, 0, canvasWidth, canvasHeight));
            foreach (var layer in layout.Layers.Where(l => l.Visible).OrderBy(l => l.ZIndex))
            {
                var placement = resolved?.Layers.FirstOrDefault(l => l.Source.Id == layer.Id);
                if (resolved is not null && placement is null) continue;
                var contentScale = placement?.ContentScale ?? 1;
                dc.PushTransform(new TranslateTransform(placement is null ? 0 : placement.Bounds.X - layer.X * contentScale,
                    placement is null ? 0 : placement.Bounds.Y - layer.Y * contentScale));
                dc.PushTransform(new ScaleTransform(contentScale, contentScale));
                dc.PushOpacity(layer.Opacity);
                dc.PushTransform(new RotateTransform(layer.Rotation, layer.X + layer.Width / 2, layer.Y + layer.Height / 2));
                var rect = new Rect(layer.X, layer.Y, layer.Width, layer.Height);
                dc.PushClip(new RectangleGeometry(rect));
                try
                {
                    theme.Children.TryGetValue(layer.Id, out var child);
                    if (layer.Kind is ThemeElementKind.Wallpaper or ThemeElementKind.Image)
                    {
                        if (child is ImageScene image && image.Image is WpfSceneImage pixels)
                        {
                            var ratio = layer.Fill == ImageDisplayMode.Fill ? Math.Max(rect.Width / pixels.Bitmap.PixelWidth, rect.Height / pixels.Bitmap.PixelHeight) : Math.Min(rect.Width / pixels.Bitmap.PixelWidth, rect.Height / pixels.Bitmap.PixelHeight);
                            ratio *= layer.ImageZoom;
                            var width = layer.Fill == ImageDisplayMode.Stretch ? rect.Width : pixels.Bitmap.PixelWidth * ratio;
                            var height = layer.Fill == ImageDisplayMode.Stretch ? rect.Height : pixels.Bitmap.PixelHeight * ratio;
                            if (layer.Fill == ImageDisplayMode.Center) { width = pixels.Bitmap.PixelWidth; height = pixels.Bitmap.PixelHeight; }
                            dc.DrawImage(pixels.Bitmap, new Rect(rect.X + (rect.Width - width) * Math.Clamp(layer.CropX, 0, 1), rect.Y + (rect.Height - height) * Math.Clamp(layer.CropY, 0, 1), width, height));
                        }
                        else if (child is VideoScene video && video.Playback is WindowsVideoPlaybackService { Player: { } player } && player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
                        {
                            var ratio = Math.Min(rect.Width / player.NaturalVideoWidth, rect.Height / player.NaturalVideoHeight);
                            var w = player.NaturalVideoWidth * ratio; var h = player.NaturalVideoHeight * ratio;
                            dc.DrawVideo(player, new Rect(rect.X + (rect.Width - w) / 2, rect.Y + (rect.Height - h) / 2, w, h));
                        }
                        else DrawThemeText(dc, layer, "素材不可用", rect);
                        if (layer.Brightness < 1) { dc.PushOpacity(1 - layer.Brightness); dc.DrawRectangle(Brushes.Black, null, rect); dc.Pop(); }
                    }
                    else if (layer.Kind == ThemeElementKind.Rectangle) dc.DrawRectangle(ThemeBrush(layer.Color), null, rect);
                    else if (layer.Kind == ThemeElementKind.Ellipse) dc.DrawEllipse(ThemeBrush(layer.Color), null, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
                    else if (child is StockScene stocks) DrawStocks(dc, stocks, rect);
                    else if (child is RetroCameraClockScene camera) DrawRetroCameraClock(dc, camera, rect);
                    else if (layer.Kind is ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock) DrawThemeText(dc, layer, "插件未启用", rect);
                    else DrawThemeText(dc, layer, layer.GetText(theme.CurrentTime), rect);
                }
                finally { dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); }
            }
        }
        finally { dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); }
    }

    private void DrawThemeText(DrawingContext dc, ThemeLayer layer, string value, Rect rect)
    {
        var brush = ThemeBrush(layer.Color);
        FormattedText Format(string text, Brush color) => FormatThemeText(layer, text, ReferenceEquals(color, Brushes.Black) ? "#000000" : layer.Color);
        if (layer.Kind == ThemeElementKind.Text)
        {
            var lines = ThemeTextMetrics.Lines(value); var lineHeight = Format("Mg国", brush).Height;
            var height = lineHeight * lines.Length;
            var top = rect.Y + (layer.VerticalAlignment == ThemeVerticalAlignment.Top ? 0 : layer.VerticalAlignment == ThemeVerticalAlignment.Bottom ? rect.Height - height : (rect.Height - height) / 2);
            foreach (var line in lines)
            {
                var glyphs = layer.Spacing == 0 ? new[] { line } : StringInfo.GetTextElementEnumerator(line).AsElements().ToArray();
                var advance = glyphs.Select(g => Format(g, brush).WidthIncludingTrailingWhitespace).ToArray();
                var width = advance.Sum() + Math.Max(0, glyphs.Length - 1) * layer.Spacing;
                var left = rect.X + (layer.Alignment == "居左" ? 0 : layer.Alignment == "居右" ? rect.Width - width : (rect.Width - width) / 2);
                for (var i = 0; i < glyphs.Length; i++) { if (layer.Shadow) dc.DrawText(Format(glyphs[i], Brushes.Black), new Point(left + 3, top + 3)); dc.DrawText(Format(glyphs[i], brush), new Point(left, top)); left += advance[i] + layer.Spacing; }
                top += lineHeight;
            }
            return;
        }
        if (layer.Kind == ThemeElementKind.DigitalClock && layer.ClockAppearance != DigitalClockAppearance.Standard) { DigitalClockDrawing.Draw(dc, layer, value, rect, ThemeBrush(layer.Color)); return; }
        // Give every clock digit the same advance, including proportional installed fonts.
        var tabular = layer.Kind == ThemeElementKind.DigitalClock;
        var digitWidth = tabular ? "0123456789".Max(c => Format(c.ToString(), brush).WidthIncludingTrailingWhitespace) : 0;
        var pieces = layer.Spacing == 0 && !tabular ? new[] { value } : StringInfo.GetTextElementEnumerator(value).AsElements().ToArray();
        var widths = pieces.Select(p => Format(p, brush).WidthIncludingTrailingWhitespace).ToArray();
        for (var i = 0; i < widths.Length; i++) if (tabular && pieces[i].Length == 1 && char.IsAsciiDigit(pieces[i][0])) widths[i] = digitWidth;
        var total = widths.Sum() + Math.Max(0, pieces.Length - 1) * layer.Spacing;
        var x = rect.X + (layer.Alignment == "居左" ? 0 : layer.Alignment == "居右" ? rect.Width - total : (rect.Width - total) / 2);
        for (var i = 0; i < pieces.Length; i++)
        {
            var text = Format(pieces[i], brush); var y = rect.Y + (rect.Height - text.Height) / 2;
            var glyphX = x + (widths[i] - text.WidthIncludingTrailingWhitespace) / 2;
            if (layer.Shadow) dc.DrawText(Format(pieces[i], Brushes.Black), new Point(glyphX + 3, y + 3));
            dc.DrawText(text, new Point(glyphX, y)); x += widths[i] + layer.Spacing;
        }
    }
}

internal static class ThemeTextElements
{
    public static IEnumerable<string> AsElements(this TextElementEnumerator enumerator) { while (enumerator.MoveNext()) yield return enumerator.GetTextElement(); }
}


