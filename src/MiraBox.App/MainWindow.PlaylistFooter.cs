using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    (bool Hardware, string? Content, bool Playing, bool Running, string? Device, bool Preview, byte[]? Bytes, string? Transfer,
        bool Video)? playlistFooterKey;
    TextBlock? playlistFooterVideoProgress;
    TextBlock? playlistFooterCaption;
    Button? playlistFooterVideoToggle;

    void RenderPlaylistFooter(OutputRow[] rows)
    {
        var home = page == "首页";
        var list = home ? HomeFooterPlaylist() : state.Playlists.Find(item => item.Id == selectedList);
        var row = rows.FirstOrDefault(item => item.DeviceId == list?.DeviceId) ??
            rows.FirstOrDefault(item => item.DeviceId == target) ?? rows.FirstOrDefault();
        var physical = HardwarePlaybackSelected;
        var output = hardwareOutput is { Identification: false } active && active.Serial == hardwareApplySerial ? active : null;
        var running = physical ? output != null && !output.IsStopping : row is { Online: true, Running: true };
        var playing = home ? list != null : physical ? HasHardwarePlaylist : row is { Online: true, Total: > 0 };
        var preview = physical ? hardwarePreviewEnds != null || HardwareVideo :
            row != null && state.Preview?.DeviceId == row.DeviceId;
        var name = physical ? (hardwareContent.Length == 0 ? hardwareLastWritten : hardwareContent) :
            row?.ResourceName;
        var bytes = physical && output?.VideoPath == null ? output?.CurrentStill ?? stoppedHardwareStill?.Bytes : null;
        var video = physical && output?.VideoPath != null;
        var key = (physical, name, playing, running, physical ? hardwareApplySerial : row?.DeviceId, preview, bytes,
            physical ? hardwareStatus : null, video);
        if (playlistFooterKey == key)
        {
            UpdateVideoControls();
            return;
        }
        playlistFooterKey = key;

        Outputs.Children.Clear();
        var line = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var thumb = new Border { Width = 54, Height = 34, CornerRadius = new CornerRadius(8), Background = ColorBrush("#E6F6FB"),
            Margin = new Thickness(0, 0, 8, 0), ClipToBounds = true };
        if (bytes != null && physical)
            thumb.Child = new Image { Source = DecodeHardwareHomeFrame(bytes, output?.Rotation ?? hardwareApplyRotation), Stretch = Stretch.Uniform };
        else if (video && hardwareVideoResourceId is { } videoId && state.Resources.TryGetValue(videoId, out var videoResource))
            thumb.Child = Thumbnail(videoResource, HardwareCanvas, 54, 34);
        else if (!physical && row?.ResourceId is { } id && state.Resources.TryGetValue(id, out var resource))
            thumb.Child = Thumbnail(resource, row.Canvas, 54, 34, row.Fit, liveClock: running);
        else if (physical && stoppedHardwareOutput is { } saved &&
            state.Resources.TryGetValue(saved.VideoResourceId ?? saved.BaseResourceId ?? "", out var stopped))
            thumb.Child = Thumbnail(stopped, HardwareCanvas, 54, 34);
        if (!home || running || playing) line.Children.Add(thumb);

        var information = new StackPanel { Width = video && playlistWindowWidth >= 900 ? 186 : 150, VerticalAlignment = VerticalAlignment.Center };
        var caption = physical && pendingHardwareRestore != null ? HardwareTransferStatus() :
            !running ? "未输出" : physical && hardwareStatus != "正在输出" ? HardwareTransferStatus() : "正在显示";
        var captionText = HomeText(caption, 13, bold: true);
        playlistFooterCaption = !home && video && hardwareStatus == "正在输出" ? captionText : null;
        if (!home) information.Children.Add(captionText);
        var resourceId = physical ? hardwareVideoResourceId ?? hardwareStill?.ResourceId : row?.ResourceId;
        if (home && playing) name = "当前正在轮播";
        else if (home && resourceId != null && state.Resources.ContainsKey(resourceId))
            name = $"{ResourceName(resourceId)} · {Entry(resourceId).Type}";
        if (home && !running && !playing)
        {
            var start = HomeButton("▷ 开启桌面轮播", () => { }, "SmallButton");
            var picker = new ContextMenu { PlacementTarget = start, Placement = PlacementMode.Top };
            var device = target == null ? null : state.Devices.GetValueOrDefault(target);
            picker.Items.Add(new MenuItem { Header = BuildHomePlaylists(device,
                (physical ? HardwareCanvas.Width < HardwareCanvas.Height : device?.Rotation % 180 != 0)
                    ? "竖屏" : "横屏"), StaysOpenOnClick = true });
            start.ContextMenu = picker;
            start.Click += (_, _) => picker.IsOpen = true;
            line.Children.Add(start);
        }
        var description = HomeText(name == null ? "未选择内容" : MaterialDisplayName(name), 12, muted: true);
        description.TextWrapping = TextWrapping.NoWrap;
        description.TextTrimming = TextTrimming.CharacterEllipsis;
        description.ToolTip = description.Text;
        information.Children.Add(description);
        playlistFooterVideoProgress = video && !home ? HomeText("", 12, muted: true) : null;
        playlistFooterVideoToggle = null;
        if (playlistFooterVideoProgress != null)
        {
            information.Children.Add(playlistFooterVideoProgress);
        }
        if (!home || running || playing) line.Children.Add(information);

        // The footer follows the global output mode, just like the device selector.
        var more = PlaylistGlyphButton("输出选项", "M3,8 h1 M7.5,8 h1 M12,8 h1", () => { }, 32);
        var options = new ContextMenu { PlacementTarget = more, Placement = PlacementMode.Top };
        options.SetResourceReference(StyleProperty, "ResourceContextMenu");
        more.ContextMenu = options;
        more.Click += (_, _) => options.IsOpen = true;
        if (physical)
        {
            AddMenu("停止实机输出", StopPhysicalOutput, output != null || playing || pendingHardwareRestore != null);
            if (preview) AddMenu(HardwareVideo ? "停止视频并恢复" : "结束预览", EndHardwarePreviewOrVideo);
            AddMenu("查看发送诊断", OpenHardwareDiagnostics);
            EnablePlaylistFooterHover(more, options);
        }
        else
        {
            if (state.Preview is { } pending && preview)
            {
                AddMenu("结束并恢复", () => state.EndPreview(pending.Token));
                AddMenu("保留并使用", () => UsePreview(pending));
            }
            AddMenu("切换输出", ShowHardwarePilot);
        }
        foreach (var direction in new[] { -1, 1 })
        {
            var label = direction < 0 ? "上一项" : "下一项";
            var button = HomeButton(OutputStepGlyph(direction), () =>
                {
                    if (physical) StepHardwarePlaylist(direction);
                    else if (row != null && state.Devices.TryGetValue(row.DeviceId, out var device)) StepHomePlaylist(device, direction);
                }, "SmallButton");
            button.Width = button.Height = 32;
            button.Padding = new Thickness(0);
            button.Margin = new Thickness(0, 2, 6, 2);
            button.ToolTip = label;
            AutomationProperties.SetName(button, label);
            button.IsEnabled = playing;
            line.Children.Add(button);
        }
        var resume = physical && CanResumeHardware;
        var stop = PlaylistGlyphButton(resume || !physical && !running ? "恢复输出" : "停止输出",
            resume || !physical && !running ? "M5,3 L12,8 L5,13 Z" : "M4,4 H12 V12 H4 Z", () =>
            {
                if (resume) ResumeStoppedHardware();
                else if (physical) StopPhysicalOutput();
                else if (row != null && state.Devices.TryGetValue(row.DeviceId, out var device)) ToggleDeviceOutput(device);
            }, 32);
        stop.IsEnabled = physical ? resume || running || playing || pendingHardwareRestore != null : row?.ResourceId != null;
        ToolTipService.SetShowOnDisabled(stop, true);
        line.Children.Add(stop);
        if (video)
        {
            playlistFooterVideoToggle = HomeButton("", ToggleHardwareVideoPause, "SmallButton");
            line.Children.Add(playlistFooterVideoToggle);
        }
        UpdateVideoControls();
        line.Children.Add(more);
        Outputs.Children.Add(line);

        void UpdateVideoControls()
        {
            if (playlistFooterVideoToggle is { } pause)
            {
                var label = HardwareVideoPaused ? "继续视频" : "暂停视频";
                if (!Equals(pause.Content, label)) pause.Content = label;
                pause.IsEnabled = CanToggleHardwareVideo;
            }
            if (playlistFooterVideoProgress == null) return;
            if (playlistFooterCaption is { } title)
                title.Text = HardwareVideoPaused ? "视频已暂停" : "正在播放视频";
            var text = HardwareVideoProgressText(showPause: false) ?? "";
            if (playing) text += $" · 轮播剩余 {Math.Max(0, Math.Ceiling(hardwarePlaylistRemaining.TotalSeconds))} 秒";
            if (playlistFooterVideoProgress.Text != text) playlistFooterVideoProgress.Text = text;
        }

        void StopPhysicalOutput()
        {
            PauseHardwarePlaylist();
            _ = StopHardwareOutput();
            Render();
        }

        void AddMenu(string text, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = text, IsEnabled = enabled };
            item.SetResourceReference(StyleProperty, "ResourceMenuItem");
            item.Click += (_, _) => action();
            options.Items.Add(item);
        }
    }
    static void EnablePlaylistFooterHover(Button label, ContextMenu menu)
    {
        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        var hoveringMenu = false;
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            if (!label.IsLoaded) return;
            if (!menu.IsOpen && label.IsMouseOver)
            {
                hoveringMenu = true;
                menu.IsOpen = true;
            }
            else if (hoveringMenu && !label.IsMouseOver && !menu.IsMouseOver)
                menu.IsOpen = false;
        };
        label.MouseEnter += (_, _) => delay.Start();
        label.MouseLeave += (_, _) => delay.Start();
        menu.MouseLeave += (_, _) => delay.Start();
        menu.Closed += (_, _) => { hoveringMenu = false; delay.Stop(); };
        label.Unloaded += (_, _) => { delay.Stop(); menu.IsOpen = false; };
    }
}
