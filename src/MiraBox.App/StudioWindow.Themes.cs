using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    CoreWebView2Environment? webEnvironment;
    sealed class ThemeSession(StudioThemeRenderer renderer, D92StaticOutput output, SavedDevice device, int fps)
    {
        public StudioThemeRenderer Renderer = renderer;
        public D92StaticOutput Output = output;
        public Guid Submission = output.SubmissionId;
        public SavedDevice Device = device;
        public int Fps = fps;
        public long NextCapture, LastPreview, Captures, LastUsbFrames, LastDistinctUsbFrames;
        public byte[]? LastJpeg;
        public double CaptureMilliseconds, ActualFps, UsbFps;
        public bool Paused;
        public int Failures;
        public long Started = System.Diagnostics.Stopwatch.GetTimestamp();

    }
    readonly Dictionary<string, ThemeSession> themeSessions = new();
    readonly Dictionary<string, string> themePreviews = new();
    readonly Dictionary<string, string> themeErrors = new();
    readonly Dictionary<string, (SavedDevice Previous, DateTimeOffset Expires)> temporaryThemes = new();
    readonly DispatcherTimer themeTimer = new() { Interval = TimeSpan.FromMilliseconds(8) };
    static bool IsRenderedTheme(JsonNode theme) => theme["editorProject"] is JsonObject || theme["category"]?.GetValue<string>() is "主题" or "动态时钟";
    static bool CanOutputLayer(JsonObject layer) => layer["kind"]?.GetValue<string>() switch
    {
        "widget" => layer["widgetType"]?.GetValue<string>() is
            "clock-widget" or "date-card" or "sticky-note" or "progress" or "ring-progress",
        "plugin" => layer["capabilityKey"]?.GetValue<string>() is
            "quick-notes" or "todo-list" or "nixie-clock" or "flip-clock" or
            "desktop-aquarium" or "campfire" or "zen-bonsai" or "cyber-pet",
        _ => true,
    };

    static int SelectThemeLayout(JsonArray layouts, SavedDevice device)
    {
        var geometry = D92StaticImage.Geometry(device.Portrait ? 0 : 90, device.Model);
        var fallback = -1;
        for (int i = 0; i < layouts.Count; i++)
        {
            var size = layouts[i]!["size"]!.AsArray();
            var width = size[0]!.GetValue<int>();
            var height = size[1]!.GetValue<int>();
            if (width == geometry.Width && height == geometry.Height) return i;
            if (fallback < 0 && (width < height) == device.Portrait) fallback = i;
        }
        return Math.Max(fallback, 0);
    }

    string? ThemeIssue(JsonNode theme, SavedDevice? device)
    {
        string? CheckAssets(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (var child in obj)
                    if (CheckAssets(child.Value) is { } error) return error;
            }
            else if (node is JsonArray array)
            {
                foreach (var child in array)
                    if (CheckAssets(child) is { } error) return error;
            }
            else if (node is JsonValue value && value.TryGetValue<string>(out var text))
            {
                if (text.StartsWith("blob:", StringComparison.Ordinal)) return "主题包含临时素材，请重新导入并保存。";
                if ((text.StartsWith("https://media.studio.local/", StringComparison.Ordinal) || text.StartsWith("/src/assets/", StringComparison.Ordinal)) && ResolveMediaUrl(text) == null)
                    return "主题图片已缺失，请恢复素材或重新导入。";
            }
            return null;
        }

        if (theme["editorProject"] is JsonObject project)
        {
            if (project["layouts"] is not JsonArray layouts || layouts.Count == 0 || project["master"] is not JsonArray master)
                return "主题工程缺少画布或图层，请重新保存。";
            foreach (var layout in layouts)
                if (layout?["size"] is not JsonArray size || size.Count != 2 || size.Any(n => n!.GetValue<double>() is < 1 or > 8192))
                    return "主题画布尺寸无效。";
            if (CheckAssets(project["bg"]) is { } backgroundError) return backgroundError;

            var first = device == null ? 0 : SelectThemeLayout(layouts, device);
            var last = device == null ? layouts.Count : first + 1;
            for (int index = first; index < last; index++)
            {
                var layout = layouts[index]!;
                var followsMaster = index > 0 && layout["follow"]?.GetValue<bool>() == true;
                var elements = index == 0 || followsMaster ? master : layout["elements"] as JsonArray ?? master;
                var visibility = followsMaster ? layout["vis"] as JsonObject : null;
                foreach (var node in elements)
                {
                    if (node is not JsonObject layer) continue;
                    var id = layer["id"]?.GetValue<string>();
                    var hidden = (id != null ? visibility?[id]?.GetValue<bool>() : null) ?? layer["hide"]?.GetValue<bool>() ?? false;
                    if (hidden) continue;
                    if (!CanOutputLayer(layer))
                    {
                        var name = layer["name"]?.GetValue<string>() ?? layer["label"]?.GetValue<string>() ??
                            layer["pluginName"]?.GetValue<string>() ?? layer["widgetType"]?.GetValue<string>() ?? id ?? "未命名图层";
                        var size = layout["size"]!;
                        return $"布局 {size[0]} × {size[1]} 的图层「{name}」暂不支持实屏输出。可隐藏该图层后重试。";
                    }
                    if (CheckAssets(layer) is { } layerError) return layerError;
                }
            }
        }
        return CheckAssets(theme["imageUrl"]);
    }
    SavedDevice PrepareRenderedTheme(SavedDevice device, JsonNode theme)
    {
        if (ThemeIssue(theme, device) is { } issue) throw new InvalidOperationException(issue);
        var text = theme.ToJsonString();
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        var file = "theme-" + hash + ".json";
        File.WriteAllText(Path.Combine(dataRoot, file), text);
        var poster = "theme-" + hash + "-" + device.Serial + "-" + device.Portrait + ".image";
        if (!File.Exists(Path.Combine(dataRoot, poster)))
            File.WriteAllBytes(Path.Combine(dataRoot, poster), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII="));
        return device with { ThemeId = theme["id"]!.GetValue<string>(), ThemeName = theme["displayName"]!.GetValue<string>(),
            ImageFile = poster, ThemeFile = file, VideoFile = null, Playing = true };
    }
    async Task<(StudioThemeRenderer Renderer, byte[] Jpeg)> OpenTheme(SavedDevice device)
    {
        var path = Path.Combine(dataRoot, Path.GetFileName(device.ThemeFile!));
        var theme = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))! :
            FindTheme(device.ThemeId ?? "")?.DeepClone() ?? throw new FileNotFoundException("主题工程已缺失，请重新应用主题。");
        if (!File.Exists(path)) File.WriteAllText(path, theme.ToJsonString());
        if (ThemeIssue(theme, device) is { } issue) throw new InvalidOperationException(issue);
        var geometry = D92StaticImage.Geometry(device.Portrait ? 0 : 90, device.Model);
        int? layoutIndex = theme["editorProject"]?["layouts"] is JsonArray layouts
            ? SelectThemeLayout(layouts, device)
            : null;
        var renderer = new StudioThemeRenderer(geometry.Width, geometry.Height,
            (geometry.Rotation + (device.Flipped ? 180 : 0)) % 360);
        try
        {
            await renderer.Initialize(webEnvironment ?? throw new InvalidOperationException("渲染环境尚未启动。"), library.MediaRoot, theme, device.Portrait, layoutIndex, shutdown.Token);
            return (renderer, await renderer.CaptureJpeg(device.Model));
        }
        catch { renderer.Dispose(); throw; }
    }
    static string EncodePreview(BitmapSource frame)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var stream = new MemoryStream(); encoder.Save(stream);
        return "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
    }
    void TickThemes()
    {
        if (closing || systemSuspended) return;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var (serial, session) in themeSessions.ToArray())
        {
            if (now < session.NextCapture || !saved.TryGetValue(serial, out var device) || !device.Playing ||
                standbySerials.Contains(serial) || !connectedSerials.Contains(serial)) continue;
            var gate = OutputGate(serial);
            if (gate.Wait(0)) _ = CaptureThemeFrame(serial, session, device, gate, now);
        }
    }

    async Task CaptureThemeFrame(string serial, ThemeSession session, SavedDevice device, SemaphoreSlim gate, long now)
    {
        try
        {
            var period = System.Diagnostics.Stopwatch.Frequency / session.Fps;
            session.NextCapture = now - session.NextCapture >= period ? now + period : session.NextCapture + period;
            var jpeg = await session.Renderer.CaptureJpeg(device.Model);
            bool newFrame = !ReferenceEquals(session.LastJpeg, jpeg) && (session.LastJpeg == null || !session.LastJpeg.AsSpan().SequenceEqual(jpeg));
            if (newFrame && !session.Output.RefreshStill(session.Submission, jpeg)) return;
            if (newFrame) session.LastJpeg = jpeg;
            if (newFrame)
            {
                session.CaptureMilliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(now).TotalMilliseconds;
                session.Captures++;
            }
            if (System.Diagnostics.Stopwatch.GetElapsedTime(session.LastPreview).TotalSeconds >= 1)
            {
                themePreviews[serial] = EncodePreview(session.Renderer.DecodePreview(jpeg));
                session.LastPreview = now;
            }

            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(session.Started).TotalSeconds;
            if (elapsed >= 3)
            {
                var usbFrames = session.Output.Frames;
                session.ActualFps = session.Captures / elapsed;
                var distinctUsbFrames = session.Output.DistinctLiveFrames;
                var writesPerSecond = (usbFrames - session.LastUsbFrames) / elapsed;
                session.UsbFps = session.Fps > 1 ? (distinctUsbFrames - session.LastDistinctUsbFrames) / elapsed : writesPerSecond;
                if (session.Fps > 1)
                    Log(new { at = DateTimeOffset.Now, serial, @event = "theme-performance", targetFps = session.Fps,
                        renderFps = session.ActualFps, usbFps = writesPerSecond,
                        captureMs = session.Captures > 0 ? session.CaptureMilliseconds / session.Captures : 0,
                        distinctUsbFps = session.UsbFps,
                        usbWriteMs = session.Output.LastWriteMilliseconds });
                session.Started = System.Diagnostics.Stopwatch.GetTimestamp();
                session.LastUsbFrames = usbFrames;
                session.LastDistinctUsbFrames = distinctUsbFrames;
                session.Captures = 0;
                session.CaptureMilliseconds = 0;
            }
            themeErrors.Remove(serial);
            session.Failures = 0;
        }
        catch (Exception ex)
        {
            var message = "主题更新失败：" + ex.Message;
            if (themeErrors.GetValueOrDefault(serial) != message) Log(new { at = DateTimeOffset.Now, serial, error = message });
            themeErrors[serial] = message;
            session.Failures++;
        }
        finally { gate.Release(); }
    }
    static bool ThemeHasMotion(JsonNode? node) => node switch
    {
        JsonObject obj => obj["kind"]?.GetValue<string>() is "video" or "motion" ||
            obj["capabilityKey"]?.GetValue<string>() is "desktop-aquarium" or "campfire" or "cyber-pet" || obj.Any(p => ThemeHasMotion(p.Value)),
        JsonArray array => array.Any(ThemeHasMotion),
        _ => false
    };
    async Task EndThemePreview(string serial, bool restoreOutput)
    {
        if (!temporaryThemes.Remove(serial, out var preview)) return;
        saved[serial] = preview.Previous;
        playlistTicks[serial] = System.Diagnostics.Stopwatch.GetTimestamp();
        if (restoreOutput)
        {
            if (preview.Previous.Playing && connectedSerials.Contains(serial)) await Start(preview.Previous);
            else await Stop(serial);
        }
        Save();
    }
}
