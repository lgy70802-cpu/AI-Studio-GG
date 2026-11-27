using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MiraBox.Core;

namespace MiraBox.App;

// A dedicated WebView context per canvas/settings, shared by preview and USB rendering.
// The original package is not rewritten. The bridge implements the subset used by Woodland.
internal sealed class WoodlandRuntime : IDisposable
{
    readonly WebView2 browser = new() { Width = 1, Height = 1 };
    readonly Panel host;
    readonly CraftPluginEntry entry;
    readonly string package, cache;
    readonly bool hour12;
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool disposed;
    public WoodlandRuntime(Panel host, CraftPluginEntry entry, string package, string cache, int width, int height, bool hour12)
    {
        this.host = host; this.entry = entry; this.package = package; this.cache = cache;
        Width = width; Height = height; this.hour12 = hour12;
        if (File.Exists(cache))
            try { AcceptFrame(File.ReadAllBytes(cache), false); } catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidDataException) { }
        host.Children.Add(browser);
        _ = StartAsync();
    }
    public int Width { get; }
    public int Height { get; }
    public BitmapSource? Frame { get; private set; }
    public long Revision { get; private set; }
    public string? Error { get; private set; }
    public Task Ready => ready.Task;
    public event Action? FrameChanged;
    public int Viewers { get; set; }
    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;
    public byte[]? Encoded { get; set; }
    public (long Revision, int Rotation, string Model) EncodedKey { get; set; }

    async Task StartAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null,
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(cache))!, "Browser"));
            if (disposed) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (disposed) return;
            var core = browser.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("woodland.local", package, CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.NavigationStarting += (_, navigation) => {
                if (!navigation.Uri.StartsWith("https://woodland.local/", StringComparison.OrdinalIgnoreCase)) navigation.Cancel = true;
            };
            core.NewWindowRequested += (_, request) => request.Handled = true;
            core.PermissionRequested += (_, request) => request.State = CoreWebView2PermissionState.Deny;
            core.ProcessFailed += (_, failure) => Fail("插件浏览器进程异常：" + failure.ProcessFailedKind);
            core.WebMessageReceived += (_, message) => {
                try
                {
                    if (disposed || !message.Source.StartsWith("https://woodland.local/", StringComparison.OrdinalIgnoreCase)) return;
                    using var payload = JsonDocument.Parse(message.WebMessageAsJson);
                    var node = payload.RootElement;
                    if (node.GetProperty("event").GetString() == "frame")
                    {
                        var data = node.GetProperty("image").GetString()!;
                        const string prefix = "data:image/jpeg;base64,";
                        if (!data.StartsWith(prefix, StringComparison.Ordinal) || data.Length > 12 * 1024 * 1024)
                            throw new InvalidDataException("插件帧格式或大小无效。");
                        AcceptFrame(Convert.FromBase64String(data[prefix.Length..]), true);
                    }
                    else if (node.GetProperty("event").GetString() == "error") Fail(node.GetProperty("message").GetString()!);
                }
                catch (Exception ex) { Fail("插件画面接收失败：" + ex.Message); }
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(Bridge);
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            core.NavigationCompleted += (_, navigation) => {
                if (navigation.IsSuccess) loaded.TrySetResult();
                else loaded.TrySetException(new IOException("插件页面加载失败：" + navigation.WebErrorStatus));
            };
            core.Navigate("https://woodland.local/" + entry.CodePath);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (disposed) return;
            var args = JsonSerializer.Serialize(new { plugin = new { uuid = entry.Id } });
            var appear = JsonSerializer.Serialize(new {
                @event = "willAppear", action = entry.ActionId, context = "woodland-instance",
                payload = new { settings = new { ActionGeometry = new { width = Width, height = Height }, hour12 } }
            });
            await core.ExecuteScriptAsync($"window.__craftAppear={appear};window.connectSDSocket('0','woodland-instance','registerPlugin',{JsonSerializer.Serialize(args)});");
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (Exception ex) when (!disposed) { Fail(ex.Message); }
        catch (Exception) when (disposed) { }
    }

    void AcceptFrame(byte[] jpeg, bool persist)
    {
        using var stream = new MemoryStream(jpeg);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        if (frame.PixelWidth != Width || frame.PixelHeight != Height) throw new InvalidDataException("插件画布尺寸与目标不一致。");
        frame.Freeze(); Frame = frame; Revision++; Error = null;
        if (persist)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            var temporary = cache + ".tmp";
            File.WriteAllBytes(temporary, jpeg);
            File.Move(temporary, cache, true);
            ready.TrySetResult();
        }
        FrameChanged?.Invoke();
    }

    void Fail(string error)
    {
        Error = error;
        ready.TrySetException(new InvalidOperationException(error));
        FrameChanged?.Invoke();
    }

    // Deterministic wall-clock override is only used by the integration checks.
    internal Task SetTestTime(DateTimeOffset value) => browser.ExecuteScriptAsync(
        "window.__craftTestTime=" + value.ToUnixTimeMilliseconds() + ";window.__craftUseTestDate();");

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ready.TrySetCanceled();
        browser.Dispose(); host.Children.Remove(browser);
        FrameChanged = null;
    }

    const string Bridge = """
        (() => {
          const post = data => chrome.webview.postMessage(data);
          const sockets = new Set();
          class CraftSocket extends EventTarget {
            constructor(url) {
              super(); if (url !== 'ws://127.0.0.1:0') throw new Error('Unsupported Woodland socket');
              this.readyState=0;this.bufferedAmount=0;sockets.add(this);
              setTimeout(()=>{this.readyState=1;this.onopen?.(new Event('open'));},0);
            }
            send(raw) {
              const data=JSON.parse(raw);
              if(data.event==='registerPlugin') setTimeout(()=>this.receive(window.__craftAppear),0);
              else if(data.event==='setImage' && data.context==='woodland-instance') post({event:'frame',image:data.payload.image});
            }
            receive(data) { this.onmessage?.(new MessageEvent('message',{data:JSON.stringify(data)})); }
            close() {this.readyState=3;sockets.delete(this);}
          }
          window.WebSocket=CraftSocket;
          window.addEventListener('error',e=>post({event:'error',message:e.message}));
          window.addEventListener('unhandledrejection',e=>post({event:'error',message:String(e.reason)}));
          const NativeDate=Date;
          window.__craftUseTestDate=()=>{
            window.Date=class extends NativeDate {
              constructor(...args){super(...(args.length?args:[window.__craftTestTime]));}
              static now(){return window.__craftTestTime;}
            };
          };
        })();
        """;
}
