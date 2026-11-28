using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MiraBox.App;

// This window hosts the original React artwork without editor chrome. It stays
// independent of the main window's navigation, visibility and editor state.
internal sealed class StudioThemeRenderer : IDisposable
{
    readonly Window window;
    readonly WebView2 view = new();
    readonly TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly string token = Guid.NewGuid().ToString("N");
    bool disposed;
    string? failure;
    byte[]? latestFrame;
    long lastFrameAt;
    long heartbeat = System.Diagnostics.Stopwatch.GetTimestamp();
    CoreWebView2DevToolsProtocolEventReceiver? screencast;
    readonly int targetWidth;
    readonly int targetHeight;
    readonly int rotation;
    public StudioThemeRenderer(int width, int height, int rotation = 0)
    {
        this.rotation = rotation;
        (targetWidth, targetHeight) = rotation is 90 or 270 ? (height, width) : (width, height);
        view.Width = targetWidth;
        view.Height = targetHeight;
        var host = new System.Windows.Controls.Canvas();
        host.Children.Add(view);
        window = new Window { Width = targetWidth, Height = targetHeight, Left = -20000, Top = -20000,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, ShowActivated = false, Content = host, Title = "MiraBox background renderer" };
    }

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    public async Task Initialize(CoreWebView2Environment environment, string mediaRoot, JsonNode theme, bool portrait, int? layoutIndex, CancellationToken cancellationToken = default)
    {
        window.Show();
        var handle = new WindowInteropHelper(window).Handle;
        var scale = GetDpiForWindow(handle) / 96d;
        // Bypass the desktop's maximum tracking size for this offscreen render surface.
        const uint preservePositionAndActivation = 0x0016;
        const uint noWindowPosChanging = 0x0400;
        SetWindowPos(handle, 0, 0, 0, (int)Math.Ceiling(targetWidth * scale), (int)Math.Ceiling(targetHeight * scale),
            preservePositionAndActivation | noWindowPosChanging);
        await view.EnsureCoreWebView2Async(environment);
        view.CoreWebView2.ProcessFailed += (_, e) => failure = "主题渲染进程中断：" + e.ProcessFailedKind;
        view.CoreWebView2.SetVirtualHostNameToFolderMapping("studio.local", Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
        view.CoreWebView2.SetVirtualHostNameToFolderMapping("media.studio.local", mediaRoot, CoreWebView2HostResourceAccessKind.Allow);
        view.CoreWebView2.NavigationStarting += (_, e) => { if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Host != "studio.local") e.Cancel = true; };
        view.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) || source.Host != "studio.local") return;
            var message = JsonNode.Parse(e.WebMessageAsJson);
            if (message?["event"]?.GetValue<string>() == "renderer-loaded") loaded.TrySetResult();
            if (message?["event"]?.GetValue<string>() == "render-heartbeat" && message["token"]?.GetValue<string>() == token)
                heartbeat = System.Diagnostics.Stopwatch.GetTimestamp();
            if (message?["event"]?.GetValue<string>() == "render-failed" && message["token"]?.GetValue<string>() == token)
                failure = message["error"]?.GetValue<string>() ?? "主题媒体解码中断。";
            if (message?["event"]?.GetValue<string>() == "render-ready" && message["token"]?.GetValue<string>() == token)
            {
                if (message["error"]?.GetValue<string>() is { } error) ready.TrySetException(new IOException(error));
                else ready.TrySetResult();
            }
        };
        view.Source = new Uri("https://studio.local/index.html?device-render=1");
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", JsonSerializer.Serialize(new
        {
            width = targetWidth, height = targetHeight, deviceScaleFactor = 1, mobile = false,
            screenWidth = targetWidth, screenHeight = targetHeight
        })).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "render-theme", token, theme, portrait, layoutIndex, rotation }));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        try
        {
            screencast = view.CoreWebView2.GetDevToolsProtocolEventReceiver("Page.screencastFrame");
            screencast.DevToolsProtocolEventReceived += OnFrame;
            await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.startScreencast", JsonSerializer.Serialize(new {
                format = "jpeg", quality = 85, maxWidth = (int)window.Width, maxHeight = (int)window.Height, everyNthFrame = 1 })).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch { if (screencast != null) screencast.DevToolsProtocolEventReceived -= OnFrame; screencast = null; }
    }
    async void OnFrame(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (disposed) return;
        try
        {
            using var message = JsonDocument.Parse(e.ParameterObjectAsJson);
            latestFrame = Convert.FromBase64String(message.RootElement.GetProperty("data").GetString()!);
            lastFrameAt = System.Diagnostics.Stopwatch.GetTimestamp();
            await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.screencastFrameAck", "{\"sessionId\":" + message.RootElement.GetProperty("sessionId").GetInt32() + "}");
        }
        catch (Exception) when (disposed) { }
        catch { /* CapturePreview remains available if a runtime drops the event stream. */ }
    }
    public BitmapSource DecodePreview(byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg, writable: false);
        BitmapSource frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        if (rotation != 0) frame = new TransformedBitmap(frame, new RotateTransform(360 - rotation));
        frame.Freeze();
        return frame;
    }

    public async Task<byte[]> CaptureJpeg(string transportModel)
    {
        if (disposed) throw new ObjectDisposedException(nameof(StudioThemeRenderer));
        if (failure != null) throw new IOException(failure);
        if (System.Diagnostics.Stopwatch.GetElapsedTime(heartbeat).TotalSeconds > 8) throw new IOException("主题渲染心跳中断，正在重建画布。");
        if (latestFrame is { } bytes && System.Diagnostics.Stopwatch.GetElapsedTime(lastFrameAt).TotalSeconds < 2)
        {
            try { D92StaticImage.RequireTransportRaster(bytes, transportModel); return bytes; }
            catch (InvalidOperationException) { /* Use an exact-size capture if the stream is scaled. */ }
        }

        var response = await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", JsonSerializer.Serialize(new
        {
            format = "jpeg", quality = 85, captureBeyondViewport = true,
            clip = new { x = 0, y = 0, width = targetWidth, height = targetHeight, scale = 1 }
        })).WaitAsync(TimeSpan.FromSeconds(3));
        using var result = JsonDocument.Parse(response);
        var jpeg = Convert.FromBase64String(result.RootElement.GetProperty("data").GetString()!);
        D92StaticImage.RequireTransportRaster(jpeg, transportModel);
        return jpeg;
    }
    public Task SetPaused(bool paused)
    {
        view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "render-paused", paused }));
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (screencast != null) screencast.DevToolsProtocolEventReceived -= OnFrame;
        view.Dispose();
        window.Close();
    }
}
