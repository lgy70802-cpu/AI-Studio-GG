using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Services;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    Func<IVideoFrameComposition>? CreateEnhancedVideoComposition(string? resourceId)
    {
        if (resourceId == null || !state.Resources.TryGetValue(resourceId, out var resource) ||
            state.Themes.GetValueOrDefault(resourceId)?.EditorData is not { } data ||
            Workspace.BestLayout(resource, HardwareCanvas) is not { } match) return null;
        var size = HardwareCanvas;
        var background = data.GetProperty("bg");
        var videoBackground = JsText(background, "kind") == "video" && !JsFlag(background, "hide");
        // The full renderer handles blurred video backgrounds.
        if (videoBackground && JsNumber(background, "blur") > 0) return null;
        var layout = data.GetProperty("layouts")[match.Index];
        var follows = match.Index > 0 && JsFlag(layout, "follow");
        var elements = (match.Index == 0 || follows || !layout.TryGetProperty("elements", out _) ?
            data.GetProperty("master") : layout.GetProperty("elements")).EnumerateArray().ToArray();
        var videoIndex = Array.FindIndex(elements, element =>
        {
            var hidden = JsFlag(element, "hide");
            if (follows && layout.TryGetProperty("vis", out var visibility) &&
                visibility.TryGetProperty(JsText(element, "id"), out var flag)) hidden = flag.GetBoolean();
            return JsText(element, "kind") == "video" && !hidden;
        });
        if (!videoBackground && videoIndex < 0) return null;
        var video = videoBackground ? background.Clone() : elements[videoIndex].Clone();
        var belowData = Part(videoBackground ? [] : elements.Take(videoIndex), false);
        var aboveData = Part(videoBackground ? elements : elements.Skip(videoIndex + 1), true);
        var dynamicStaticLayers = elements.Any(element => JsText(element, "kind") is "clock" or "date");
        return () => new EnhancedVideoComposition(video, videoBackground, size.Width, size.Height,
            () => state.LocalTime, time => Dispatcher.Invoke(() => (Render(belowData, false, time), Render(aboveData, true, time))), dynamicStaticLayers)
            { ReuseEncoderBitmap = true };

        JsonElement Part(IEnumerable<JsonElement> part, bool transparent)
        {
            var document = JsonNode.Parse(data.GetRawText())!;
            if (transparent || videoBackground)
            {
                document["bg"] = JsonSerializer.SerializeToNode(new { kind = "color", color = "#000000", hide = transparent });
                document.AsObject().Remove("hostBackgrounds");
            }
            var array = new JsonArray();
            foreach (var element in part)
            {
                var node = JsonNode.Parse(element.GetRawText())!;
                if (follows && layout.TryGetProperty("vis", out var visibility) &&
                    visibility.TryGetProperty(JsText(element, "id"), out var hidden)) node["hide"] = hidden.GetBoolean();
                array.Add(node);
            }
            if (match.Index == 0) document["master"] = array;
            else
            {
                document["layouts"]![match.Index]!["follow"] = false;
                document["layouts"]![match.Index]!["elements"] = array;
            }
            return JsonSerializer.SerializeToElement(document);
        }

        BitmapSource Render(JsonElement document, bool transparent, DateTimeOffset time)
        {
            var view = EnhancedThemeArtwork(document, match.Index, size, time, transparentBackground: transparent);
            var dimensions = new System.Windows.Size(size.Width, size.Height);
            view.Measure(dimensions); view.Arrange(new Rect(dimensions));
            var bitmap = new RenderTargetBitmap(size.Width, size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view); bitmap.Freeze();
            return bitmap;
        }
    }
}
