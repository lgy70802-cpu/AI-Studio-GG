using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace MiraBox.App;

public partial class MainWindow
{
    static string JsText(JsonElement value, string key, string fallback = "") =>
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? fallback : fallback;
    static double JsNumber(JsonElement value, string key, double fallback = 0) =>
        value.TryGetProperty(key, out var item) && item.TryGetDouble(out var number) ? number : fallback;
    static bool JsFlag(JsonElement value, string key) =>
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.True;

    FrameworkElement EnhancedThemeArtwork(JsonElement data, int index, MiraBox.Core.Size size, DateTimeOffset? time,
        System.Windows.Media.Imaging.BitmapSource? videoFrame = null, bool transparentBackground = false)
    {
        var width = size.Width;
        var height = size.Height;
        var minimum = Math.Min(width, height);
        var root = new Grid { Width = width, Height = height, Background = transparentBackground ? Brushes.Transparent : Brushes.Black, ClipToBounds = true };
        var background = data.GetProperty("bg");
        var baked = data.TryGetProperty("hostBackgrounds", out var backgrounds) ?
            ThemeAssetPath(JsText(backgrounds, $"{width}x{height}")) : null;
        if (baked != null && File.Exists(baked) && JsText(background, "kind") != "video")
            root.Children.Add(new Image { Source = Load(baked, width), Stretch = Stretch.Fill });
        else if (!JsFlag(background, "hide"))
        {
            var kind = JsText(background, "kind", "style");
            var fill = kind == "color" ? ColorBrush(JsText(background, "color", "#25314b")) :
                kind == "gradient" ? new LinearGradientBrush(
                    ((SolidColorBrush)ColorBrush(JsText(background, "g1", "#2468e8"))).Color,
                    ((SolidColorBrush)ColorBrush(JsText(background, "g2", "#0b97c2"))).Color,
                    JsNumber(background, "angle", 135) - 90) : EnhancedPresetBrush(JsText(background, "style", "paper"));
            var back = new Grid { Width = width, Height = height, Background = fill, RenderTransformOrigin = new(.5, .5) };
            var path = ThemeAssetPath(JsText(background, kind == "video" ? "poster" : "url"));
            if (kind == "video" && videoFrame != null || path != null && File.Exists(path)) back.Children.Add(new Image { Source = kind == "video" && videoFrame != null ? videoFrame : Load(path!, width),
                Stretch = JsText(background, "fit") switch { "contain" => Stretch.Uniform, "stretch" => Stretch.Fill, _ => Stretch.UniformToFill } });
            var transform = new TransformGroup();
            var scale = JsNumber(background, "sc", 1);
            transform.Children.Add(new ScaleTransform(scale, scale));
            transform.Children.Add(new RotateTransform(JsNumber(background, "rot")));
            transform.Children.Add(new TranslateTransform(JsNumber(background, "tx") * width / 100, JsNumber(background, "ty") * height / 100));
            back.RenderTransform = transform;
            if (JsNumber(background, "blur") > 0) back.Effect = new BlurEffect { Radius = JsNumber(background, "blur") * minimum / 100 };
            root.Children.Add(back);
            root.Children.Add(new Rectangle { Fill = Brushes.Black, Opacity = JsNumber(background, "dim") / 100 });
        }
        var canvas = new Canvas { Width = width, Height = height };
        root.Children.Add(canvas);
        var layout = data.GetProperty("layouts")[index];
        var follows = index > 0 && JsFlag(layout, "follow");
        var elements = index == 0 || follows || !layout.TryGetProperty("elements", out _) ? data.GetProperty("master") : layout.GetProperty("elements");
        foreach (var element in elements.EnumerateArray())
        {
            var id = JsText(element, "id");
            var hidden = JsFlag(element, "hide");
            if (follows && layout.TryGetProperty("vis", out var visibility) && visibility.TryGetProperty(id, out var visible)) hidden = visible.GetBoolean();
            if (hidden) continue;
            var kind = JsText(element, "kind");
            FrameworkElement view;
            if (kind == "shape")
                view = EnhancedDecoration(element, width, height);
            else if (kind is "image" or "video")
            {
                var path = ThemeAssetPath(JsText(element, kind == "video" ? "poster" : "src"));
                var elementHeight = JsNumber(element, "size", 30) * minimum / 100;
                view = new Image { Width = elementHeight * JsNumber(element, "ar", 1), Height = elementHeight,
                    Source = kind == "video" && videoFrame != null ? videoFrame : path != null && File.Exists(path) ? Load(path, width) : null, Stretch = Stretch.Fill };
            }
            else
            {
                var current = time ?? new DateTimeOffset(2026, 10, 30, 10, 24, 36, TimeSpan.Zero);
                var zone = JsText(element, "tz");
                if (time != null && zone.Length > 0)
                    current = TimeZoneInfo.ConvertTime(current, TimeZoneInfo.FindSystemTimeZoneById(zone switch
                    { "JP" => "Tokyo Standard Time", "GB" => "GMT Standard Time", "US" => "Eastern Standard Time", _ => TimeZoneInfo.Local.Id }));
                var text = JsText(element, "text", "文字");
                if (kind == "clock")
                {
                    text = current.ToString(JsFlag(element, "h24") ? "HH" : "hh", CultureInfo.InvariantCulture) +
                        (width < height ? "\n" : ":") + current.ToString("mm");
                    if (JsFlag(element, "sec") && width >= height) text += ":" + current.ToString("ss");
                    if (!JsFlag(element, "h24")) text += current.Hour >= 12 ? " PM" : " AM";
                }
                if (kind == "date") text = (int)JsNumber(element, "fmt") switch
                {
                    1 => current.ToString("dddd · MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant(),
                    2 => current.ToString("yyyy-MM-dd"),
                    3 => current.ToString("MM.dd ddd", CultureInfo.InvariantCulture).ToUpperInvariant(),
                    _ => current.ToString("M月d日 ") + "周" + "日一二三四五六"[(int)current.DayOfWeek]
                };
                view = EnhancedStyledText(element, text, Math.Max(1, JsNumber(element, "size", kind == "date" ? 9 : 12) * minimum / 100),
                    (int)JsNumber(element, "weight", 500), kind == "date" ? .04 : 0);
                if (kind == "clock")
                    view = JsText(element, "style") == "analog" ? EnhancedAnalogClock(element, current, minimum) : JsText(element, "style") == "flip" ?
                        EnhancedFlipClock(element, current, JsNumber(element, "size", 30) * minimum / 100, width < height) :
                        EnhancedDigitalClock(element, current, JsNumber(element, "size", 30) * minimum / 100, width < height);
            }
            view.Opacity = Math.Clamp(JsNumber(element, "alpha", 1), 0, 1);
            view.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
            var anchor = JsText(element, "a", "mc");
            var ax = anchor[1] == 'l' ? 0 : anchor[1] == 'r' ? 1 : .5;
            var ay = anchor[0] == 't' ? 0 : anchor[0] == 'b' ? 1 : .5;
            Canvas.SetLeft(view, ax * width + JsNumber(element, "x") * width / 100 - ax * view.DesiredSize.Width);
            Canvas.SetTop(view, ay * height + JsNumber(element, "y") * height / 100 - ay * view.DesiredSize.Height);
            view.RenderTransformOrigin = new(.5, .5);
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(JsNumber(element, "sx", 1), JsNumber(element, "sy", 1)));
            transform.Children.Add(new RotateTransform(JsNumber(element, "rot")));
            view.RenderTransform = transform;
            canvas.Children.Add(view);
        }
        return root;
    }

