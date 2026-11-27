using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record HomeDeviceChoice(string? Serial, string Label);
    Action? refreshHomeDevicePicker;

    ComboBox BuildHomeDevicePicker()
    {
        var picker = new ComboBox
        {
            Width = 290, Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            DisplayMemberPath = nameof(HomeDeviceChoice.Label),
            SelectedValuePath = nameof(HomeDeviceChoice.Serial),
            ToolTip = "切换首页设备，其他设备继续输出"
        };
        AutomationProperties.SetName(picker, "首页设备切换");
        var updating = false;
        var scanning = false;
        void Populate()
        {
            updating = true;
            try
            {
                var serials = KnownHardwareSerials();
                var choices = serials.Select(serial =>
                {
                    var online = managedHardwareDetected?.Any(device => device.SerialNumber == serial);
                    var status = online == true ? "已连接" : online == false ? "离线" : "待检查";
                    return new HomeDeviceChoice(serial, $"{HardwareDeviceName(serial)} · {serial[^Math.Min(6, serial.Length)..]} · {status}");
                }).ToList();
                if (hardwareApplySerial == null)
                    choices.Insert(0, new(null, IsPlaylistDemo ? "演示设备" : "选择设备"));
                picker.ItemsSource = choices;
                picker.SelectedIndex = choices.FindIndex(choice => choice.Serial == hardwareApplySerial);
            }
            finally { updating = false; }
        }
        async Task Refresh()
        {
            if (scanning) return;
            scanning = true;
            try
            {
                var found = await Task.Run(DiscoverHardware);
                RememberHardwareModels(found);
                managedHardwareDetected = found;
                Populate();
                UpdateHardwareOverview();
            }
            catch (Exception ex) { picker.ToolTip = "设备查找失败，可重开下拉框重试：" + ex.Message; }
            finally { scanning = false; }
        }
        Populate();
        refreshHomeDevicePicker = Populate;
        picker.SelectionChanged += (_, _) =>
        {
            if (updating || picker.SelectedItem is not HomeDeviceChoice { Serial: { } serial } ||
                serial == hardwareApplySerial) return;
            SetHardwareApplySerial(serial);
        };
        picker.Loaded += async (_, _) => { if (managedHardwareDetected == null) await Refresh(); };
        picker.DropDownOpened += async (_, _) => await Refresh();
        return picker;
    }
}
