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
        public long LastCapture, LastPreview, Captures;
        public bool Paused;
        public int Failures;
        public long Started = System.Diagnostics.Stopwatch.GetTimestamp();
        public double ActualFps => Captures / Math.Max(1, System.Diagnostics.Stopwatch.GetElapsedTime(Started).TotalSeconds);
    }
    readonly Dictionary<string, ThemeSession> themeSessions = new();
    readonly Dictionary<string, string> themePreviews = new();
    readonly Dictionary<string, string> themeErrors = new();
    readonly Dictionary<string, (SavedDevice Previous, DateTimeOffset Expires)> temporaryThemes = new();
    readonly DispatcherTimer themeTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    bool themeTickBusy;
    static bool IsRenderedTheme(JsonNode theme) => theme["editorProject"] is JsonObject || theme["category"]?.GetValue<string>() is "主题" or "动态时钟";
    string? ThemeIssue(JsonNode theme)
    {
        if (theme["editorProject"] is JsonObject project)
        {
            if (project["layouts"] is not JsonArray layouts || layouts.Count == 0 || project["master"] is not JsonArray)
                return "主题工程缺少画布或图层，请重新保存。";
            foreach (var layout in layouts)
                if (layout?["size"] is not JsonArray size || size.Count != 2 || size.Any(n => n!.GetValue<double>() is < 1 or > 8192))
                    return "主题画布尺寸无效。";
        }
        string? Check(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj["kind"]?.GetValue<string>() is "plugin" or "widget") return "此主题包含尚未接入真实数据的组件或插件，请移除后输出。";
                foreach (var child in obj) { var error = Check(child.Value); if (error != null) return error; }
            }
            else if (node is JsonArray array) { foreach (var child in array) { var error = Check(child); if (error != null) return error; } }
            else if (node is JsonValue value && value.TryGetValue<string>(out var text))
            {
                if (text.StartsWith("blob:", StringComparison.Ordinal)) return "主题包含临时素材，请重新导入并保存。";
                if ((text.StartsWith("https://media.studio.local/", StringComparison.Ordinal) || text.StartsWith("/src/assets/", StringComparison.Ordinal)) && ResolveMediaUrl(text) == null)
                    return "主题图片已缺失，请恢复素材或重新导入。";
            }
            return null;
        }
        return Check(theme["editorProject"]) ?? (theme["imageUrl"] is JsonValue image ? Check(image) : null);
    }
    SavedDevice PrepareRenderedTheme(SavedDevice device, JsonNode theme)
    {
        if (ThemeIssue(theme) is { } issue) throw new InvalidOperationException(issue);
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
    async Task<(StudioThemeRenderer Renderer, BitmapSource Frame)> OpenTheme(SavedDevice device)
    {
        var path = Path.Combine(dataRoot, Path.GetFileName(device.ThemeFile!));
        var theme = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))! :
            FindTheme(device.ThemeId ?? "")?.DeepClone() ?? throw new FileNotFoundException("主题工程已缺失，请重新应用主题。");
        if (!File.Exists(path)) File.WriteAllText(path, theme.ToJsonString());
        if (ThemeIssue(theme) is { } issue) throw new InvalidOperationException(issue);
        var geometry = D92StaticImage.Geometry(device.Portrait ? 0 : 90, device.Model);
        int? layoutIndex = null;
        if (theme["editorProject"]?["layouts"] is JsonArray layouts)
        {
            var exact = layouts.Select((n, i) => (Node: n!, Index: i)).FirstOrDefault(x =>
                x.Node["size"]![0]!.GetValue<int>() == geometry.Width && x.Node["size"]![1]!.GetValue<int>() == geometry.Height);
            layoutIndex = exact.Node != null ? exact.Index : layouts.Select((n, i) => (Node: n!, Index: i))
                .FirstOrDefault(x => (x.Node["size"]![0]!.GetValue<int>() < x.Node["size"]![1]!.GetValue<int>()) == device.Portrait).Index;
        }
        var renderer = new StudioThemeRenderer(geometry.Width, geometry.Height);
        try
        {
            await renderer.Initialize(webEnvironment ?? throw new InvalidOperationException("渲染环境尚未启动。"), library.MediaRoot, theme, device.Portrait, layoutIndex, shutdown.Token);
            return (renderer, await renderer.Capture());
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
    async Task TickThemes()
    {
        if (closing || systemSuspended || themeTickBusy) return;
        themeTickBusy = true;
        try
        {
            await Task.WhenAll(themeSessions.ToArray().Select(async entry =>
            {
                var (serial, session) = entry;
                if (!saved.TryGetValue(serial, out var device) || !device.Playing || standbySerials.Contains(serial) || !connectedSerials.Contains(serial)) return;
                if (System.Diagnostics.Stopwatch.GetElapsedTime(session.LastCapture).TotalSeconds < 1d / session.Fps) return;
                var gate = OutputGate(serial);
                if (!await gate.WaitAsync(0)) return;
                try
                {
                    if (!themeSessions.TryGetValue(serial, out var current) || current != session) return;
                    session.LastCapture = System.Diagnostics.Stopwatch.GetTimestamp();
                    var frame = await session.Renderer.Capture();
                    if (session.Output.RefreshStill(session.Submission, await session.Renderer.Encode(frame, session.Output.Rotation, device.Model)))
                    {
                        session.Captures++;
                        if (System.Diagnostics.Stopwatch.GetElapsedTime(session.LastPreview).TotalSeconds >= 1)
                        {
                        themePreviews[serial] = EncodePreview(frame);
                        session.LastPreview = session.LastCapture;
                        }
                        themeErrors.Remove(serial);
                        session.Failures = 0;
                    }
                }
                catch (Exception ex)
                {
                    var message = "主题更新失败：" + ex.Message;
                    if (themeErrors.GetValueOrDefault(serial) != message) Log(new { at = DateTimeOffset.Now, serial, error = message });
                    themeErrors[serial] = message;
                    session.Failures++;
                }
                finally { gate.Release(); }
            }));
        }
        finally { themeTickBusy = false; }
    }
    static bool ThemeHasMotion(JsonNode? node) => node switch
    {
        JsonObject obj => obj["kind"]?.GetValue<string>() is "video" or "motion" || obj.Any(p => ThemeHasMotion(p.Value)),
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