    FrameworkElement EnhancedDigitalClock(JsonElement element, DateTimeOffset time, double fontSize, bool portrait)
    {
        var outer = new StackPanel { Orientation = portrait ? Orientation.Vertical : Orientation.Horizontal };
        var ink = ColorBrush(JsText(element, "color", "#ffffff"));
        var weight = JsText(element, "style") == "minimal" ? 300 : (int)JsNumber(element, "weight", 600);
        FrameworkElement Label(string text, double size) => EnhancedStyledText(element, text, size, weight, JsText(element, "style") == "minimal" ? .14 : 0, portrait ? .95 : 1.08);
        var hour = time.ToString(JsFlag(element, "h24") ? "HH" : "%h");
        var minute = time.ToString("mm");
        if (portrait) { outer.Children.Add(Label(hour, fontSize)); outer.Children.Add(Label(minute, fontSize)); }
        else
        {
            var mainText = hour + (JsText(element, "style") == "minimal" ? "·" : ":") + minute;
            FrameworkElement main = EnhancedStyledText(element, mainText, fontSize, weight, JsText(element, "style") == "minimal" ? .14 : 0);
            if (main is TextBlock digits) System.Windows.Documents.Typography.SetNumeralAlignment(digits, FontNumeralAlignment.Tabular);
            outer.Children.Add(main);
        }
        if (!JsFlag(element, "h24"))
        {
            var period = Label(time.Hour < 12 ? "AM" : "PM", fontSize * .28);
            period.VerticalAlignment = VerticalAlignment.Top;
            period.Margin = new Thickness(fontSize * .12, fontSize * .14, 0, 0);
            period.Opacity = .8;
            outer.Children.Add(period);
        }
        if (JsFlag(element, "sec") && !portrait)
        {
            var seconds = Label(time.ToString("ss"), fontSize * .4);
            seconds.VerticalAlignment = VerticalAlignment.Bottom;
            seconds.Margin = new Thickness(fontSize * .12, 0, 0, fontSize * .14);
            seconds.Opacity = .7;
            outer.Children.Add(seconds);
        }
        return outer;
    }

