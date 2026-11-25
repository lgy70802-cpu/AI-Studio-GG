using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Usb;
using Microsoft.Win32;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly D92OrientationPreferences hardwareOrientations = new(Path.Combine(DataRoot, "d92-orientations.json"));
    string? hardwareApplySerial;
    int hardwareApplyRotation = D92OrientationPreferences.DefaultRotation;
    readonly D92Diagnostics hardwareDiagnostics = new(DataRoot);
    internal Func<string, int, byte[], bool, D92StaticOutput> CreateHardwareOutput { get; set; } =
        static (serial, rotation, bytes, identification) => new(serial, rotation, bytes, identification);
    internal Func<string, bool> HardwarePresent { get; set; } =
        static serial => SupportedHardwareDevices().Any(device => device.SerialNumber == serial);
    internal Func<D92DeviceInfo[]> DiscoverHardware { get; set; } = SupportedHardwareDevices;
    Dictionary<string, string> hardwareDeviceModels = new(StringComparer.Ordinal);
    string HardwareModelFor(string serial) => hardwareDeviceModels.GetValueOrDefault(serial, "D92");
    D92StaticOutput CreateModelHardwareOutput(string serial, int rotation, byte[] bytes, bool identification) =>
        new(serial, rotation, bytes, identification, HardwareModelFor(serial), HardwareModelFor(serial) == "D69");
    string HardwareModel => hardwareApplySerial is { } serial ? HardwareModelFor(serial) : "D92";
    void RememberHardwareModels(IEnumerable<D92DeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            if (hardwareDeviceModels.GetValueOrDefault(device.SerialNumber) != device.ModelKey)
                hardwareOrientations.SaveModel(device.SerialNumber, device.ModelKey);
            hardwareDeviceModels[device.SerialNumber] = device.ModelKey;
        }
    }
    static D92DeviceInfo[] SupportedHardwareDevices() => D92HidDevice.EnumerateSupported()
        .Where(device => IsHardwareSerialCandidate(device.SerialNumber))
        .GroupBy(device => device.SerialNumber, StringComparer.Ordinal)
        .Where(group => group.Count() == 1).Select(group => group.Single()).ToArray();

    internal static bool IsHardwareSerialCandidate(string serial)
    {
        if (!string.IsNullOrWhiteSpace(serial) &&
            serial.All(character => character is >= ' ' and <= '~') &&
            !string.Equals(serial.Trim(), "HOTSPOTEKUSB HID DEMO", StringComparison.OrdinalIgnoreCase))
            return true;

        System.Diagnostics.Trace.TraceWarning("Ignored USB display interface with invalid or demo serial: {0}",
            System.Text.Json.JsonSerializer.Serialize(serial));
        return false;
    }
    string? hardwareLastWritten;
    D92StaticOutput? hardwareOutput;
    Task hardwareOutputCompletion = Task.CompletedTask;
    bool closingForHardware;
    string hardwareContent = "";
    string? lastHardwareContentSerial;
    string hardwareStatus = "";
    Guid? manualHardwareSubmission;
    bool manualHardwareWasOffline;

    bool UsesHardwareApply => hardwareApplySerial != null && page is "首页" or "资源库" or "创作中心";
    string HardwareTargetLabel => hardwareApplySerial is { } serial ? $"{HardwareDeviceName(serial)} · {serial}" : "D92 实机";

    void ShowHardwarePilot()
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("USB 屏幕测试", panel, 720, 570);
        var geometryLabel = Text("USB D92");
        panel.Children.Add(geometryLabel);
        panel.Children.Add(Text("图片成功输出后，此 D92 会成为首页和资源库的应用目标。关闭弹窗后可从底栏停止实机输出。", 13));
        var devices = new ComboBox { DisplayMemberPath = nameof(D92DeviceInfo.SerialNumber), Margin = new Thickness(4) };
        panel.Children.Add(devices);
        var angle = new ComboBox { ItemsSource = new[] { "0°", "90°", "180°", "270°" }, Margin = new Thickness(4) };
        panel.Children.Add(Text("发往屏幕的像素旋转"));
        panel.Children.Add(angle);

        var status = Text("正在查找 D92…");
        panel.Children.Add(status);
        var buttons = new WrapPanel();
        var rescan = new Button { Content = "重新查找" };
        var pick = new Button { Content = "选择图片" };
        var identify = new Button { Content = "发送识别画面" };
        var start = new Button { Content = "开始输出" };
        var stop = new Button { Content = "停止输出" };
        var use = new Button { Content = "设为首页应用目标" };
        var simulation = new Button { Content = "切回模拟应用" };
        simulation.Visibility = IsPlaylistDemo ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { rescan, pick, identify, start, stop, use, simulation }) buttons.Children.Add(button);
        panel.Children.Add(buttons);
        panel.Children.Add(Action("查看发送诊断", OpenHardwareDiagnostics));
        var preview = new Image { Height = 130, Margin = new Thickness(4) };
        var fileLabel = Text("请选择本地图片。");
        panel.Children.Add(fileLabel);
        panel.Children.Add(preview);

        D92StaticOutput? dialogOutput = null;
        BitmapSource? selectedImage = null;
        var scanning = false;
        var loadingAngle = false;
        var closingAfterStop = false;
        var dialogClosed = false;
        var refresh = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        refresh.Tick += (_, _) =>
        {
            if (hardwareOutput != null || dialogOutput != null)
                status.Text = HardwareStatusLine();
            RefreshButtons();
        };
        dialog.Loaded += async (_, _) => { refresh.Start(); await Scan(); };
        dialog.Closed += (_, _) => { dialogClosed = true; refresh.Stop(); };
        dialog.Closing += async (_, args) =>
        {
            if (dialogOutput is not { Identification: true } || !ReferenceEquals(dialogOutput, hardwareOutput)) return;
            args.Cancel = true;
            if (closingAfterStop) return;
            closingAfterStop = true;
            await StopHardwareTransport();
            await Dispatcher.InvokeAsync(dialog.Close);
        };

        rescan.Click += async (_, _) => await Scan();
        devices.SelectionChanged += (_, _) => LoadAngle();
        angle.SelectionChanged += (_, _) =>
        {
            if (loadingAngle || angle.SelectedIndex < 0 || devices.SelectedItem is not D92DeviceInfo device) return;
            try { SaveHardwareRotation(device.SerialNumber, angle.SelectedIndex * 90); }
            catch (Exception ex) { status.Text = "保存旋转失败：" + ex.Message; LoadAngle(); }
            RefreshButtons();
        };
        pick.Click += (_, _) =>
        {
            var picker = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (picker.ShowDialog(dialog) != true) return;
            try
            {
                selectedImage = Load(picker.FileName, D92StaticImage.TransportHeight);
                preview.Source = selectedImage;
                fileLabel.Text = Path.GetFileName(picker.FileName);
                RefreshButtons();
            }
            catch (Exception ex) { status.Text = "无法读取图片：" + ex.Message; }
        };
        identify.Click += (_, _) => Send(true);
        start.Click += (_, _) => Send(false);
        stop.Click += async (_, _) =>
        {
            await StopHardwareOutput();
            status.Text = HardwareStatusLine();
            RefreshButtons();
        };
        use.Click += (_, _) =>
        {
            if (devices.SelectedItem is not D92DeviceInfo device || angle.SelectedIndex < 0) return;
            try
            {
                SaveHardwareRotation(device.SerialNumber, angle.SelectedIndex * 90);
                SetHardwareApplyTarget(device);
                dialog.Close();
            }
            catch (Exception ex) { status.Text = "保存设备设置失败：" + ex.Message; }
        };
        simulation.Click += async (_, _) =>
        {
            try
            {
                await StopHardwareOutput();
                SetHardwareApplyTarget(null);
                dialog.Close();
            }
            catch (Exception ex) { status.Text = "切换应用目标失败：" + ex.Message; }
        };
        RefreshButtons();
        dialog.ShowDialog();
        Render();

        void LoadAngle()
        {
            loadingAngle = true;
            try
            {
                angle.SelectedIndex = devices.SelectedItem is D92DeviceInfo device
                    ? hardwareOrientations.Get(device.SerialNumber) / 90 : -1;
            }
            catch (Exception ex) { angle.SelectedIndex = -1; status.Text = "读取旋转失败：" + ex.Message; }
            finally { loadingAngle = false; RefreshButtons(); }
        }

        void RefreshButtons()
        {
            var idle = hardwareOutput == null && !scanning && !closingAfterStop;
            var ready = devices.SelectedItem is D92DeviceInfo && angle.SelectedIndex >= 0;
            if (ready)
            {
                var model = HardwareModelFor(((D92DeviceInfo)devices.SelectedItem!).SerialNumber);
                var geometry = D92StaticImage.Geometry(angle.SelectedIndex * 90, model);
                geometryLabel.Text = $"USB {model} · {(geometry.Width < geometry.Height ? "竖屏" : "横屏")}预览 " +
                    $"{geometry.Width} × {geometry.Height} · 传输 {(model == "D69" ? "480 × 1280" : "462 × 1920")}";
                preview.Width = geometry.Width < geometry.Height ? 130d * geometry.Width / geometry.Height : double.NaN;
            }
            else geometryLabel.Text = "USB 屏幕";
            rescan.IsEnabled = devices.IsEnabled = pick.IsEnabled = idle;
            angle.IsEnabled = idle && devices.SelectedItem is D92DeviceInfo;
            identify.IsEnabled = idle && ready;
            use.IsEnabled = ready && !scanning && (idle || hardwareOutput is { Identification: false } active &&
                active.Serial == (devices.SelectedItem as D92DeviceInfo)?.SerialNumber);
            start.IsEnabled = idle && ready && selectedImage != null;
            stop.IsEnabled = hardwareOutput != null || pendingHardwareRestore != null;
            simulation.IsEnabled = hardwareApplySerial != null && !scanning && !closingAfterStop;
        }

        async Task Scan()
        {
            if (scanning) return;
            var preferred = (devices.SelectedItem as D92DeviceInfo)?.SerialNumber ?? hardwareApplySerial;
            scanning = true;
            RefreshButtons();
            try
            {
                var candidates = await Task.Run(DiscoverHardware);
                if (dialogClosed) return;
                RememberHardwareModels(candidates);
                devices.ItemsSource = candidates;
                devices.SelectedItem = candidates.FirstOrDefault(device => device.SerialNumber == preferred) ??
                    (candidates.Length == 1 ? candidates[0] : null);
                if (devices.SelectedItem == null || angle.SelectedIndex >= 0)
                    status.Text = candidates.Length == 0 ? "未找到可按序列号识别的 USB D92。" : $"找到 {candidates.Length} 台 D92，请核对序列号。";
            }
            catch (Exception ex) { status.Text = "设备查找失败：" + ex.Message; }
            finally { scanning = false; RefreshButtons(); }
        }

        void Send(bool marker)
        {
            if (hardwareOutput != null || devices.SelectedItem is not D92DeviceInfo device || angle.SelectedIndex < 0 ||
                !marker && selectedImage == null) return;
            try
            {
                var rotation = angle.SelectedIndex * 90;
                SaveHardwareRotation(device.SerialNumber, rotation);
                var model = HardwareModelFor(device.SerialNumber);
                var jpeg = marker ? D92StaticImage.Encode(rotation, DrawHardwareMarker, model) : D92StaticImage.Encode(rotation, selectedImage!, model);
                dialogOutput = StartHardwareOutput(device.SerialNumber, rotation, jpeg, marker ? "识别画面" : fileLabel.Text, marker);
                status.Text = HardwareStatusLine();
                RefreshButtons();
            }
            catch (Exception ex) { status.Text = "启动输出失败：" + ex.Message; }
        }
    }

    static D92DeviceInfo[] UniqueHardwareDevices() => D92HidDevice.Enumerate()
        .GroupBy(device => device.SerialNumber, StringComparer.Ordinal)
        .Where(group => group.Count() == 1).Select(group => group.Single()).ToArray();

    static void DrawHardwareMarker(DrawingContext context, System.Windows.Size size)
    {
        context.DrawRectangle(Brushes.MidnightBlue, null, new Rect(size));
        var label = new FormattedText(size.Width * size.Height == 1280 * 480 ? "MiraBox D69" : "MiraBox D92", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size.Width < size.Height ? 60 : 130, Brushes.White, 1);
        context.DrawText(label, new Point((size.Width - label.Width) / 2, (size.Height - label.Height) / 2));
    }

    void LoadHardwareApplyTarget()
    {
        try
        {
            var serial = hardwareOrientations.GetApplyTarget();
            hardwareApplyRotation = serial == null ? D92OrientationPreferences.DefaultRotation : hardwareOrientations.Get(serial);
            hardwareApplySerial = serial;
            hardwareDeviceNames = hardwareOrientations.GetNames();
            hardwareDeviceModels = hardwareOrientations.GetModels();
            if (serial != null) ResetOutputBrowseRotation();
        }
        catch (Exception ex) { Notice.Text = "读取 D92 应用目标失败：" + ex.Message; }
        UpdateHardwareTargetLabel();
        ReadLastHardwareContent();
        for (var index = 0; index < state.ScheduleHistory.Count; index++)
        {
            var item = state.ScheduleHistory[index];
            if (item.TransferState is "已提交发送" or "等待同一设备重连")
                state.ScheduleHistory[index] = item with { TransferState = "上次会话已结束", Succeeded = false };
        }
    }

    void UpdateHardwareTargetLabel() =>
        HardwareTargetButton.Content = hardwareApplySerial == null ? IsPlaylistDemo ? "输出：模拟" : "选择设备" : "输出：实机";

    void SetHardwareApplyTarget(D92DeviceInfo? device)
    {
        if (device != null) RememberHardwareModels([device]);
        SetHardwareApplySerial(device?.SerialNumber);
    }

    void SetHardwareApplySerial(string? serial)
    {
        var rotation = serial == null ? D92OrientationPreferences.DefaultRotation : hardwareOrientations.Get(serial);
        hardwareOrientations.SaveApplyTarget(serial);
        UpdateHardwareApplySerial(serial, rotation);
    }

    void UpdateHardwareApplySerial(string? serial, int rotation)
    {
        SelectHardwareSession(serial);
        hardwareApplyRotation = rotation;
        ResetOutputBrowseRotation();
        UpdateHardwareTargetLabel();
        ReadLastHardwareContent();
        Notice.Text = serial == null ? "首页和资源库的应用目标已切回模拟设备。" :
            $"当前输出为 {HardwareTargetLabel}，可应用本地壁纸、匹配方向的主题和已导入的视频。";
        Render();
    }

    void SaveHardwareRotation(string serial, int rotation)
    {
        if (HardwareOutputFor(serial) is { } active && active.Rotation != rotation)
            throw new InvalidOperationException("请先停止当前 D92 输出，再修改方向。");
        hardwareOrientations.Save(serial, rotation);
        if (hardwareSessions.TryGetValue(serial, out var session)) { session.stoppedHardwareStill = null; session.hardwareStill = null; }
        if (hardwareApplySerial != serial || hardwareApplyRotation == rotation) return;
        hardwareApplyRotation = rotation;
        stoppedHardwareStill = null;
        hardwareStill = null;
        ResetOutputBrowseRotation();
    }

    void ResetOutputBrowseRotation()
    {
        browseRotation = DefaultBrowseRotation();
        foreach (var name in new[] { "资源库", "在线资源" })
            if (pageMemory.TryGetValue(name, out var memory))
                pageMemory[name] = memory with { Rotation = browseRotation };
    }

    void AdoptHardwareOutput(D92StaticOutput output)
    {
        if (output.Identification || output.Serial == hardwareApplySerial) return;
        try { SetHardwareApplySerial(output.Serial); }
        catch (Exception ex)
        {
            UpdateHardwareApplySerial(output.Serial, output.Rotation);
            Notice.Text = "当前已切到 D92 实机，但无法保存应用目标：" + ex.Message;
        }
    }

    void OpenHardwareTarget(object sender, RoutedEventArgs e)
    {
        if (IsPlaylistDemo) ShowHardwarePilot();
        else Navigate("设备管理");
    }

    void ApplyHardwareResource(Resource resource)
    {
        if (hardwareApplySerial == null)
        {
            Notice.Text = "请先选择 D92 实机。";
            return;
        }
        if (HardwareApplyIssue(resource) is { } issue)
        {
            Notice.Text = issue;
            return;
        }

        try
        {
            var offline = hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal) ||
                managedHardwareDetected != null && !managedHardwareDetected.Any(device => device.SerialNumber == hardwareApplySerial);
            UseHardwareContent(resource);
            manualHardwareSubmission = hardwareOutput?.RequestedSubmissionId;
            manualHardwareWasOffline = offline;
            if (offline) Notice.Text = $"{HardwareTargetLabel}：{HardwareOfflineSaved}";
            SaveWorkspace();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        { Notice.Text = ex.Message; }
    }

    D92StaticOutput StartHardwareOutput(string serial, int rotation, byte[] jpeg, string name,
        bool identification = false, string? resourceId = null, DateTimeOffset? clockTime = null,
        HardwareVideoRequest? video = null, bool recordUse = true)
    {
        if (hardwareApplySerial != serial)
            return InHardwareSession(serial, () => StartHardwareOutput(serial, rotation, jpeg, name,
                identification, resourceId, clockTime, video, recordUse));
        var still = new HardwareStill(jpeg, name, resourceId, clockTime);
        if (hardwareOutput is { } active)
        {
            if (active.IsStopping || active.Serial != serial || active.Rotation != rotation || active.Identification || identification)
                throw new InvalidOperationException("请先停止当前 D92 输出。");
            Submit(active);
            if (video != null) hardwareStatus = "正在准备视频";
            else if (hardwareStatus is "正在输出" or "正在准备视频") hardwareStatus = "已提交 · 等待 USB 写入";
            RenderHardwareStatus();
            return active;
        }

        var output = CreateHardwareOutput(serial, rotation, jpeg, identification);
        hardwareOutput = output;
        output.Diagnostic += OnHardwareDiagnostic;
        output.VideoPerformance += hardwareDiagnostics.RecordVideoPerformance;
        lastHardwareContentSerial = identification ? null : serial;
        Submit(output);
        hardwareStatus = video != null ? "正在准备视频" : "正在连接 USB";
        RenderHardwareStatus();
        hardwareOutputCompletion = CompleteHardwareOutput(output);
        return output;

        void Submit(D92StaticOutput targetOutput)
        {
            editorPreviewReturns.Remove(serial);
            editorRestoreVideoPaused.Remove(serial);
            targetOutput.TransformVideoFrame = CreateThemeVideoTransform(video?.Id, serial, rotation);
            targetOutput.VideoDecodeSize = ThemeVideoDecodeSize(video?.Id);
            targetOutput.CreateVideoComposition = CreateEnhancedVideoComposition(video?.Id) ?? CreateMediaFitComposition(video?.Id);
            if (targetOutput.CreateVideoComposition != null)
            {
                targetOutput.TransformVideoFrame = null;
            }
            if (!identification)
            {
                stoppedHardwareOutput = null;
                stoppedHardwareStill = null;
                pendingHardwareRestore = null;
                pendingHardwareScheduleStop = null;
                manualHardwareSubmission = null;
            }
            hardwareStill = identification ? null : still;
            hardwareVideoResourceId = video?.Id;
            hardwarePreviewEnds = null;
            hardwarePreviewResourceId = null;
            hardwareContent = video == null ? name : $"视频「{video.Name}」";
            if (video == null) targetOutput.SubmitStill(jpeg, name, identification ? "identification" : still.Mode,
                recordUse && !identification ? resourceId : null);
            else targetOutput.SubmitVideo(video.Path, video.Name, jpeg, name, video.Profile,
                recordUse ? video.Id : null);
        }
    }

    async Task CompleteHardwareOutput(D92StaticOutput output)
    {
        try
        {
            await output.RunAsync(result => _ = Dispatcher.InvokeAsync(() => InHardwareSession(output.Serial, () =>
            {
                if (!ReferenceEquals(hardwareOutput, output)) return;
                if (result is { Status: "Streaming", Frames: 1 }) AdoptHardwareOutput(output);
                hardwareStatus = result.Status switch
                {
                    "Connected" => HardwareVideo && hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal)
                        ? "已连接 · 正在恢复视频" : "已连接 · 正在发送首帧",
                    "Streaming" => "正在输出",
                    "WaitingForDevice" => $"等待同一 {HardwareModelFor(output.Serial)} 重连" +
                        (HardwareVideo ? " · 视频已暂停" : string.IsNullOrWhiteSpace(result.Error) ? "" : " · " + result.Error),
                    "BlockedByCraft" => "MiraBox Craft 正在运行",
                    "Failed" => "输出失败：" + result.Error,
                    "Stopped" => "已停止发送",
                    _ => result.Status
                };
                RenderHardwareStatus();
            })));
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => InHardwareSession(output.Serial, () => hardwareStatus = "输出失败：" + ex.Message));
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => InHardwareSession(output.Serial, () =>
            {
                if (!ReferenceEquals(hardwareOutput, output)) { output.Dispose(); return; }
                if (hardwareStatus == "正在输出" || hardwareStatus == "正在连接 USB")
                    hardwareStatus = "已停止发送";
                if (output.Identification && output.Frames == 0 && hardwareStatus == "已停止发送")
                    hardwareStatus = "未发送识别画面，请检查设备连接";
                hardwareStatus += $" · {output.Frames} 帧 · {output.Connections} 次连接";
                hardwareOutput = null;
                hardwareStill = null;
                hardwareVideoResourceId = null;
                hardwarePreviewEnds = null;
                PauseHardwarePlaylist();
                output.Dispose();
                RenderHardwareStatus();
            }));
        }
    }

    async Task StopHardwareOutput()
    {
        if (CaptureHardwareOutput() is { } saved)
        {
            stoppedHardwareOutput = saved;
            stoppedHardwareStill = hardwareStill;
        }
        if (hardwareApplySerial is { } stoppedSerial) editorPreviewReturns.Remove(stoppedSerial);
        if (hardwareOutput is not { Identification: true })
        {
            var hadPending = pendingHardwareRestore != null;
            pendingHardwareRestore = null;
            manualHardwareSubmission = null;
            if (hadPending)
            {
                hardwareStatus = "已停止发送";
                RenderHardwareStatus();
            }
        }
        await StopHardwareTransport();
        try { SaveWorkspace(); }
        catch (Exception ex) { Notice.Text = "保存停止状态失败：" + ex.Message; }
    }

    async Task StopHardwareTransport()
    {
        hardwareOutput?.Stop();
        await hardwareOutputCompletion;
    }

    async void StopHardwareBeforeClose(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (e.Cancel || !HardwareSessionSerials().Any(serial => HardwareOutputFor(serial) != null)) return;
        e.Cancel = true;
        if (closingForHardware) return;
        closingForHardware = true;
        await StopAllHardwareTransport();
        await Dispatcher.InvokeAsync(Close);
    }

    async void StopHardwareFromFooter(object sender, RoutedEventArgs e) => await StopHardwareOutput();

    string HardwareTransferStatus(bool showConnections = false)
    {
        if (hardwareOutput is not { } active)
            return hardwareStatus.Length > 0 ? hardwareStatus : "本次尚未开始发送";
        var status = active.IsStopping ? "正在停止" : hardwareStatus != "正在输出" ? hardwareStatus :
            active.VideoPath == null ? $"USB 正在发送 · {active.Frames} 帧" :
            active.VideoPaused ? "视频已暂停" :
            active.VideoRate is { } measured
                ? $"USB 实际 {measured.ActualFps:F1} FPS · 目标 {measured.RequestedFps} FPS · Q{measured.JpegQuality}" +
                  (measured.EffectiveFps < measured.RequestedFps ? $" · 自动降档，已降至 {measured.EffectiveFps} FPS" : "")
                : $"USB 正在发送 · 目标 {active.ActiveVideoProfile.FramesPerSecond} FPS";
        return showConnections ? $"{status} · {active.Connections} 次连接" : status;
    }

    string HardwareStatusLine()
    {
        var labels = new[] { HardwareModel + " 实机", hardwareContent, HardwareTransferStatus(showConnections: true), HardwareOutputProgress() };
        return string.Join(" · ", labels.Where(label => !string.IsNullOrWhiteSpace(label)));
    }

    void ReadLastHardwareContent() =>
        hardwareLastWritten = hardwareApplySerial is { } serial ? hardwareDiagnostics.LastWrittenContent(serial) : null;

    void OnHardwareDiagnostic(D92Diagnostic entry)
    {
        hardwareDiagnostics.Record(entry);
        if (entry.Event == "usb-first-write" && entry.Mode is "wallpaper" or "clock" or "video" ||
            entry.Event is "Failed" or "BlockedByCraft" or "WaitingForDevice" or "submitted" or "Stopped" or "video-failed")
        {
            _ = Dispatcher.InvokeAsync(() => InHardwareSession(entry.Serial, () =>
            {
                if (entry.UsageResourceId is { } resourceId && state.RecordHardwareUse(resourceId))
                {
                    var options = hardwareOrientations.GetOptions(entry.Serial);
                    hardwareOrientations.SaveOptions(entry.Serial, entry.Rotation % 180 == 0
                        ? options with { PortraitContent = resourceId } : options with { LandscapeContent = resourceId });
                    homeRecommendationsDrawn = false;
                    if (page == "首页") UpdateHomePreview();
                    if (page == "资源库" && sort == "最常用") Render();
                    try { SaveWorkspace(); }
                    catch (Exception ex) { Notice.Text = "保存使用记录失败：" + ex.Message; }
                }
                if (entry.Serial != hardwareApplySerial) return;
                if (entry.SubmissionId == manualHardwareSubmission && entry.SubmissionId.HasValue)
                {
                    if (entry.Event == "WaitingForDevice")
                    {
                        manualHardwareWasOffline = true;
                        Notice.Text = $"{HardwareTargetLabel}：{HardwareOfflineSaved}";
                    }
                    else if (entry.Event == "usb-first-write")
                    {
                        if (manualHardwareWasOffline) Notice.Text = $"{HardwareModel} 已重新连接，正在显示「{entry.Content}」。";
                        manualHardwareSubmission = null;
                    }
                }
                if (entry.Event == "usb-first-write")
                {
                    if (editorRestoreVideoPaused.Remove(entry.Serial)) hardwareOutput?.SetVideoPaused(true);
                    hardwareLastWritten = entry.Content;
                    if (hardwareOutput?.SubmissionId == entry.SubmissionId)
                    {
                        hardwareStatus = "正在输出";
                        RenderHardwareStatus();
                    }
                }
                TrackHardwareSchedule(entry);
                if (entry.Event == "video-failed" && hardwareOutput?.RequestedSubmissionId == entry.SubmissionId)
                    HandleHardwareVideoFailure(entry.Detail);
            }));
        }
    }

    void OpenHardwareDiagnostics()
    {
        var path = hardwareDiagnostics.Path;
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "尚无实机发送记录。每次应用、首帧写入和连接变化都会记在此工作区。",
                "D92 发送诊断", MessageBoxButton.OK);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "notepad.exe", Arguments = "\"" + path + "\"", UseShellExecute = true
        });
    }

    void RenderHardwareStatus()
    {
        if (backgroundHardwareContext) return;
        UpdateHardwareHomeCard();
        HardwareBar.Visibility = Visibility.Collapsed;
        HardwareStatus.Text = HardwareStatusLine();
        HardwareStop.IsEnabled = hardwareOutput != null || pendingHardwareRestore != null;
        HardwareRestore.Content = HardwareVideo ? "停止视频并恢复画面" : "结束预览";
        HardwarePause.Content = hardwarePlaylistPaused ? "继续轮播切换" : "暂停轮播切换";
        HardwareVideoPause.Content = HardwareVideoPaused ? "继续视频" : "暂停视频";
        HardwareVideoPause.IsEnabled = CanToggleHardwareVideo;
        HardwareVideoPause.Visibility = HardwareVideo ? Visibility.Visible : Visibility.Collapsed;
        HardwareRestore.Visibility = HardwareVideo || hardwarePreviewEnds != null ? Visibility.Visible : Visibility.Collapsed;
        HardwarePrevious.Visibility = HardwareNext.Visibility = HardwarePause.Visibility = HasHardwarePlaylist ? Visibility.Visible : Visibility.Collapsed;
    }
}
