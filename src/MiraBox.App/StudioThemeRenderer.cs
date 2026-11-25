using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    readonly Dispatcher encoder;
    readonly Thread encoderThread;
    public StudioThemeRenderer(int width, int height)
    {
        var started = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        encoderThread = new Thread(() => { started.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = "MiraBox theme JPEG" };
        encoderThread.SetApartmentState(ApartmentState.STA); encoderThread.Start(); encoder = started.Task.GetAwaiter().GetResult();
        window = new Window { Width = width, Height = height, Left = -20000, Top = -20000,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, ShowActivated = false, Content = view, Title = "MiraBox background renderer" };
    }
    public async Task Initialize(CoreWebView2Environment environment, string mediaRoot, JsonNode theme, bool portrait, int? layoutIndex, CancellationToken cancellationToken = default)
    {
        window.Show();
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
        view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "render-theme", token, theme, portrait, layoutIndex }));
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
    public async Task<BitmapSource> Capture()
    {
        if (disposed) throw new ObjectDisposedException(nameof(StudioThemeRenderer));
        if (failure != null) throw new IOException(failure);
        if (System.Diagnostics.Stopwatch.GetElapsedTime(heartbeat).TotalSeconds > 8) throw new IOException("主题渲染心跳中断，正在重建画布。");
        using var stream = new MemoryStream();
        if (latestFrame is { } bytes && System.Diagnostics.Stopwatch.GetElapsedTime(lastFrameAt).TotalSeconds < 2) stream.Write(bytes);
        else await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, stream).WaitAsync(TimeSpan.FromSeconds(3));
        stream.Position = 0;
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }
    public Task SetPaused(bool paused)
    {
        view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "render-paused", paused }));
        return Task.CompletedTask;
    }
    public Task<byte[]> Encode(BitmapSource frame, int rotation, string model) => encoder.InvokeAsync(() => D92StaticImage.Encode(rotation, frame, model)).Task;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (screencast != null) screencast.DevToolsProtocolEventReceived -= OnFrame;
        view.Dispose();
        window.Close();
        encoder.BeginInvokeShutdown(DispatcherPriority.Background);
        encoderThread.Join(TimeSpan.FromSeconds(2));
    }
}
