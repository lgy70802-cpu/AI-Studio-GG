using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record HardwareOverviewRow(string Serial, Button Button, TextBlock Content, TextBlock Status);
    WrapPanel? hardwareOverview;
    readonly List<HardwareOverviewRow> hardwareOverviewRows = [];

    string SelectedHardwareState()
    {
        if (hardwareOutput?.IsStopping == true) return "正在停止";
        if (hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal)) return "离线 · 等待重连";
        if (hardwareOutput != null)
        {
            if (hardwareStatus.StartsWith("输出失败", StringComparison.Ordinal) || hardwareStatus.Contains("Craft"))
                return hardwareStatus;
            if (hardwareStatus == "正在输出") return HardwareVideoPaused ? "已连接 · 视频暂停" : "已连接 · 发送中";
            return hardwareStatus.Length == 0 ? "正在连接" : hardwareStatus;
        }
        if (stoppedHardwareOutput != null) return "已停止";
        if (pendingHardwareRestore != null)
            return preferences.Restore ? "等待恢复" : "已停止 · 可手动恢复";
        var online = managedHardwareDetected?.Any(device => device.SerialNumber == hardwareApplySerial);
        return online == false ? "离线" : online == true ? "已连接 · 未输出" : "等待检查连接";
    }

    string SelectedHardwareContent()
    {
        var id = hardwareVideoResourceId ?? hardwareStill?.ResourceId ??
            pendingHardwareRestore?.VideoResourceId ?? pendingHardwareRestore?.BaseResourceId ??
            stoppedHardwareOutput?.VideoResourceId ?? stoppedHardwareOutput?.BaseResourceId;
        return id != null ? ResourceName(id) : string.IsNullOrWhiteSpace(hardwareContent) ? "尚未应用内容" : hardwareContent;
    }

    FrameworkElement BuildHardwareOverview()
    {
        hardwareOverview = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        hardwareOverviewRows.Clear();
        AutomationProperties.SetName(hardwareOverview, "设备输出概览");
        UpdateHardwareOverview();
        return hardwareOverview;
    }

    void UpdateHardwareOverview()
    {
        if (page != "首页" || hardwareOverview is not { } panel || backgroundHardwareContext) return;
        var serials = KnownHardwareSerials();
        panel.Visibility = serials.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (!hardwareOverviewRows.Select(row => row.Serial).SequenceEqual(serials))
        {
            panel.Children.Clear();
            hardwareOverviewRows.Clear();
            foreach (var serial in serials)
            {
                var content = HomeText("", 12, muted: true);
                content.TextWrapping = TextWrapping.NoWrap;
                content.TextTrimming = TextTrimming.CharacterEllipsis;
                var status = HomeText("", 12);
                var lines = new StackPanel();
                lines.Children.Add(HomeText($"{HardwareDeviceName(serial)} · {serial[^Math.Min(6, serial.Length)..]}", 13, bold: true));
                lines.Children.Add(content);
                lines.Children.Add(status);
                var button = HomeButton("", () => SetHardwareApplySerial(serial), "PillButton");
                button.Content = lines;
                button.Width = 240;
                button.Height = double.NaN;
                button.MinHeight = 78;
                button.Padding = new Thickness(16, 8, 16, 8);
                button.Margin = new Thickness(0, 0, 8, 6);
                AutomationProperties.SetName(button, "切换首页设备：" + serial);
                panel.Children.Add(button);
                hardwareOverviewRows.Add(new(serial, button, content, status));
            }
        }
        foreach (var row in hardwareOverviewRows)
        {
            var selected = row.Serial == hardwareApplySerial;
            var view = InHardwareSession(row.Serial, () => (Content: SelectedHardwareContent(), Status: SelectedHardwareState()));
            row.Content.Text = view.Content;
            row.Status.Text = (selected ? "当前设备 · " : "") + view.Status;
            row.Button.SetResourceReference(Control.BackgroundProperty, selected ? "HomePreviewBrush" : "SurfaceBrush");
            row.Button.ToolTip = $"{HardwareDeviceName(row.Serial)} · {row.Serial}\n{view.Content}\n{view.Status}，点击切换，其他设备继续输出";
        }
    }

    FrameworkElement BuildHardwareOperationTarget()
    {
        var label = HomeText($"操作设备：{HardwareTargetLabel} · {SpecText(HardwareCanvas)}", 13, bold: true);
        AutomationProperties.SetName(label, "当前操作设备");
        label.ToolTip = "应用、预览和停止仅作用于这台设备，其他设备继续输出";
        return label;
    }
}
