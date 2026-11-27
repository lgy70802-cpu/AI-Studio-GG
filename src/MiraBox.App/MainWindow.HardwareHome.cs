using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record HardwareHomeCard(Image Image, TextBlock Placeholder,
        TextBlock ContentName, TextBlock Status, TextBlock Progress, Button Stop, Button Restore, Button PauseVideo);

    HardwareHomeCard? hardwareHomeCard;
    readonly DispatcherTimer hardwareVideoPreviewTimer = new(DispatcherPriority.Render)
        { Interval = TimeSpan.FromSeconds(1d / 15) };

    void ConfigureHardwareVideoPreview()
    {
        hardwareVideoPreviewTimer.Tick += (_, _) => RefreshHardwareVideoPreview();
        StateChanged += (_, _) => UpdateHardwareVideoPreviewDemand();
        IsVisibleChanged += (_, _) => UpdateHardwareVideoPreviewDemand();
        Closed += (_, _) => hardwareVideoPreviewTimer.Stop();
    }

    void UpdateHardwareVideoPreviewDemand()
    {
        var visible = page == "首页" && IsVisible && WindowState != WindowState.Minimized && hardwareHomeCard != null;
        var dpi = VisualTreeHelper.GetDpi(this);
        var image = hardwareHomeCard?.Image;
        var width = Math.Clamp((int)Math.Ceiling((image?.ActualWidth ?? 0) * dpi.DpiScaleX), 320, 1280);
        var height = Math.Clamp((int)Math.Ceiling((image?.ActualHeight ?? 0) * dpi.DpiScaleY), 180, 720);
        foreach (var serial in HardwareSessionSerials())
            HardwareOutputFor(serial)?.SetVideoPreviewRequest(visible && serial == hardwareApplySerial
                ? new(true, width, height, 15) : VideoPreviewRequest.Disabled);
        if (visible && hardwareOutput?.VideoPath != null) hardwareVideoPreviewTimer.Start();
        else hardwareVideoPreviewTimer.Stop();
    }

    void RefreshHardwareVideoPreview()
    {
        if (page != "首页" || !IsVisible || WindowState == WindowState.Minimized ||
            hardwareHomeCard is not { } card || hardwareOutput is not { Identification: false, VideoPath: not null } output ||
            output.Serial != hardwareApplySerial || output.VideoPreview is not { } videoFrame) return;
        if (!ReferenceEquals(card.Image.Tag, videoFrame))
        {
            card.Image.Source = videoFrame;
            card.Image.Tag = videoFrame;
        }
        card.Placeholder.Visibility = Visibility.Collapsed;
    }

    Border BuildHardwareHomeCard()
    {
        var canvas = HardwareCanvas;
        var portrait = canvas.Width < canvas.Height;
        var status = HomeText("", 12, muted: true);
        var image = new Image { Stretch = Stretch.Uniform, ClipToBounds = true };
        var placeholder = HomeText("", 14, muted: true);
        placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        placeholder.TextAlignment = TextAlignment.Center;
        var view = new Grid();
        view.Children.Add(image);
        view.Children.Add(placeholder);
        var frame = new Border
        {
            Child = view, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 12, 0, 12)
        };
        if (portrait)
        {
            frame.Width = PortraitCardWidth(canvas);
            frame.Height = PortraitPreviewHeight;
            frame.HorizontalAlignment = HorizontalAlignment.Center;
        }
        frame.SetResourceReference(Border.BackgroundProperty, "HomePreviewBrush");
        frame.SizeChanged += (_, e) =>
        {
            if (!e.WidthChanged) return;
            var width = e.NewSize.Width;
            var height = portrait ? PortraitPreviewHeight : width * canvas.Height / canvas.Width;
            if (!portrait) frame.Height = height;
            view.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 16, 16);
        };

        var name = HomeText("", 16, bold: true);
        var labels = new StackPanel { Orientation = portrait ? Orientation.Vertical : Orientation.Horizontal };
        labels.Children.Add(name);
        status.Margin = portrait ? new Thickness(0, 4, 0, 0) : new Thickness(10, 0, 0, 0);
        labels.Children.Add(status);
        var progress = HomeText("", 13, muted: true);
        labels.Children.Add(progress);
        var actions = new WrapPanel();
        var pauseVideo = HomeButton("暂停视频", ToggleHardwareVideoPause, "SmallButton");
        var restore = HomeButton("结束预览", EndHardwarePreviewOrVideo, "SmallButton");
        var stop = HomeButton("停止实机输出", () =>
        {
            if (CanResumeHardware) ResumeStoppedHardware();
            else _ = StopHardwareOutput();
        }, "SmallButton");
        actions.Children.Add(BuildHomeOrientation(portrait, ChangeHomeHardwareDirection));
        var resourceId = hardwareVideoResourceId ?? hardwareStill?.ResourceId ??
            pendingHardwareRestore?.VideoResourceId ?? pendingHardwareRestore?.BaseResourceId ??
            stoppedHardwareOutput?.VideoResourceId ?? stoppedHardwareOutput?.BaseResourceId;
        if (resourceId != null && state.Resources.TryGetValue(resourceId, out var resource) && IsHardwareTheme(resource))
        {
            var label = Workspace.Difference(resource, canvas) is <= .05 ? "编辑" : $"添加 {SpecText(canvas)} 布局";
            actions.Children.Add(HomeButton(label, () => OpenEditor(resource), "SmallButton"));
        }
        actions.Children.Add(HomeButton("更换内容", () => BrowseHomeDevice(null), "SmallPrimaryButton"));

        var panel = new StackPanel();
        panel.Children.Add(portrait ? WrapPortraitHomePreview(frame) : frame);
        if (portrait)
        {
            panel.Children.Add(labels);
            panel.Children.Add(actions);
        }
        else panel.Children.Add(HomeHeader(labels, actions));

        hardwareHomeCard = new(image, placeholder, name, status, progress, stop, restore, pauseVideo);
        UpdateHardwareHomeCard();
        return HomeCard(panel);
    }

    Task ChangeHomeHardwareDirection(int direction) => ChangeHardwareDirection(direction);

    async Task ChangeHardwareDirection(int direction, int? exactRotation = null)
    {
        if (hardwareApplySerial is not { } serial ||
            (exactRotation.HasValue ? hardwareApplyRotation == exactRotation.Value : HardwareBrowseRotation == direction)) return;
        var previousRotation = hardwareApplyRotation;
        var snapshot = CaptureHardwareOutput();
        var options = hardwareOrientations.GetOptions(serial);
        options = previousRotation % 180 == 0
            ? options with { PortraitRotation = previousRotation }
            : options with { LandscapeRotation = previousRotation };
        hardwareOrientations.SaveOptions(serial, options);
        var previousContent = snapshot?.VideoResourceId ?? snapshot?.BaseResourceId;
        if (previousContent != null)
        {
            options = previousRotation % 180 == 0
                ? options with { PortraitContent = previousContent }
                : options with { LandscapeContent = previousContent };
            hardwareOrientations.SaveOptions(serial, options);
        }
        var savedRotation = direction == 90 ? options.PortraitRotation : options.LandscapeRotation;
        var rotation = direction == 90
            ? savedRotation is 0 or 180 ? savedRotation : 0
            : savedRotation is 90 or 270 ? savedRotation : 90;
        rotation = exactRotation ?? rotation;
        var geometry = D92StaticImage.Geometry(rotation, HardwareModel);
        var canvas = new MiraBox.Core.Size(geometry.Width, geometry.Height);
        var rememberedId = direction == 90 ? options.PortraitContent : options.LandscapeContent;
        var remembered = rememberedId == null ? null : state.Resources.GetValueOrDefault(rememberedId);
        var available = remembered is { Local: true } && StartupResourceAvailable(remembered) &&
            (!IsHardwareTheme(remembered) || Workspace.Difference(remembered, canvas) is <= .05);
        IsEnabled = false;
        try
        {
            await StopHardwareOutput();
            SaveHardwareRotation(serial, rotation);
            if (available) UseHardwareContent(remembered!);
            SaveWorkspace();
            Render();
            Notice.Text = available ? "" : "此方向暂无可恢复的内容，请选择内容。";
        }
        catch
        {
            if (HardwareOutputFor(serial) == null)
            {
                SaveHardwareRotation(serial, previousRotation);
                if (snapshot != null) ResumeStoppedHardware();
            }
            Render();
            throw;
        }
        finally { IsEnabled = true; }
    }

    void UpdateHardwareHomeCard()
    {
        if (hardwareHomeCard is not { } card || page != "首页") return;
        var output = hardwareOutput is { Identification: false } active && active.Serial == hardwareApplySerial
            ? active : null;

        var pending = pendingHardwareRestore?.Serial == hardwareApplySerial && pendingHardwareRestore != null;
        card.Stop.IsEnabled = output is { IsStopping: false } || pending || CanResumeHardware;
        card.Stop.Content = CanResumeHardware ? "恢复输出" : "停止实机输出";
        var lastOutputMatches = hardwareApplySerial != null && lastHardwareContentSerial == hardwareApplySerial;
        var stateLabel = output == null ? SelectedHardwareState() :
            hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal) ? "等待重连" :
            HardwareVideoPaused ? "已暂停" : hardwareStatus == "正在输出" ? "● 正在输出" : hardwareStatus;
        card.Status.Text = $"{stateLabel} · {SpecText(HardwareCanvas)}";
        card.Status.Foreground = (Brush)FindResource(stateLabel == "● 正在输出" ? "RunningInkBrush" : "MutedBrush");
        card.ContentName.Text = output != null || lastOutputMatches || pending ? hardwareContent :
            hardwareLastWritten is { } previous ? $"上次发送「{previous}」" : "尚未应用内容";
        card.ContentName.Text = MaterialDisplayName(card.ContentName.Text);
        card.Progress.Text = output == null ? "" : HardwareOutputProgress();
        card.Progress.Visibility = card.Progress.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        card.Restore.Content = HardwareVideo ? "停止视频并恢复画面" : "结束预览";
        card.Restore.Visibility = output != null && (HardwareVideo || hardwarePreviewEnds != null)
            ? Visibility.Visible : Visibility.Collapsed;
        card.PauseVideo.Visibility = output != null && HardwareVideo ? Visibility.Visible : Visibility.Collapsed;
        card.PauseVideo.IsEnabled = CanToggleHardwareVideo;
        card.PauseVideo.Content = HardwareVideoPaused ? "继续视频" : "暂停视频";

        if (output?.VideoPath != null && output.VideoPreview is { } videoFrame)
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!ReferenceEquals(card.Image.Tag, videoFrame)) { card.Image.Source = videoFrame; card.Image.Tag = videoFrame; }
            card.Placeholder.Visibility = Visibility.Collapsed;
            return;
        }
        if (output == null && (stoppedHardwareOutput?.VideoResourceId ?? stoppedHardwareOutput?.BaseResourceId) is { } savedId &&
            state.Resources.TryGetValue(savedId, out var savedResource) && IsHardwareTheme(savedResource) &&
            Workspace.Difference(savedResource, HardwareCanvas) is not <= .05)
        {
            card.Image.Source = null; card.Image.Tag = null;
            card.Placeholder.Text = $"当前主题缺少 {SpecText(HardwareCanvas)} 布局，请添加对应布局。";
            card.Placeholder.Visibility = Visibility.Visible;
            return;
        }
        if (output == null || output.VideoPath != null)
        {
            if (CanResumeHardware && stoppedHardwareStill is { } still)
            {
                if (!ReferenceEquals(card.Image.Tag, still.Bytes))
                {
                    card.Image.Source = DecodeHardwareHomeFrame(still.Bytes, hardwareApplyRotation);
                    card.Image.Tag = still.Bytes;
                }
                card.Placeholder.Visibility = Visibility.Collapsed;
                return;
            }
            if (CanResumeHardware && stoppedHardwareOutput is { } saved &&
                state.Resources.TryGetValue(saved.VideoResourceId ?? saved.BaseResourceId ?? "", out var resource))
            {
                if (Equals(card.Image.Tag, (resource.Id, HardwareCanvas, HardwareMediaFit)))
                {
                    card.Placeholder.Visibility = Visibility.Collapsed;
                    return;
                }
                var preview = Thumbnail(resource, HardwareCanvas, HardwareCanvas.Width < HardwareCanvas.Height ? 130 : 800,
                    HardwareCanvas.Width < HardwareCanvas.Height ? 540 : 260, HardwareMediaFit);
                var size = new System.Windows.Size(preview.Width, preview.Height);
                preview.Measure(size);
                preview.Arrange(new Rect(size));
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height),
                    96, 96, PixelFormats.Pbgra32);
                bitmap.Render(preview);
                card.Image.Source = bitmap;
                card.Image.Tag = (resource.Id, HardwareCanvas, HardwareMediaFit);
                card.Placeholder.Visibility = Visibility.Collapsed;
                return;
            }
            card.Image.Source = null;
            card.Image.Tag = null;
            card.Placeholder.Text = output == null ? "从首页或资源库应用壁纸、主题或视频" :
                hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal) ? "视频已暂停，等待设备重连" :
                HardwareVideoPaused ? "视频已暂停" : "视频发送中";
            card.Placeholder.Visibility = Visibility.Visible;
            return;
        }

        var bytes = output.CurrentStill;
        if (ReferenceEquals(card.Image.Tag, bytes)) return;
        card.Image.Tag = bytes;
        try
        {
            card.Image.Source = DecodeHardwareHomeFrame(bytes, output.Rotation);
            card.Placeholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            card.Image.Source = null;
            card.Placeholder.Text = "发送帧预览不可用";
            card.Placeholder.Visibility = Visibility.Visible;
        }
    }

    static BitmapSource DecodeHardwareHomeFrame(byte[] bytes, int rotation)
    {
        using var stream = new MemoryStream(bytes);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (rotation == 0) return frame;
        var image = new TransformedBitmap(frame, new RotateTransform((360 - rotation) % 360));
        image.Freeze();
        return image;
    }
}
