using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    string? ThemeVideoPath(Resource resource)
    {
        if (state.Themes.GetValueOrDefault(resource.Id)?.EditorData is not { } data ||
            Workspace.BestLayout(resource, HardwareCanvas) is not { } match) return null;
        var background = data.GetProperty("bg");
        if (JsText(background, "kind") == "video" && !JsFlag(background, "hide"))
            return ThemeAssetPath(JsText(background, "vurl"));
        var layout = data.GetProperty("layouts")[match.Index];
        var follows = match.Index > 0 && JsFlag(layout, "follow");
        var elements = match.Index == 0 || follows || !layout.TryGetProperty("elements", out _) ?
            data.GetProperty("master") : layout.GetProperty("elements");
        foreach (var element in elements.EnumerateArray())
        {
            var hidden = JsFlag(element, "hide");
            if (follows && layout.TryGetProperty("vis", out var vis) && vis.TryGetProperty(JsText(element, "id"), out var flag)) hidden = flag.GetBoolean();
            if (JsText(element, "kind") == "video" && !hidden) return ThemeAssetPath(JsText(element, "src"));
        }
        return null;
    }

    Func<byte[], byte[]>? CreateThemeVideoTransform(string? resourceId, string serial, int rotation)
    {
        if (resourceId == null || !state.Resources.TryGetValue(resourceId, out var resource) ||
            state.Themes.GetValueOrDefault(resourceId)?.EditorData is not { } data ||
            Workspace.BestLayout(resource, HardwareCanvas) is not { } match) return null;
        var model = HardwareModelFor(serial);
        var layoutIndex = match.Index;
        return bytes => Dispatcher.Invoke(() =>
        {
            var frame = DecodeHardwareHomeFrame(bytes, 0);
            return D92StaticImage.Encode(rotation, (context, size) =>
            {
                var view = EnhancedThemeArtwork(data, layoutIndex, new((int)size.Width, (int)size.Height), state.LocalTime, frame);
                view.Measure(size);
                view.Arrange(new Rect(size));
                context.DrawRectangle(new VisualBrush(view), null, new Rect(size));
            }, model);
        });
    }

    (int Width, int Height)? ThemeVideoDecodeSize(string? resourceId)
    {
        if (resourceId == null || state.Themes.GetValueOrDefault(resourceId)?.EditorData is not { } data) return null;
        var background = data.GetProperty("bg");
        var aspect = JsText(background, "kind") == "video" ? JsNumber(background, "ar", 16d / 9) : 0;
        if (aspect == 0)
        {
            foreach (var element in data.GetProperty("master").EnumerateArray())
                if (JsText(element, "kind") == "video") { aspect = JsNumber(element, "ar", 16d / 9); break; }
            if (aspect == 0)
                foreach (var layout in data.GetProperty("layouts").EnumerateArray())
                    if (layout.TryGetProperty("elements", out var elements))
                        foreach (var element in elements.EnumerateArray())
                            if (JsText(element, "kind") == "video") { aspect = JsNumber(element, "ar", 16d / 9); break; }
        }
        if (aspect <= 0) return null;
        var longestEdge = Math.Max(HardwareCanvas.Width, HardwareCanvas.Height);
        var width = Math.Min((double)longestEdge, longestEdge * aspect);
        var height = width / aspect;
        return (Math.Max(2, (int)width / 2 * 2), Math.Max(2, (int)height / 2 * 2));
    }
}
