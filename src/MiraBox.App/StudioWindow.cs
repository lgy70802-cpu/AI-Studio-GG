using MiraBox.Core;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Display.Usb;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace MiraBox.App;

public sealed partial class StudioWindow : Window
{
    internal sealed record SavedDevice(string Serial, string Model, bool Portrait = false, bool Flipped = false,
        bool Playing = false, string? ThemeId = null, string? ImageFile = null, string? ThemeName = null, string? VideoFile = null, string? ThemeFile = null,
        int Brightness = 100, int StandbyMinutes = 0, bool ManualStandby = false);
    readonly object logGate = new();
    readonly WebView2 browser = new();
    readonly Dictionary<string, SavedDevice> saved = new();
    readonly Dictionary<string, D92StaticOutput> outputs = new();
    readonly Dictionary<string, Task> runners = new();
    readonly Dictionary<string, string> errors = new();
    readonly HashSet<string> written = new();
    HashSet<string> connectedSerials = new();
    readonly SemaphoreSlim commands = new(1);
    readonly DispatcherTimer reconnect = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly string dataRoot;
    readonly StudioLibraryStore library;
    string? lastSavedState;
    string? selected;
    bool restore = true;
    bool librarySaveFailed;
    bool closing;
    bool closed;
    readonly CancellationTokenSource shutdown = new();
    long lastMainMessage = System.Diagnostics.Stopwatch.GetTimestamp();
    readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };
    public StudioWindow()
    {
        dataRoot = Environment.GetEnvironmentVariable("MIRABOX_DATA_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiraBoxStudio.Next");
        Directory.CreateDirectory(dataRoot);
        StudioPackageStore.RecoverRestore(dataRoot);
        StudioAtomicFile.Recover(Path.Combine(dataRoot, "desktop-state.json"), n =>
        {
            var state = JsonSerializer.Deserialize<Settings>(n!.ToJsonString());
            return state?.Devices != null && state.Devices.All(d => d != null && !string.IsNullOrWhiteSpace(d.Serial) && d.Model is "D69" or "D92") && state.Devices.Select(d => d.Serial).Distinct().Count() == state.Devices.Length;
        });
        StudioAtomicFile.Recover(Path.Combine(dataRoot, "schedule-state.json"), n => n?["History"] is System.Text.Json.Nodes.JsonArray && n?["Completed"] is System.Text.Json.Nodes.JsonArray);
        library = new StudioLibraryStore(dataRoot);
        Load();
        LoadWindowPreferences();
        LoadSchedules();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPower;
        Title = "小屏宝PixBuddy";
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/MiraBox.App;component/Assets/PixBuddy.ico"));
        Width = 1440; Height = 940; MinWidth = 720; MinHeight = 640;
        InitializeWindowFrame();
        Loaded += async (_, _) =>
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions("--disable-background-timer-throttling --disable-renderer-backgrounding --disable-features=CalculateNativeWinOcclusion");
                if (int.TryParse(Environment.GetEnvironmentVariable("MIRABOX_DEVTOOLS_PORT"), out var port) && port is >= 1024 and <= 65535)
                    options.AdditionalBrowserArguments += " --remote-debugging-port=" + port;
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataRoot, "WebView2"), options);
                webEnvironment = environment;
                await browser.EnsureCoreWebView2Async(environment);
                browser.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;
                browser.CoreWebView2.ProcessFailed += (_, failure) =>
                {
                    Log(new { at = DateTimeOffset.Now, subsystem = "main-webview", kind = failure.ProcessFailedKind.ToString() });
                    if (failure.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited && !closing)
                        try { browser.Reload(); } catch (Exception ex) { Log(new { error = ex.Message }); }
                };
                browser.CoreWebView2.SetVirtualHostNameToFolderMapping("studio.local", Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
                browser.CoreWebView2.SetVirtualHostNameToFolderMapping("media.studio.local", library.MediaRoot, CoreWebView2HostResourceAccessKind.Allow);
                browser.CoreWebView2.NavigationStarting += (_, navigation) =>
                {
                    if (!Uri.TryCreate(navigation.Uri, UriKind.Absolute, out var uri) || uri.Host != "studio.local")
                        navigation.Cancel = true;
                };
                browser.CoreWebView2.WebMessageReceived += Receive;
                browser.Source = new Uri("https://studio.local/index.html");
                reconnect.Tick += async (_, _) => await RestorePending();
                reconnect.Start();
                playlistTimer.Tick += (_, _) => TickPlaylists();
                playlistTimer.Start();
                scheduleTimer.Tick += async (_, _) => await TickSchedules();
                scheduleTimer.Start();
                themeTimer.Tick += (_, _) => TickThemes();
                themeTimer.Start();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "启动失败"); }
        };
        Closing += async (_, e) =>
        {
            if (closed) return;
            if (closeToTray && !exitRequested) { e.Cancel = true; HideToTray(); return; }
            e.Cancel = true;
            if (closing) return;

            if (librarySaveFailed && MessageBox.Show("资源库修改尚未保存。仍然退出吗？", "保存未完成",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            closing = true;
            shutdown.Cancel();
            pluginPreviewWindow?.Close();
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPower;
            reconnect.Stop();
            playlistTimer.Stop();
            scheduleTimer.Stop();
            themeTimer.Stop();
            await commands.WaitAsync();
            try
            {
                foreach (var serial in outputs.Keys.ToArray()) await Stop(serial);
                Save();
                SaveSchedules();
                foreach (var upload in videoUploads.Values) { upload.Stream.Dispose(); if (File.Exists(upload.Path)) File.Delete(upload.Path); }
                videoUploads.Clear();
                if (inspectedPackage != null) { Directory.Delete(inspectedPackage.Directory, true); inspectedPackage = null; }
                trayIcon?.Dispose();
                browser.Dispose();
                closed = true;
            }
            finally { commands.Release(); }
            Close();
        };
    }
    internal sealed record Settings(string? Selected, bool Restore, SavedDevice[] Devices, StudioPlaylistProgress[]? PlaylistSessions = null, SavedDevice[]? OrientationOutputs = null, OrientationUse[]? OrientationHistory = null);
    void Load()
    {
        var path = Path.Combine(dataRoot, "desktop-state.json");
        if (!File.Exists(path)) return;
        var state = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), json)
            ?? throw new InvalidDataException("新版状态文件为空。");
        selected = state.Selected;
        restore = state.Restore;
        foreach (var progress in state.PlaylistSessions ?? [])
        {
            if (!restore) progress.Running = false;
            playlistProgress[progress.Serial] = progress;
        }
        orientationOutputs.Clear();
        orientationHistory.Clear();
        foreach (var output in state.OrientationOutputs ?? []) orientationOutputs[(output.Serial, output.Portrait)] = output;
        orientationHistory.AddRange(state.OrientationHistory ?? []);
        foreach (var device in state.Devices)
            saved.Add(device.Serial, restore ? device : device with { Playing = false });
        RememberOrientationOutputs();
    }
    void Save()
    {
        var path = Path.Combine(dataRoot, "desktop-state.json");
        RememberOrientationOutputs();
        var content = JsonSerializer.Serialize(new Settings(selected, restore, saved.Values.Select(d => temporaryThemes.TryGetValue(d.Serial, out var preview) ? preview.Previous : d).ToArray(), playlistProgress.Values.ToArray(), orientationOutputs.Values.ToArray(), orientationHistory.ToArray()));
        if (content == lastSavedState) return;
        StudioAtomicFile.Write(path, content);
        lastSavedState = content;
    }
    string ImagePath(SavedDevice device)
    {
        var path = Path.GetFullPath(Path.Combine(dataRoot, device.ImageFile ?? throw new InvalidOperationException("请先选择壁纸。")));
        if (!path.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("素材路径超出新版数据目录。");
        return path;
    }
    void Log(object value)
    {
        try
        {
            lock (logGate) File.AppendAllText(Path.Combine(dataRoot, "desktop-output.jsonl"),
                JsonSerializer.Serialize(value) + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    async Task Stop(string serial)
    {
        var gate = OutputGate(serial);
        await gate.WaitAsync();
        try { await StopCore(serial); }
        finally { gate.Release(); }
    }
    async Task Start(SavedDevice device)
    {
        var gate = OutputGate(device.Serial);
        await gate.WaitAsync();
        try { await StartCore(device); }
        finally { gate.Release(); }
    }
    async Task StopCore(string serial)
    {
        if (pluginOutputs.Remove(serial, out var plugin))
        {
            plugin.Dispose();
            if (saved.TryGetValue(serial, out var device) && themePreviews.Remove(serial, out var snapshot))
                try { File.WriteAllBytes(ImagePath(device), Convert.FromBase64String(snapshot.Split(',', 2)[1])); }
                catch (IOException ex) { Log(new { serial, error = ex.Message }); }
        }
        themeErrors.Remove(serial);
        if (themeSessions.Remove(serial, out var themeSession))
        {
            try { if (themePreviews.TryGetValue(serial, out var preview)) File.WriteAllBytes(ImagePath(themeSession.Device), Convert.FromBase64String(preview[(preview.IndexOf(',') + 1)..])); }
            catch (IOException ex) { Log(new { at = DateTimeOffset.Now, serial, error = ex.Message }); }
            themeSession.Renderer.Dispose();
        }
        written.Remove(serial);
        if (outputs.Remove(serial, out var output))
        {
            output.Stop();
            try { if (runners.Remove(serial, out var runner)) await runner; }
            finally { output.Dispose(); }
        }
    }
    async Task StartCore(SavedDevice device)
    {
        var rotation = (device.Portrait ? 0 : 90) + (device.Flipped ? 180 : 0);
        byte[] jpeg;
        StudioThemeRenderer? themeRenderer = null;
        string? themePreview = null;
        WoodlandRuntime? pluginRuntime = null;
        if (device.ThemeId?.StartsWith(PluginPrefix, StringComparison.Ordinal) == true)
        {
            pluginRuntime = OpenPlugin(pluginHost, FindPlugin(device.ThemeId[PluginPrefix.Length..]), device);
            try
            {
                await pluginRuntime.Ready.WaitAsync(TimeSpan.FromSeconds(20));
                var frame = FitPlugin(pluginRuntime.Frame!, device);
                jpeg = D92StaticImage.Encode(rotation, frame, device.Model);
                File.WriteAllBytes(ImagePath(device), Convert.FromBase64String(EncodePreview(frame).Split(',', 2)[1]));
            }
            catch { pluginRuntime.Dispose(); throw; }
        }
        else if (device.ThemeFile != null)
        {
            var rendered = await OpenTheme(device);
            themeRenderer = rendered.Renderer;
            jpeg = rendered.Jpeg;
            themePreview = EncodePreview(rendered.Renderer.DecodePreview(jpeg));
        }
        else
        {
            var bitmap = new BitmapImage();
            using var stream = File.OpenRead(ImagePath(device));
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            jpeg = D92StaticImage.Encode(rotation, bitmap, device.Model);
        }
        try { await StopCore(device.Serial); }
        catch { pluginRuntime?.Dispose(); throw; }
        themePreviews.Remove(device.Serial);
        var output = new D92StaticOutput(device.Serial, rotation, jpeg, false, device.Model, device.Model == "D69");
        output.DisplayBrightness = device.Brightness; output.DisplayStandby = device.ManualStandby;
        if (themeRenderer != null)
        {
            var scene = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dataRoot, device.ThemeFile!)))!;
            var fps = ThemeHasMotion(scene["editorProject"]) ? (device.Model == "D69" ? 8 : D92StaticOutput.MaxLiveFps) : 1;
            output.LiveFramesPerSecond = fps;
            output.SubmitStill(jpeg, device.ThemeName, "clock");
            themeSessions[device.Serial] = new(themeRenderer, output, device, fps);
            themePreviews[device.Serial] = themePreview!;
            File.WriteAllBytes(ImagePath(device), Convert.FromBase64String(themePreview![(themePreview!.IndexOf(',') + 1)..]));
        }
        if (pluginRuntime != null)
        {
            pluginOutputs[device.Serial] = pluginRuntime;
            output.LiveFramesPerSecond = 8;
            output.SubmitStill(jpeg, device.ThemeName, "clock");
            pluginRuntime.FrameChanged += () =>
            {
                try
                {
                    if (pluginRuntime.Error is { } error) { themeErrors[device.Serial] = error; return; }
                    var frame = FitPlugin(pluginRuntime.Frame!, device);
                    themeErrors.Remove(device.Serial);
                    output.SubmitStill(D92StaticImage.Encode(rotation, frame, device.Model), device.ThemeName, "clock");
                    themePreviews[device.Serial] = EncodePreview(frame);
                }
                catch (Exception ex) { themeErrors[device.Serial] = ex.Message; }
            };
        }
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.Diagnostic += diagnostic =>
        {
            if (diagnostic.Event == "usb-first-write")
            {
                Log(diagnostic);
                firstWrite.TrySetResult();
            }
        };
        if (device.VideoFile != null)
        {
            output.VideoPerformance += new D92Diagnostics(dataRoot).RecordVideoPerformance;
            var profile = device.Model == "D69"
                ? new DeskDisplay.Core.Models.UsbVideoPreferences(20, true, 80)
                : D92StaticOutput.SmoothVideoProfile;
            output.SubmitVideo(ManagedVideoPath(device), device.ThemeName, profile: profile);
        }
        outputs[device.Serial] = output;
        runners[device.Serial] = output.RunAsync(state =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!outputs.TryGetValue(device.Serial, out var current) || current != output) return;
                if (state.Error is { } error) { written.Remove(device.Serial); errors[device.Serial] = error; }
                else if (state.Frames > 0) { written.Add(device.Serial); errors.Remove(device.Serial); }
            });
            if (state.Error is { } error) firstWrite.TrySetException(new IOException(error));
        });
        try
        {
            await firstWrite.Task.WaitAsync(TimeSpan.FromSeconds(12));
            written.Add(device.Serial);
            errors.Remove(device.Serial);
            retryOutput.Remove(device.Serial);
        }
        catch { await StopCore(device.Serial); throw; }
    }
    async Task RestorePending()
    {
        if (closing || systemSuspended || !await commands.WaitAsync(0)) return;
        try
        {
            if (System.Diagnostics.Stopwatch.GetElapsedTime(lastMainMessage).TotalSeconds > 20)
            {
                lastMainMessage = System.Diagnostics.Stopwatch.GetTimestamp();
                try { browser.Reload(); Log(new { at = DateTimeOffset.Now, action = "main-ui-heartbeat-reload" }); } catch (Exception ex) { Log(new { error = ex.Message }); }
            }
            foreach (var serial in temporaryThemes.Where(p => p.Value.Expires <= DateTimeOffset.Now).Select(p => p.Key).ToArray())
                try { await EndThemePreview(serial, true); } catch (Exception ex) { errors[serial] = ex.Message; }
            var connected = await Task.Run(() => D92HidDevice.EnumerateSupported().Select(d => d.SerialNumber).ToHashSet());
            connectedSerials = connected;
            await RefreshDisplayPower();
            foreach (var device in saved.Values.Where(d => d.Playing && d.ImageFile != null).ToArray())
            {
                if (playlistWorking.Contains(device.Serial)) continue;
                if (retryOutput.TryGetValue(device.Serial, out var retry) && retry.Next > DateTimeOffset.Now) continue;
                if (!connected.Contains(device.Serial)) { written.Remove(device.Serial); if (!outputs.TryGetValue(device.Serial, out var offline) || offline.VideoPath == null) await Stop(device.Serial); continue; }
                if (themeSessions.TryGetValue(device.Serial, out var session) && session.Failures >= 3) await Stop(device.Serial);
                if (outputs.ContainsKey(device.Serial)) continue;
                if (playlistProgress.TryGetValue(device.Serial, out var playback) && playback.Running &&
                    FindPlaylist(playback.PlaylistId) is { } list && !Available(list).Contains(playback.ThemeId ?? "") && Available(list).Count > 0)
                    continue;
                try { await Start(device); }
                catch (Exception ex) { errors[device.Serial] = ex.Message; FailedRestore(device.Serial); }
            }
        }
        catch (Exception ex) { Log(new { at = DateTimeOffset.Now, error = ex.Message }); }
        finally { commands.Release(); }
    }
    async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var origin) || origin.Host != "studio.local") return;
        using (var control = JsonDocument.Parse(args.WebMessageAsJson))
        {
            if (control.RootElement.TryGetProperty("command", out var capture) && capture.GetString() is "capture-shot" or "capture-record")
            {
                var captureId = control.RootElement.GetProperty("id").GetString();
                try { Reply(new { id = captureId, result = await CaptureDesktop(control.RootElement) }); }
                catch (Exception ex) { Reply(new { id = captureId, error = ex.Message }); }
                return;
            }
            if (control.RootElement.TryGetProperty("command", out var update) && update.GetString()?.StartsWith("updates-", StringComparison.Ordinal) == true)
            {
                var updateId = control.RootElement.GetProperty("id").GetString();
                try { Reply(new { id = updateId, result = await UpdateCommand(update.GetString()!, control.RootElement) }); }
                catch (Exception ex) { Reply(new { id = updateId, error = ex is OperationCanceledException ? "更新操作已取消或超时，请重试。" : ex.Message }); }
                return;
            }
            if (control.RootElement.TryGetProperty("command", out var video) &&
                video.GetString() is "video-begin" or "video-chunk" or "video-finish" or "video-cancel")
            {
                var videoId = control.RootElement.GetProperty("id").GetString();
                try { Reply(new { id = videoId, result = await ImportVideo(video.GetString()!, control.RootElement) }); }
                catch (Exception ex) { Reply(new { id = videoId, error = ex.Message }); }
                return;
            }
            if (control.RootElement.TryGetProperty("command", out var diagnostic) && diagnostic.GetString() == "debug-window-state" &&
                Environment.GetEnvironmentVariable("MIRABOX_DEVTOOLS_PORT") != null)
            {
                Reply(new { id = control.RootElement.GetProperty("id").GetString(), result = WindowDiagnostics() });
                return;
            }
            if (control.RootElement.TryGetProperty("command", out var kind) && kind.GetString() == "window-control")
            {
                switch (control.RootElement.GetProperty("action").GetString())
                {
                    case "minimize": WindowState = WindowState.Minimized; break;
                    case "maximize": WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
                    case "close": Close(); break;
                }
                return;
            }
        }
        lastMainMessage = System.Diagnostics.Stopwatch.GetTimestamp();
        string? id = null;
        await commands.WaitAsync();
        try
        {

            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var message = document.RootElement;
            id = message.GetProperty("id").GetString();
            var command = message.GetProperty("command").GetString();
            var detected = await Task.Run(() => D92HidDevice.EnumerateSupported().GroupBy(d => d.SerialNumber).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray());
            connectedSerials = detected.Select(d => d.SerialNumber).ToHashSet();
            foreach (var d in detected) saved.TryAdd(d.SerialNumber, new(d.SerialNumber, d.ModelKey));
            object result;
            if (command == "debug-system-power" && Environment.GetEnvironmentVariable("MIRABOX_DEVTOOLS_PORT") != null)
            {
                await HandleSystemPower(message.GetProperty("suspend").GetBoolean()); result = true;
            }
            else if (command == "device-power")
            {
                await PowerCommand(message.GetProperty("serial").GetString()!, message); result = true;
            }
            else if (command == "window-preferences")
            {
                closeToTray = message.GetProperty("enabled").GetBoolean();
                SaveWindowPreferences(); result = DesktopPreferences();
            }
            else if (command == "device-rename")
            {
                var serial = message.GetProperty("serial").GetString()!;
                if (!saved.ContainsKey(serial)) throw new InvalidOperationException("设备不存在");
                var name = message.GetProperty("name").GetString()?.Trim() ?? "";
                if (name.Length > 40) throw new InvalidOperationException("设备名称最多40个字符");
                deviceNames[serial] = name; SaveWindowPreferences(); result = true;
            }
            else if (command == "desktop-preferences") result = DesktopPreferences();
            else if (command == "desktop-autostart")
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (message.GetProperty("enabled").GetBoolean()) key.SetValue("MiraBoxStudio.Next", "\"" + Environment.ProcessPath + "\" --autostart");
                else key.DeleteValue("MiraBoxStudio.Next", false);
                result = DesktopPreferences();
            }
            else if (command == "cache-clean")
            {
                long bytes = 0;
                foreach (var file in Directory.GetFiles(library.MediaRoot, "*.upload*"))
                {
                    if (videoUploads.Values.Any(v => v.Path == file || file.StartsWith(v.Path + ".", StringComparison.OrdinalIgnoreCase))) continue;
                    if (processingVideos.Any(path => file.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase))) continue;
                    bytes += new FileInfo(file).Length; File.Delete(file);
                }
                result = new { bytes };
            }
            else if (command?.StartsWith("package-", StringComparison.Ordinal) == true)
            {
                result = await PackageCommand(command, message);
            }
            else if (command == "theme-preview-end")
            {
                await EndThemePreview(message.GetProperty("serial").GetString()!, true);
                result = true;
            }
            else if (command == "theme-preview")
            {
                var serial = message.GetProperty("serial").GetString()!;
                await EndThemePreview(serial, true);
                if (!saved.TryGetValue(serial, out var device) || !connectedSerials.Contains(serial)) throw new InvalidOperationException("目标设备已离线。");
                var theme = System.Text.Json.Nodes.JsonNode.Parse(message.GetProperty("theme").GetRawText())!;
                library.PrepareTheme(theme);
                var next = PrepareRenderedTheme(device, theme);
                InvalidatePlaylistWork(serial);
                await Start(next);
                temporaryThemes[serial] = (device, DateTimeOffset.Now.AddSeconds(15));
                saved[serial] = next;
                result = true;
            }
            else if (command == "apply-media")
            {
                var serial = message.GetProperty("serial").GetString()!;
                await EndThemePreview(serial, true);
                if (!saved.TryGetValue(serial, out var device) || !connectedSerials.Contains(serial))
                    throw new InvalidOperationException("目标设备已离线。");
                InvalidatePlaylistWork(serial);
                var next = PreparePlaylistImage(device, message.GetProperty("themeId").GetString()!, true);
                await Start(next);
                saved[serial] = next;
                playlistProgress.Remove(serial);
                Save();
                result = true;
            }
            else if (command == "refresh-current-media")
            {
                var serial = message.GetProperty("serial").GetString()!;
                var themeId = message.GetProperty("themeId").GetString()!;
                var portrait = message.GetProperty("portrait").GetBoolean();
                var sourceThemeId = message.TryGetProperty("sourceThemeId", out var source) ? source.GetString() : themeId;
                if (!saved.TryGetValue(serial, out var current) || current.ThemeId != sourceThemeId || current.Portrait != portrait)
                    result = false;
                else
                {
                    var next = PreparePlaylistImage(current, themeId, true) with { Playing = current.Playing };
                    InvalidatePlaylistWork(serial);
                    if (connectedSerials.Contains(serial))
                    {
                        await Start(next);
                        if (!next.Playing) await Stop(serial);
                    }
                    saved[serial] = next;
                    Save();
                    result = true;
                }
            }
            else if (command is "craft-preview" or "craft-settings" or "craft-configure" or "craft-apply" or "craft-cover" or "craft-remove")
                result = await CraftCommand(command, message, message.TryGetProperty("serial", out var craftSerial) &&
                    detected.Any(d => d.SerialNumber == craftSerial.GetString()));
            else if (command == "craft-list") result = new CraftPluginStore(Path.Combine(dataRoot, "Plugins")).Entries;
            else if (command == "craft-import")
            {
                var picker = new Microsoft.Win32.OpenFolderDialog { Title = "选择 Craft 或小屏宝插件文件夹", Multiselect = false };
                result = picker.ShowDialog(this) == true
                    ? await Task.Run(() => new CraftPluginStore(Path.Combine(dataRoot, "Plugins")).Import(picker.FolderName, DateTimeOffset.UtcNow))
                    : null;
            }
            else if (command == "library-load")
            {
                librarySnapshot = library.Load();
                result = librarySnapshot!;
            }
            else if (command == "library-save")
            {
                var snapshot = System.Text.Json.Nodes.JsonNode.Parse(message.GetProperty("snapshot").GetRawText())!.AsObject();
                try { library.Save(snapshot); librarySnapshot = snapshot; scheduleState.Reconcile(ScheduleDefinitions(), DateTime.Now); SaveSchedules(); librarySaveFailed = false; }
                catch { librarySaveFailed = true; throw; }
                foreach (var theme in snapshot["themes"]!.AsArray())
                {
                    var themeId = theme!["id"]!.GetValue<string>();
                    var title = theme["displayName"]!.GetValue<string>();
                    foreach (var device in saved.Values.Where(d => d.ThemeId == themeId).ToArray())
                        saved[device.Serial] = device with { ThemeName = title };
                }
                Save();
                result = snapshot["themes"]!;
            }
            else if (command == "devices")
            {
                result = new { selected, restore, orientationHistory, scheduling = ScheduleSnapshot(), devices = saved.Values.Select(d =>
                {
                    var progress = playlistProgress.GetValueOrDefault(d.Serial);
                    var binding = progress == null ? null : FindPlaylist(progress.PlaylistId);
                    var bound = binding?.Portrait == d.Portrait && binding?.DeviceId == d.Serial;
                    var online = detected.Any(x => x.SerialNumber == d.Serial);
                    var width = d.Model == "D69" ? 1280 : 1920;
                    var height = d.Model == "D69" ? 480 : 462;
                    var preview = PreviewData(d);
                    return new { id = d.Serial, serial = d.Serial, name = deviceNames.TryGetValue(d.Serial, out var alias) && !string.IsNullOrWhiteSpace(alias) ? alias : d.Model, model = d.Model,
                        status = online ? "已连接" : "离线", nativeWidth = width, nativeHeight = height,
                        resolution = d.Portrait ? height + " × " + width : width + " × " + height,
                        refreshRate = "—", brightness = d.Brightness, orientation = d.Portrait ? "portrait" : "landscape",
                        flipped = d.Flipped, sleepMinutes = d.StandbyMinutes, standby = standbySerials.Contains(d.Serial), hardwareBrightness = false, hardwarePower = false,
                        firmware = "—", themeId = d.ThemeId, themeName = d.ThemeName, preview,
                        frames = outputs.TryGetValue(d.Serial, out var active) ? active.Frames : 0, playing = online && d.Playing && written.Contains(d.Serial) && !standbySerials.Contains(d.Serial), wantsPlaying = d.Playing,
                        playlistId = bound ? progress?.PlaylistId : null,
                        playlistRunning = bound && progress?.Running == true && d.Playing,
                        playlistRemaining = bound ? (int?)Math.Ceiling(progress?.RemainingSeconds ?? 0) : null,
                        playlistMessage = progress?.Message,
                        videoPosition = outputs.GetValueOrDefault(d.Serial)?.VideoProgress?.Position.TotalSeconds,
                        videoDuration = outputs.GetValueOrDefault(d.Serial)?.VideoProgress?.Duration?.TotalSeconds,
                        videoFps = outputs.GetValueOrDefault(d.Serial)?.VideoRate?.ActualFps,
                        renderFps = themeSessions.GetValueOrDefault(d.Serial)?.ActualFps,
                        usbFps = themeSessions.GetValueOrDefault(d.Serial)?.UsbFps,
                        outputStatus = !online ? "设备离线" : standbySerials.Contains(d.Serial) ? "黑屏待机（背光保持）" : themeErrors.GetValueOrDefault(d.Serial) ?? errors.GetValueOrDefault(d.Serial) ?? progress?.Message ?? (written.Contains(d.Serial) ? "正在输出" : d.Playing ? "等待恢复输出" : "已暂停输出") };
                }).ToArray() };
            }
            else if (command == "schedule-enable")
            {
                scheduleState.Enabled = message.GetProperty("enabled").GetBoolean();
                SaveSchedules();
                result = true;
            }
            else if (command is "schedule-run" or "schedule-reset")
            {
                var rule = ScheduleDefinitions().SingleOrDefault(rule => rule.Id == message.GetProperty("ruleId").GetString())
                    ?? throw new InvalidOperationException("规则不存在，请刷新后重试。");
                if (command == "schedule-reset") { scheduleState.Reset(rule, DateTime.Now); SaveSchedules(); }
                else await ExecuteScheduled(rule, true);
                result = ScheduleSnapshot();
            }
            else if (command is "playlist-start" or "playlist-pause" or "playlist-step")
            {
                await PlaylistCommand(command, message, detected.Select(d => d.SerialNumber).ToHashSet());
                result = true;
            }
            else if (command == "select")
            {
                selected = message.GetProperty("serial").GetString();
                Save();
                result = true;
            }
            else if (command == "restore")
            {
                restore = message.GetProperty("enabled").GetBoolean();
                Save();
                result = restore;
            }
            else
            {
                var serial = message.GetProperty("serial").GetString()!;
                await EndThemePreview(serial, true);
                if (!saved.TryGetValue(serial, out var previous)) throw new InvalidOperationException("请先连接并选择一台 MiraBox。");
                InvalidatePlaylistWork(serial);
                if (command == "apply") playlistProgress.Remove(serial);
                if (command == "configure" && message.TryGetProperty("portrait", out var requestedPortrait) &&
                    requestedPortrait.GetBoolean() != previous.Portrait && playlistProgress.TryGetValue(serial, out var progress))
                { progress.Running = false; progress.Message = "显示方向已变更，轮播已暂停"; }
                var next = previous;
                if (command == "apply")
                {
                    if (!detected.Any(d => d.SerialNumber == serial)) throw new InvalidOperationException("目标设备已离线。");
                    var data = message.GetProperty("image").GetString()!;
                    var bytes = Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]);
                    if (bytes.Length > 50 * 1024 * 1024) throw new InvalidOperationException("图片超过 50 MiB。");
                    var name = Guid.NewGuid().ToString("N") + ".image";
                    File.WriteAllBytes(Path.Combine(dataRoot, name), bytes);
                    next = previous with { ImageFile = name, ThemeId = message.GetProperty("themeId").GetString(),
                        ThemeName = message.GetProperty("themeName").GetString(), VideoFile = null, ThemeFile = null, Portrait = message.GetProperty("portrait").GetBoolean(), Flipped = message.GetProperty("flipped").GetBoolean(), Playing = true };
                }
                else if (command == "configure")
                    next = previous with { Portrait = message.TryGetProperty("portrait", out var portrait) ? portrait.GetBoolean() : previous.Portrait,
                        Flipped = message.TryGetProperty("flipped", out var flipped) ? flipped.GetBoolean() : previous.Flipped,
                        Playing = message.TryGetProperty("playing", out var playing) ? playing.GetBoolean() : previous.Playing };
                else throw new InvalidOperationException("未知设备操作。");
                if (command == "configure" && next.Portrait != previous.Portrait &&
                    orientationOutputs.TryGetValue((serial, next.Portrait), out var remembered) &&
                    remembered.ThemeId != null)
                {
                    if (PlaylistImagePath(remembered.ThemeId) != null)
                        next = PreparePlaylistImage(next, remembered.ThemeId) with { Flipped = remembered.Flipped, Playing = next.Playing };
                    else if (remembered.ImageFile != null && File.Exists(ImagePath(remembered)))
                        next = next with { ThemeId = remembered.ThemeId, ThemeName = remembered.ThemeName,
                            ImageFile = remembered.ImageFile, ThemeFile = remembered.ThemeFile,
                            VideoFile = remembered.VideoFile, Flipped = remembered.Flipped };
                }
                if (command == "configure" && next.Portrait == previous.Portrait && next.Flipped == previous.Flipped && themeSessions.TryGetValue(serial, out var themeOutput))
                {
                    await themeOutput.Renderer.SetPaused(!next.Playing);
                    themeOutput.Paused = !next.Playing;
                    saved[serial] = next; Save(); Reply(new { id, result = true }); return;
                }
                if (command == "configure" && next.Portrait == previous.Portrait && next.Flipped == previous.Flipped &&
                    outputs.TryGetValue(serial, out var videoOutput) && videoOutput.VideoPath != null &&
                    videoOutput.SetVideoPaused(!next.Playing))
                {
                    saved[serial] = next;
                    Save();
                    Reply(new { id, result = true });
                    return;
                }
                if (!next.Playing)
                {
                    if (next.ImageFile != null && detected.Any(d => d.SerialNumber == serial) &&
                        (next.Portrait != previous.Portrait || next.Flipped != previous.Flipped))
                        await Start(next);
                    await Stop(serial);
                }
                else if (detected.Any(d => d.SerialNumber == serial))
                {
                    try { await Start(next); }
                    catch
                    {
                        if (previous.Playing && previous.ImageFile != null)
                            try { await Start(previous); } catch { }
                        throw;
                    }
                }
                saved[serial] = next;
                Save();
                result = true;
            }
            Reply(new { id, result });
        }
        catch (Exception ex) { Log(new { at = DateTimeOffset.Now, error = ex.Message }); Reply(new { id, error = ex.Message }); }
        finally { commands.Release(); }
    }
    void Reply(object value)
    {
        if (!closing) browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value));
    }
}