    static FrameworkElement EnhancedSpacedText(string text, double fontSize, Brush ink, int weight, double spacing)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var character in text)
        {
            var label = new TextBlock
            {
                Text = character.ToString(), FontFamily = new FontFamily("Segoe UI"), FontSize = fontSize,
                Foreground = ink, FontWeight = FontWeight.FromOpenTypeWeight(weight),
                LineHeight = fontSize * 1.08, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Margin = new Thickness(0, 0, fontSize * spacing, 0)
            };
            line.Children.Add(label);
        }
        return line;
    }

    FrameworkElement EnhancedFlipClock(JsonElement element, DateTimeOffset time, double fontSize, bool portrait)
    {
        var ink = ColorBrush(JsText(element, "color", "#ffffff"));
        var outer = new StackPanel { Orientation = portrait ? Orientation.Vertical : Orientation.Horizontal };
        var hour = time.ToString(JsFlag(element, "h24") ? "HH" : "%h");
        outer.Children.Add(Digits(hour));
        if (!portrait) { var colon = EnhancedStyledText(element, ":", fontSize * .6, 600); colon.VerticalAlignment = VerticalAlignment.Center; colon.Opacity = .75; outer.Children.Add(colon); }
        outer.Children.Add(Digits(time.ToString("mm")));
        if (!JsFlag(element, "h24")) { var period = EnhancedStyledText(element, time.Hour < 12 ? "AM" : "PM", fontSize * .28, 600);
            period.Opacity = .8; period.VerticalAlignment = VerticalAlignment.Top; period.Margin = new Thickness(fontSize * .12, fontSize * .14, 0, 0); outer.Children.Add(period); }
        if (JsFlag(element, "sec") && !portrait) { var seconds = EnhancedStyledText(element, time.ToString("ss"), fontSize * .4, 600);
            seconds.Opacity = .7; seconds.VerticalAlignment = VerticalAlignment.Bottom; seconds.Margin = new Thickness(fontSize * .12, 0, 0, fontSize * .14); outer.Children.Add(seconds); }
        return outer;

        FrameworkElement Digits(string value)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var digit in value)
            {
                var face = new Grid();
                var label = EnhancedStyledText(element, digit.ToString(), fontSize * .82, (int)JsNumber(element, "weight", 700));
                label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
                face.Children.Add(label);
                face.Children.Add(new Border { Height = 1, Background = ColorBrush("#8C000000"), VerticalAlignment = VerticalAlignment.Center });
                line.Children.Add(new Border { Child = face, Width = fontSize * .78 * .82, Height = fontSize * 1.08 * .82,
                    CornerRadius = new CornerRadius(fontSize * .13 * .82), Background = ColorBrush("#D6121A2C"), Margin = new Thickness(fontSize * .025, 0, fontSize * .025, 0) });
            }
            return line;
        }
    }

    static Brush EnhancedPresetBrush(string style)
    {
        var palette = style switch
        {
            "paper" => new[] { "#e7dfcf", "#e7dfcf" },
            "ink" => new[] { "#152035", "#344b68" },
            "pink" => new[] { "#ebb9c3", "#b47f9a" },
            "ocean" => new[] { "#0f3a5f", "#2a7fa6", "#a8d8e0" },
            "night" => new[] { "#070b1f", "#1a2250" },
            "mint" => new[] { "#b9efd9", "#5fc4b0" },
            "neon" => new[] { "#1a0b3d", "#6a1b9a", "#00bcd4" },
            "dusk" => new[] { "#2b1b4a", "#c0577a", "#f2a65a" },
            "mono" => new[] { "#2a2d34", "#5b616d" },
            "sand" => new[] { "#efe3c8", "#d3b98a" },
            "ember" => new[] { "#3a0f0a", "#c2410c", "#f59e0b" },
            "plum" => new[] { "#2d1240", "#7a3b8f", "#d489b8" },
            "ice" => new[] { "#cfe6f2", "#7fb3d5", "#3b6a8c" },
            "forest" => new[] { "#10361f", "#2f7d4f", "#a5d6a7" },
            "aurora" => new[] { "#142c46", "#2e5b67", "#192339" },
            "sun" => new[] { "#475a7f", "#d6948c", "#172f4d" },
            "mountain" => new[] { "#b8d4cb", "#527f7d", "#204c55" },
            _ => new[] { "#152035", "#344b68" }
        };
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        for (var index = 0; index < palette.Length; index++)
            brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(palette[index]), (double)index / (palette.Length - 1)));
        return brush;
    }

    string? ThemeAssetPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "workspace.local") return null;
        var path = Path.GetFullPath(Path.Combine(DataRoot, Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))));
        return path.StartsWith(Path.GetFullPath(DataRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
