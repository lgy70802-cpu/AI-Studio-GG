using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Wpf;

namespace MiraBox.App;

public partial class MainWindow
{
    async Task CaptureThemeBackgrounds(WebView2 browser)
    {
        if (draft?.EditorData is not { } data || JsText(data.GetProperty("bg"), "kind") == "video") return;
        var core = browser.CoreWebView2!;
        var document = JsonNode.Parse(data.GetRawText())!;
        var backgrounds = new JsonObject();
        try
        {
            for (var index = 0; index < draft.Layouts.Count; index++)
            {
                var size = draft.Layouts[index].Size;
                await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", JsonSerializer.Serialize(new
                { width = size.Width, height = size.Height, deviceScaleFactor = 1, mobile = false }));
                await browser.ExecuteScriptAsync("document.getElementById('host-background')?.remove();" +
                    "{const el=document.createElement('div');el.id='host-background';el.style.cssText='position:fixed;inset:0;z-index:999999;background:black;width:" +
                    size.Width + "px;height:" + size.Height + "px';el.innerHTML=hostBackgroundMarkup(" + index + ");document.body.appendChild(el);}");
                await Task.Delay(100);
                var result = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", JsonSerializer.Serialize(new
                { format = "png", captureBeyondViewport = true, clip = new { x = 0, y = 0, width = size.Width, height = size.Height, scale = 1 } }));
                using var screenshot = JsonDocument.Parse(result);
                var directory = Path.Combine(DataRoot, "Library", "Themes", draft.ResourceId);
                Directory.CreateDirectory(directory);
                var bytes = Convert.FromBase64String(screenshot.RootElement.GetProperty("data").GetString()!);
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                var path = Path.Combine(directory, $"background-{size.Width}x{size.Height}-{hash}.png");
                if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
                backgrounds[$"{size.Width}x{size.Height}"] = EditorMediaUrl(path);
            }
            document["hostBackgrounds"] = backgrounds;
            draft = draft with { EditorData = JsonSerializer.SerializeToElement(document) };
        }
        finally
        {
            await browser.ExecuteScriptAsync("document.getElementById('host-background')?.remove()");
            await core.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");
        }
    }
}
