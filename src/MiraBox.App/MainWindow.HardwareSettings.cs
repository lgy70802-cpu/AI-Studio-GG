using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    string? hardwareDetailSerial;
    readonly Dictionary<string, (string Name, int Rotation)> hardwareSettingsDrafts = [];
    sealed record HardwareSettingsView(string Serial, ContentControl Preview, TextBlock Status, TextBlock Badge, Button Toggle);
    HardwareSettingsView? hardwareSettingsView;

    void DrawHardwareDeviceSettings(Panel host, string[] serials)
    {
        hardwareSettingsView = null;
        if (serials.Length == 0)
        {
            var empty = new StackPanel();
            empty.Children.Add(HomeText(managedHardwareDetected == null ? "正在查找设备…" : "尚未连接设备", 20, bold: true));
            empty.Children.Add(HomeText("设备按型号和序列号识别，连接后可设置名称和方向。", 13, muted: true));
            empty.Children.Add(HomeButton("添加第一台设备", ConnectWizard, "PrimaryButton"));
            host.Children.Add(HomeCard(empty));
            return;
        }
        hardwareDetailSerial = serials.Contains(hardwareDetailSerial) ? hardwareDetailSerial :
            serials.Contains(hardwareApplySerial) ? hardwareApplySerial : serials[0];
        var serial = hardwareDetailSerial!;
        var details = BuildHardwareDeviceDetails(serial, serials.Length > 1);
        if (serials.Length == 1)
        {
            details.MaxWidth = 940;
            details.HorizontalAlignment = HorizontalAlignment.Stretch;
            host.Children.Add(details);
        }
        else
        {
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new());
            layout.ColumnDefinitions.Add(new() { Width = new GridLength(20) });
            layout.ColumnDefinitions.Add(new());
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var choices = new StackPanel();
            var scroll = new ScrollViewer { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            AutomationProperties.SetName(scroll, "设备列表");
            foreach (var candidate in serials)
            {
                var content = new StackPanel();
                var name = HomeText(HardwareDeviceName(candidate), 15, bold: true);
                name.TextWrapping = TextWrapping.NoWrap;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                content.Children.Add(name);
                var model = HardwareModelFor(candidate);
                var size = D92StaticImage.Geometry(hardwareOrientations.Get(candidate), model);
                content.Children.Add(HomeText($"{model} · {size.Width} × {size.Height}", 12, muted: true));
                var online = managedHardwareDetected?.Any(device => device.SerialNumber == candidate) == true;
                var badge = PlaylistBadge(online ? "已连接" : "未连接", online ? "#E2F7EF" : "#EEF3F6",
                    online ? "#067647" : "#53677C");
                badge.HorizontalAlignment = HorizontalAlignment.Left;
                badge.Margin = new Thickness(0, 8, 0, 0);
                content.Children.Add(badge);
                var choose = HomeButton("", () => { hardwareDetailSerial = candidate; Render(); }, "SidebarCardChoice");
                choose.Content = content;
                choose.Tag = candidate == serial ? "Selected" : null;
                choose.Width = 210;
                choose.Margin = new Thickness(0, 0, 0, 10);
                AutomationProperties.SetName(choose, $"{HardwareDeviceName(candidate)}，{model}，{candidate}");
                choices.Children.Add(choose);
            }
            layout.Children.Add(scroll);
            layout.Children.Add(details);
            host.Children.Add(layout);
            layout.SizeChanged += (_, e) => Arrange(e.NewSize.Width);
            Arrange(Math.Max(0, Width - 290));

            void Arrange(double width)
            {
                var wide = width >= 740;
                layout.ColumnDefinitions[0].Width = wide ? new GridLength(210) : new GridLength(0);
                layout.ColumnDefinitions[1].Width = wide ? new GridLength(20) : new GridLength(0);
                choices.Orientation = wide ? Orientation.Vertical : Orientation.Horizontal;
                scroll.HorizontalScrollBarVisibility = wide ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
                Grid.SetColumnSpan(scroll, wide ? 1 : 3);
                Grid.SetColumn(details, wide ? 2 : 0);
                Grid.SetColumnSpan(details, wide ? 1 : 3);
                Grid.SetRow(details, wide ? 0 : 1);
                details.Margin = wide ? new Thickness(0) : new Thickness(0, 14, 0, 0);
            }
        }
        UpdateHardwareSettingsPreview();
    }

    StackPanel BuildHardwareDeviceDetails(string serial, bool multiple)
    {
        var model = HardwareModelFor(serial);
        var rotation = hardwareOrientations.Get(serial);
        var draft = hardwareSettingsDrafts.GetValueOrDefault(serial, (Name: HardwareDeviceName(serial), Rotation: rotation));
        var details = new StackPanel();
        var form = new StackPanel();
        var identity = HomeText("序列号 " + serial, 12, muted: true);
        identity.TextWrapping = TextWrapping.Wrap;
        var identityBadge = new Border { Child = identity, CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 5, 10, 5), Background = (Brush)FindResource("TintBrush") };
        form.Children.Add(HomeHeader(HomeText(HardwareDeviceName(serial), 22, bold: true), identityBadge));
        var preview = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        form.Children.Add(new Border { Child = preview, Height = 210, Padding = new Thickness(12),
            CornerRadius = new CornerRadius(18), Background = (Brush)FindResource("HomePreviewBrush"),
            Margin = new Thickness(0, 14, 0, 16) });
        var fields = new Grid();
        fields.ColumnDefinitions.Add(new());
        fields.ColumnDefinitions.Add(new());
        for (var row = 0; row < 2; row++) fields.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var name = new TextBox { Text = draft.Name, MaxLength = 30, Height = 36 };
        name.SetResourceReference(StyleProperty, "PlaylistSeconds");
        var angle = new ComboBox { ItemsSource = new[] { "竖屏 · 0°", "横屏 · 90°", "竖屏翻转 · 180°", "横屏翻转 · 270°" },
            SelectedIndex = draft.Rotation / 90, Height = 36 };
        angle.ToolTip = "保存时自动切换此设备的输出方向";
        ToolTipService.SetShowOnDisabled(angle, true);
        var raw = new TextBox { Text = model == "D69" ? "1280 × 480" : "1920 × 462", IsReadOnly = true, Height = 36 };
        var connection = new TextBox { Text = "USB · " + model, IsReadOnly = true, Height = 36 };
        raw.SetResourceReference(StyleProperty, "PlaylistSeconds");
        connection.SetResourceReference(StyleProperty, "PlaylistSeconds");
        raw.IsTabStop = connection.IsTabStop = false;
        Field("设备名称", name, 0, 0);
        Field("显示方向", angle, 0, 1);
        Field("原始输出尺寸", raw, 1, 0);
        Field("当前连接", connection, 1, 1);
        name.TextChanged += (_, _) => hardwareSettingsDrafts[serial] = (name.Text, angle.SelectedIndex * 90);
        angle.SelectionChanged += (_, _) => hardwareSettingsDrafts[serial] = (name.Text, angle.SelectedIndex * 90);
        form.Children.Add(fields);
        if (multiple) form.Children.Add(HomeText("同型号设备按序列号区分，不按插入顺序。", 12, muted: true));
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var identify = HomeButton("识别这台设备", () => _ = IdentifyHardwareSettings(serial), "PillButton");
        var identifyingOutput = HardwareOutputFor(serial);
        identify.IsEnabled = managedHardwareDetected?.Any(device => device.SerialNumber == serial) == true &&
            (identifyingOutput == null || !identifyingOutput.Identification && identifyingOutput.VideoPath == null);
        identify.ToolTip = identify.IsEnabled ? "短暂显示型号识别画面" : "连接此设备并结束视频或临时预览后可识别";
        ToolTipService.SetShowOnDisabled(identify, true);
        if (multiple) actions.Children.Add(identify);
        var save = HomeButton("保存设备设置", () => { }, "PrimaryButton");
        save.Click += async (_, _) =>
        {
            var previousSerial = hardwareApplySerial;
            try
            {
                var deviceName = name.Text.Trim();
                if (deviceName.Length is < 1 or > 30)
                    throw new ArgumentException("设备名称须为 1–30 个字符。");
                IsEnabled = false;
                SelectHardwareSession(serial);
                await ChangeHardwareDirection(angle.SelectedIndex % 2 == 0 ? 90 : 0, angle.SelectedIndex * 90);
                hardwareOrientations.SaveName(serial, deviceName);
                hardwareDeviceNames[serial] = deviceName;
                hardwareSettingsDrafts.Remove(serial);
                SaveWorkspace();
            }
            catch (Exception ex) { Notice.Text = ex.Message; }
            finally
            {
                SelectHardwareSession(previousSerial);
                IsEnabled = true;
                Render();
            }
        };
        actions.Children.Add(save);
        form.Children.Add(actions);
        details.Children.Add(HomeCard(form));

        details.Children.Add(BuildDisplayPowerSettings(serial));

        var playback = new StackPanel();
        playback.Children.Add(HomeText("独立播放", 17, bold: true));
        var status = HomeText("", 13, muted: true);
        status.Margin = new Thickness(0, 8, 0, 8);
        playback.Children.Add(status);
        var controls = new WrapPanel();
        controls.Children.Add(HomeButton("更换内容", () => { Select(); Navigate("资源库"); }, "PillButton"));
        controls.Children.Add(HomeButton("设置轮播列表", () =>
        {
            Select();
            selectedList = state.Playlists.FirstOrDefault(list => list.DeviceId == HardwareSchedulePrefix + serial)?.Id;
            Navigate("轮播列表");
        }, "PillButton"));
        var toggle = HomeButton("恢复显示", () =>
        {
            Select();
            if (CanResumeHardware) ResumeStoppedHardware();
            else if (hardwareOutput != null || pendingHardwareRestore != null) _ = StopHardwareOutput();
            else ResumeStoppedHardware();
        }, "PillButton");
        controls.Children.Add(toggle);
        var badge = HomeText("", 12, muted: true);
        controls.Children.Add(badge);
        playback.Children.Add(controls);
        var playbackCard = HomeCard(playback);
        playbackCard.Margin = new Thickness(0, 14, 0, 14);
        details.Children.Add(playbackCard);
        hardwareSettingsView = new(serial, preview, status, badge, toggle);

        var options = hardwareOrientations.GetOptions(serial);
        var advanced = new StackPanel { Margin = new Thickness(16) };
        advanced.Children.Add(HomeText("视频模式", 12, muted: true));
        var d69Rates = new[] { 5, 10, 20, 24 };
        var quality = new ComboBox { ItemsSource = model == "D69"
                ? d69Rates.Select(fps => $"{fps} FPS").ToArray()
                : new[] { "画质优先 · 24 FPS", "流畅优先 · 30 FPS" },
            SelectedIndex = model == "D69" ? Math.Max(0, Array.IndexOf(d69Rates,
                d69Rates.Contains(options.D69VideoFps) ? options.D69VideoFps : 20)) :
                options.VideoMode == "Smooth" || options.VideoMode == null &&
                preferences.D92VideoMode == DeskDisplay.Core.Models.VideoPlaybackMode.Smooth ? 1 : 0,
            IsEnabled = true, Margin = new Thickness(0, 6, 0, 12) };
        advanced.Children.Add(quality);
        if (hardwareApplySerial == serial)
        {
            advanced.Children.Add(HomeText("图片 / 独立视频显示方式", 12, muted: true));
            advanced.Children.Add(HardwareMediaFitControl());
            advanced.Children.Add(HomeText("完整显示保留全部内容，比例不同时留边；裁切铺满会裁掉超出画布的部分。主题布局在编辑器中调整。", 12, muted: true));
        }
        AutomationProperties.SetName(quality, "视频模式");
        if (model == "D69") advanced.Children.Add(HomeText("D69 支持横竖屏视频，默认 20 FPS，负载较高时自动降低帧率。", 12, muted: true));
        advanced.Children.Add(HomeText("物理屏关联组", 12, muted: true));
        var group = new TextBox { Text = options.Group, MaxLength = 40, Height = 36, Margin = new Thickness(0, 6, 0, 8) };
        advanced.Children.Add(group);
        AutomationProperties.SetName(group, "物理屏关联组");
        advanced.Children.Add(HomeText("关联组用于标记同一块物理屏幕。视频档位在下次开始播放视频时生效。", 12, muted: true));
        advanced.Children.Add(HomeButton("保存高级设置", () =>
        {
            hardwareOrientations.SaveOptions(serial, hardwareOrientations.GetOptions(serial) with {
                VideoMode = model == "D69" ? options.VideoMode : quality.SelectedIndex == 1 ? "Smooth" : "Quality",
                D69VideoFps = model == "D69" ? d69Rates[quality.SelectedIndex] : options.D69VideoFps,
                Group = group.Text.Trim() });
            Notice.Text = "高级设置已保存。";
        }, "PillButton"));
        var expander = new Expander { Header = "高级设置 · 输出性能与物理屏关联", Content = advanced };
        var advancedCard = HomeCard(expander);
        advancedCard.Padding = new Thickness(14);
        details.Children.Add(advancedCard);
        return details;

        void Select()
        {
            EnsureHardwareTargetCanChange(serial);
            if (hardwareApplySerial != serial) SetHardwareApplySerial(serial);
        }

        void Field(string label, FrameworkElement control, int row, int column)
        {
            var field = new StackPanel { Margin = new Thickness(0, 0, column == 0 ? 16 : 0, 12) };
            field.Children.Add(HomeText(label, 12, muted: true));
            field.Children.Add(control);
            AutomationProperties.SetName(control, label);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, column);
            fields.Children.Add(field);
        }
    }

    void UpdateHardwareSettingsPreview()
    {
        if (page != "设备管理" || hardwareSettingsView is not { } view || IsPlaylistDemo) return;
        if (view.Serial != hardwareApplySerial)
        {
            InHardwareSession(view.Serial, UpdateHardwareSettingsPreview);
            return;
        }
        var selected = hardwareApplySerial == view.Serial;
        var output = selected ? hardwareOutput : null;
        var saved = stoppedHardwareOutput?.Serial == view.Serial ? stoppedHardwareOutput : null;
        var online = managedHardwareDetected?.Any(device => device.SerialNumber == view.Serial) == true;
        view.Badge.Text = online ? "已连接" : "未连接";
        view.Toggle.Content = CanResumeHardware ? "恢复显示" :
            output != null || selected && pendingHardwareRestore != null ? "停止输出" : "恢复显示";
        view.Toggle.IsEnabled = selected && (output != null || pendingHardwareRestore != null) || saved != null;
        var id = selected ? hardwareVideoResourceId ?? hardwareStill?.ResourceId ??
            saved?.VideoResourceId ?? saved?.BaseResourceId : saved?.VideoResourceId ?? saved?.BaseResourceId;
        view.Status.Text = "当前内容：" + ResourceName(id) + (selected && output != null ? " · " + HardwareTransferStatus() :
            !online ? " · 设备离线" : " · 输出已停止");
        var bytes = output is { VideoPath: null } ? output.CurrentStill : null;
        var key = (id, bytes, hardwareOrientations.Get(view.Serial));
        if (Equals(view.Preview.Tag, key)) return;
        view.Preview.Tag = key;
        if (bytes != null)
            view.Preview.Content = new Image { Source = DecodeHardwareHomeFrame(bytes, output!.Rotation), Stretch = Stretch.Uniform };
        else if (id != null && state.Resources.TryGetValue(id, out var resource))
        {
            var size = D92StaticImage.Geometry(hardwareOrientations.Get(view.Serial), HardwareModelFor(view.Serial));
            view.Preview.Content = Thumbnail(resource, new(size.Width, size.Height), 550, 185);
        }
        else
        {
            var empty = HomeText("未选择内容", 14, muted: true);
            empty.TextAlignment = TextAlignment.Center;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            view.Preview.Content = empty;
        }
    }

    async Task IdentifyHardwareSettings(string serial)
    {
        try
        {
            var rotation = hardwareOrientations.Get(serial);
            var bytes = D92StaticImage.Encode(rotation, DrawHardwareMarker, HardwareModelFor(serial));
            if (HardwareOutputFor(serial) is { } active)
            {
                if (active.Identification || active.VideoPath != null ||
                    InHardwareSession(serial, () => hardwarePreviewEnds != null))
                { Notice.Text = "请先停止其他输出，或结束视频和临时预览。"; return; }
                active.SubmitStill(bytes, "识别画面", "identification");
                var request = active.RequestedSubmissionId;
                await Task.Delay(2000);
                InHardwareSession(serial, () =>
                {
                    if (ReferenceEquals(hardwareOutput, active) && active.RequestedSubmissionId == request && !active.IsStopping)
                        RestoreHardwareStill();
                });
            }
            else StartHardwareOutput(serial, rotation, bytes, "识别画面", identification: true);
        }
        catch (Exception ex) { Notice.Text = "识别设备失败：" + ex.Message; }
    }
}
