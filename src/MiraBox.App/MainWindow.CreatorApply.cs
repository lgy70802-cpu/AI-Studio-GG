using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    void SaveEditorDraft(bool use)
    {
        try
        {
            SaveDraft();
            Render();
            Notice.Text = "主题已保存。";
            if (use && draft != null) ChooseThemeOutput(state.Resources[draft.ResourceId]);
        }
        catch (Exception ex) when (ex is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Notice.Text = "保存主题失败：" + ex.Message;
        }
    }

    void ChooseThemeOutput(Resource theme)
    {
        var serials = KnownHardwareSerials();
        if (serials.Length == 0)
        {
            Apply(theme);
            return;
        }
        if (serials.Length == 1 && HardwarePresent(serials[0]) &&
            InHardwareSession(serials[0], () => HardwareApplyIssue(theme)) == null)
        {
            SetHardwareApplySerial(serials[0]);
            ApplyHardwareResource(theme);
            return;
        }
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("选择使用主题的设备", panel, 600, 440);
        panel.Children.Add(HomeText(theme.Name, 18, bold: true));
        foreach (var serial in serials)
        {
            var issue = InHardwareSession(serial, () => HardwareApplyIssue(theme));
            var canvas = InHardwareSession(serial, () => HardwareCanvas);
            var button = HomeButton($"{HardwareDeviceName(serial)} · {serial}\n{SpecText(canvas)} · {issue ?? "适配，可应用"}", () =>
            {
                dialog.Close();
                SetHardwareApplySerial(serial);
                ApplyHardwareResource(theme);
            }, "PillButton");
            button.Height = double.NaN;
            button.Padding = new Thickness(14, 8, 14, 8);
            button.IsEnabled = issue == null;
            button.ToolTip = issue;
            panel.Children.Add(button);
        }
        panel.Children.Add(HomeButton("取消", dialog.Close, "PillButton"));
        dialog.ShowDialog();
    }
}
