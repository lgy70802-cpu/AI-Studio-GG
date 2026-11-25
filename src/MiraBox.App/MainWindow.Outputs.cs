using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MiraBox.Core;
using Size = MiraBox.Core.Size;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record OutputRow(string DeviceId, string Name, bool Online, bool Running, string? ResourceId,
        string ResourceName, Size Canvas, string Fit, string? ListName, int Position, int Total);

    OutputRow[]? lastOutputs;
    bool lastOutputsHardware;
    Guid? lastPreviewToken;
    TextBlock? previewStatus;

    void ChangeFooterContent(object sender, RoutedEventArgs e)
    {
        if (page == "首页" && HomeFooterPlaylist() is { } playlist)
        {
            selectedList = playlist.Id;
            Navigate("轮播列表");
            return;
        }
        if (target != null && state.Devices.TryGetValue(target, out var device)) BrowseHomeDevice(device);
        else Navigate("资源库");
    }

    Playlist? HomeFooterPlaylist() => HardwarePlaybackSelected
        ? state.Playlists.Find(item => IsHardwarePlaylist(item))
        : target != null && state.Devices.TryGetValue(target, out var device) ? HomePlaying(device) : null;

    void RenderOutputs()
    {
        if (backgroundHardwareContext) return;
        var rows = state.Devices.Values.Select(device =>
        {
            var resource = state.VisibleContent(device.Id) ?? device.Content;
            var list = HomePlaying(device);
            return new OutputRow(device.Id, device.Name, device.Online,
                device.Running || state.Preview?.DeviceId == device.Id, resource,
                ResourceName(resource), device.Canvas, state.VisibleFit(device.Id),
                list?.Name, list?.Index + 1 ?? 0, list?.Items.Count ?? 0);
        }).ToArray();
        var managing = page == "首页" && HomeFooterPlaylist() != null;
        FooterChangeLabel.Text = managing ? "管理轮播" : "更换内容";
        FooterChangeContent.ToolTip = FooterChangeLabel.Text;
        AutomationProperties.SetName(FooterChangeContent, FooterChangeLabel.Text);
        if (page is "轮播列表" or "首页" || HardwarePlaybackSelected)
        {
            HardwareBar.Visibility = Visibility.Collapsed;
            PreviewBar.Visibility = Visibility.Collapsed;
            RenderPlaylistFooter(rows);
            return;
        }

        if (playlistFooterKey != null)
        {
            playlistFooterKey = null;
            lastOutputs = null;
        }
        PreviewBar.Visibility = Visibility.Visible;
        if (lastOutputs == null || !lastOutputs.SequenceEqual(rows) || lastOutputsHardware != HardwarePlaybackSelected)
        {
            lastOutputs = rows;
            lastOutputsHardware = HardwarePlaybackSelected;
            Outputs.Children.Clear();
            if (rows.Length == 0 && !HardwarePlaybackSelected)
                Outputs.Children.Add(Text("未检测到设备 · 连接后在这里显示每台设备的输出"));
            foreach (var row in rows) Outputs.Children.Add(BuildOutputRow(row, rows.Length == 1));
        }
        RenderPreviewBanner();
    }

    StackPanel BuildOutputRow(OutputRow row, bool single)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 8, 0) };
        var thumbnail = new Border
        {
            Width = 54, Height = 38, Margin = new Thickness(0, 0, 6, 0),
            Background = ColorBrush("#131C2D")
        };
        if (row.ResourceId != null && state.Resources.TryGetValue(row.ResourceId, out var resource))
            thumbnail.Child = Thumbnail(resource, row.Canvas, 54, 38, row.Fit, liveClock: row.Online && row.Running);
        panel.Children.Add(thumbnail);

        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 155 };
        var name = Text(single ? (hardwareApplySerial != null ? "模拟预览" : "正在显示") : row.Name, 13);
        name.Margin = new Thickness(0);
        name.FontWeight = FontWeights.SemiBold;
        labels.Children.Add(name);
        var description = !row.Online ? "未连接" : row.ResourceId == null ? "未选择内容" :
            !row.Running ? "已停止输出" : row.Total > 0
                ? $"{row.ResourceName} · {row.Position}/{row.Total}" : row.ResourceName;
        var status = Text(description, 12);
        status.Margin = new Thickness(0);
        status.ToolTip = row.ListName == null ? description : $"轮播列表「{row.ListName}」";
        labels.Children.Add(status);
        panel.Children.Add(labels);

        var device = state.Devices[row.DeviceId];
        foreach (var direction in new[] { -1, 1 })
        {
            var button = Action(OutputStepGlyph(direction), () => StepHomePlaylist(device, direction));
            var action = direction < 0 ? "上一项" : "下一项";
            AutomationProperties.SetName(button, device.Name + action);
            button.IsEnabled = row.Online && row.Total > 0;
            ToolTipService.SetShowOnDisabled(button, true);
            button.ToolTip = button.IsEnabled ? action : "设备离线或没有在轮播";
            panel.Children.Add(button);
        }
        var stop = Action(row.Running ? "停止" : "恢复", () => ToggleDeviceOutput(device));
        AutomationProperties.SetName(stop, $"{stop.Content}{device.Name}输出");
        stop.IsEnabled = row.ResourceId != null;
        panel.Children.Add(stop);
        return panel;
    }

    static string OutputStepGlyph(int direction) => direction < 0 ? "‹" : "›";

    void RenderPreviewBanner()
    {
        var preview = state.Preview;
        if (lastPreviewToken != preview?.Token)
        {
            lastPreviewToken = preview?.Token;
            PreviewBar.Children.Clear();
            previewStatus = null;
            if (preview != null)
            {
                previewStatus = Text("");
                PreviewBar.Children.Add(previewStatus);
                PreviewBar.Children.Add(Action("结束并恢复", () => state.EndPreview(preview.Token)));
                PreviewBar.Children.Add(Action("保留并使用", () => UsePreview(preview)));
            }
        }
        if (preview != null && previewStatus != null)
            previewStatus.Text = $"{DeviceName(preview.DeviceId)} · 临时预览 {Math.Ceiling((preview.EndsAt - state.Clock).TotalSeconds)} 秒";
    }
}
