using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    WebView2? themeEditorBrowser;

    void RenderEditor()
    {
        if (Environment.GetEnvironmentVariable("MIRABOX_LEGACY_EDITOR_CHECKS") == "1") { RenderLegacyEditor(); return; }
        if (draft == null) return;
        themeEditorBrowser?.Dispose();
        Body.Children.Clear();
        var browser = new WebView2 { Height = Math.Max(700, ActualHeight - 175) };
        var messages = new SemaphoreSlim(1, 1);
        themeEditorBrowser = browser;
        Body.Children.Add(browser);
        browser.Loaded += async (_, _) =>
        {
            if (browser.CoreWebView2 != null) return;
            try
            {
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataRoot, "EditorBrowser"));
                await browser.EnsureCoreWebView2Async(environment);
                var core = browser.CoreWebView2 ?? throw new InvalidOperationException("WebView2 未初始化。");
                core.SetVirtualHostNameToFolderMapping("workspace.local", DataRoot, CoreWebView2HostResourceAccessKind.Allow);
                core.NavigationStarting += (_, navigation) =>
                {
                    if (!navigation.Uri.StartsWith("https://editor.local/", StringComparison.OrdinalIgnoreCase)) navigation.Cancel = true;
                };
                browser.CoreWebView2.SetVirtualHostNameToFolderMapping("editor.local", Path.Combine(AppContext.BaseDirectory, "Editor"), CoreWebView2HostResourceAccessKind.Allow);
                browser.CoreWebView2.WebMessageReceived += async (_, args) =>
                {
                    await messages.WaitAsync();
                    try { await HandleEditorMessage(browser, args.WebMessageAsJson); }
                    catch (Exception ex) { Notice.Text = "编辑器操作失败：" + ex.Message; }
                    finally { messages.Release(); }
                };
                browser.CoreWebView2.NavigationCompleted += async (_, args) =>
                {
                    if (!args.IsSuccess) { Notice.Text = "编辑器加载失败：" + args.WebErrorStatus; return; }
                    await browser.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Editor", "bridge.js")));
                    await browser.ExecuteScriptAsync("hostInitialize(" + JsonSerializer.Serialize(EditorPayload()) + ")");
                    await UpdateEditorVideoVisibility();
                };
                browser.Source = new Uri("https://editor.local/editor.html");
            }
            catch (Exception ex) { Notice.Text = "无法启动主题编辑器：" + ex.Message; }
        };
    }

    async Task UpdateEditorVideoVisibility()
    {
        if (themeEditorBrowser?.CoreWebView2 == null) return;
        var hidden = !IsVisible || WindowState == WindowState.Minimized;
        try
        {
            await themeEditorBrowser.ExecuteScriptAsync(hidden ?
                "document.querySelectorAll('video').forEach(v=>{if(!v.srcObject&&!v.paused){v.dataset.hostPausedByApp='1';v.pause();}});" :
                "document.querySelectorAll('video[data-host-paused-by-app]').forEach(v=>{delete v.dataset.hostPausedByApp;v.play().catch(()=>{});});");
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("Editor visibility update: {0}", ex.Message); }
    }

    string? EditorMediaUrl(string? path) => path == null ? null : "https://workspace.local/" +
        string.Join("/", Path.GetRelativePath(DataRoot, path).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));

    object EditorPayload()
    {
        var project = draft!;
        var fresh = !draftTemplateShown && library.Entries.All(entry => entry.Resource.Id != project.ResourceId);
        draftTemplateShown = true;
        var items = new List<object>
        {
            new { id = 1, name = draftName, type = draftType, local = true, created = true,
                sizes = project.Layouts.Select(layout => new[] { layout.Size.Width, layout.Size.Height }),
                style = project.Layouts[0].Style, text = project.Layouts[0].Text, color = project.Layouts[0].Color,
                url = EditorMediaUrl(project.BackgroundFile), themeData = project.EditorData ?? LegacyEditorDocument(project) }
        };
        var index = 2;
        foreach (var entry in library.Entries.Where(entry => entry.Resource.Local && entry.Type is "壁纸" or "视频"))
            items.Add(new { id = index++, name = entry.Resource.Name, type = entry.Type, local = true,
                sizes = entry.Resource.Layouts.Select(size => new[] { size.Width, size.Height }),
                url = EditorMediaUrl(entry.Type == "视频" ? library.ThumbnailPath(entry) : library.MediaPath(entry)),
                videoUrl = entry.Type == "视频" ? EditorMediaUrl(library.MediaPath(entry)) : null,
                cap = entry.CaptureKind,
                dur = entry.Duration?.TotalSeconds ?? 0, added = entry.Added.ToUnixTimeMilliseconds() });
        JsonElement? recovery = File.Exists(EditorDraftFile(project.ResourceId)) ?
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(EditorDraftFile(project.ResourceId))) : null;
        return new { projectId = project.ResourceId, fresh, draftRecovery = recovery, resources = items, devices = KnownHardwareSerials().Select(serial =>
            InHardwareSession(serial, () => new { id = serial, name = HardwareDeviceName(serial), model = HardwareModel,
                w = HardwareCanvas.Width, h = HardwareCanvas.Height, rot = 0, online = HardwarePresent(serial), running = hardwareOutput != null })) };
    }

    async Task HandleEditorMessage(WebView2 browser, string json)
    {
        if (!ReferenceEquals(themeEditorBrowser, browser) || draft == null) return;
        using var message = JsonDocument.Parse(json);
        var root = message.RootElement;
        var action = root.GetProperty("action").GetString();
        if (action is "captureOptions" or "captureEnd")
        {
            var excluded = action == "captureOptions" && !root.GetProperty("includeApp").GetBoolean();
            if (!SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, excluded ? 0x11u : 0u))
                Notice.Text = "无法更新采集排除设置，请检查录制结果。";
            return;
        }
        if (action == "dropDraft") { File.Delete(EditorDraftFile(draft.ResourceId)); return; }
        ImportEditorCaptures(root);
        var resource = root.GetProperty("resource");
        var layouts = resource.GetProperty("sizes").EnumerateArray().Select(size =>
            new ThemeLayout(new(size[0].GetInt32(), size[1].GetInt32()))).ToList();
        draft = draft with { Layouts = layouts, EditorData = RetainThemeAssets(resource.GetProperty("themeData"), draft.ResourceId) };
        draftName = root.GetProperty("name").GetString() ?? "";
        draftType = resource.GetProperty("type").GetString() ?? "主题";
        draftDirty = root.GetProperty("dirty").GetBoolean();
        if (action == "change")
        {
            if (draftDirty)
            {
                var path = EditorDraftFile(draft.ResourceId);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { n = draftName, t = draftType,
                    d = draft.EditorData, s = layouts.Select(layout => new[] { layout.Size.Width, layout.Size.Height }),
                    at = state.LocalTime.ToUnixTimeMilliseconds() }));
                File.Move(path + ".tmp", path, true);
            }
            return;
        }
        if (action == "back") { Navigate("创作中心"); return; }
        if (action == "preview") { await CaptureThemeBackgrounds(browser); PreviewEditorDraft(); return; }
        if (string.IsNullOrWhiteSpace(draftName)) throw new InvalidOperationException("请输入主题名称。");
        await CaptureThemeBackgrounds(browser);
        SaveDraft();
        File.Delete(EditorDraftFile(draft.ResourceId));
        await browser.ExecuteScriptAsync("hostSaved(" + JsonSerializer.Serialize(draft.EditorData) + "," +
            resource.GetRawText() + "," + JsonSerializer.Serialize(draftName) + "," + root.GetProperty("original").GetRawText() + ")");
        Notice.Text = "主题已保存。";
        if (action == "use") ChooseThemeOutput(state.Resources[draft.ResourceId]);
    }

    string EditorDraftFile(string id) => Path.Combine(DataRoot, "EditorDrafts", WorkspaceIdentity.Hash(id) + ".json");

    JsonElement LegacyEditorDocument(ThemeProject project)
    {
        object[] Elements(ThemeLayout layout, int index)
        {
            var clock = layout.FlipClock != null || System.Text.RegularExpressions.Regex.IsMatch(layout.Text, @"^\d{1,2}:\d{2}$");
            return [new { id = "legacy-" + index, kind = clock ? "clock" : "text", a = "mc", x = 0, y = 0,
                size = layout.Size.Width < layout.Size.Height ? 30 : 40, color = layout.Color, alpha = 1, weight = 600,
                text = layout.Text, style = layout.FlipClock == null ? "digital" : "flip",
                h24 = layout.FlipClock?.TwelveHour != true, sec = false, tz = "" }];
        }
        return JsonSerializer.SerializeToElement(new
        {
            bg = new { kind = "style", style = project.Layouts[0].Style, url = EditorMediaUrl(project.BackgroundFile), dim = 0, blur = 0, fit = "cover" },
            master = Elements(project.Layouts[0], 0),
            layouts = project.Layouts.Select((layout, index) => new { follow = false, elements = Elements(layout, index) }),
            locks = new Dictionary<string, bool>()
        });
    }

    void PreviewEditorDraft()
    {
        if (draft?.EditorData is not { } data) return;
        var theme = new Resource(draft.ResourceId, draftName, draft.Layouts.Select(layout => layout.Size).ToArray());
        var choices = KnownHardwareSerials().Where(serial => HardwarePresent(serial) &&
            InHardwareSession(serial, () => Workspace.BestLayout(theme, HardwareCanvas) is { Loss: <= .05 })).ToArray();
        if (choices.Length == 0) { Notice.Text = "没有适配的在线设备，请先添加对应布局。"; return; }
        if (choices.Length == 1) { Send(choices[0]); return; }
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("在哪台设备上预览？", panel, 600, 400);
        foreach (var serial in choices) panel.Children.Add(HomeButton(HardwareDeviceName(serial) + " · " + serial,
            () => { dialog.Close(); Send(serial); }, "PillButton"));
        dialog.ShowDialog();

        void Send(string serial)
        {
            var sent = InHardwareSession(serial, () =>
            {
                var previous = CaptureEditorPreviewReturn();
                if (hardwareOutput == null)
                    StartHardwareOutput(serial, hardwareApplyRotation,
                        D92StaticImage.Encode(hardwareApplyRotation, (_, _) => { }, HardwareModel), "空白画面", recordUse: false);
                var match = Workspace.BestLayout(theme, HardwareCanvas)!.Value;
                var jpeg = D92StaticImage.Encode(hardwareApplyRotation, (context, size) =>
                {
                    var view = EnhancedThemeArtwork(data, match.Index, HardwareCanvas, state.LocalTime);
                    view.Measure(size); view.Arrange(new Rect(size));
                    context.DrawRectangle(new System.Windows.Media.VisualBrush(view), null, new Rect(size));
                }, HardwareModel);
                hardwareOutput!.SubmitStill(jpeg, draftName, "preview");
                hardwareVideoResourceId = null;
                editorPreviewReturns[serial] = previous;
                hardwarePreviewEnds = HardwareNow + TimeSpan.FromSeconds(15);
                hardwareContent = "预览「" + draftName + "」";
                return true;
            });
            Notice.Text = sent ? "已在「" + HardwareDeviceName(serial) + "」预览 15 秒，结束后恢复原内容。" :
                "请先在「" + HardwareDeviceName(serial) + "」应用底层内容，视频播放时请先恢复画面，再开始临时预览。";
        }
    }
}
