using System.Windows;
using System.Windows.Controls;
using DeskDisplay.Display.Usb;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    Dictionary<string, string> hardwareDeviceNames = new(StringComparer.Ordinal);
    D92DeviceInfo[]? managedHardwareDetected;
    StackPanel? managedHardwareRows;
    bool scanningManagedHardware;
    TimeSpan nextManagedHardwareScan;

    string HardwareDeviceName(string serial) => hardwareDeviceNames.GetValueOrDefault(serial, HardwareModelFor(serial) + " 实机");

    FrameworkElement BuildManagedHardwareDevices()
    {
        var panel = new StackPanel();
        if (IsPlaylistDemo) panel.Children.Add(HomeText("USB 屏幕设备", 16, bold: true));
        managedHardwareRows = new StackPanel();
        panel.Children.Add(managedHardwareRows);
        RenderManagedHardwareRows();
        nextManagedHardwareScan = TimeSpan.Zero;
        if (!IsPlaylistDemo) return panel;
        var card = HomeCard(panel);
        card.Margin = new Thickness(0, 0, 0, 14);
        return card;
    }

    void PollManagedHardwareDevices(TimeSpan now)
    {
        if (!IsLoaded || page is not ("首页" or "设备管理" or "设置" or "个人中心") ||
            now < nextManagedHardwareScan || scanningManagedHardware) return;
        _ = RefreshManagedHardwareDevices();
    }

    async Task RefreshManagedHardwareDevices()
    {
        if (scanningManagedHardware) return;
        scanningManagedHardware = true;
        nextManagedHardwareScan = watch.Elapsed + TimeSpan.FromSeconds(2);
        try
        {
            var devices = await Task.Run(DiscoverHardware);
            RememberHardwareModels(devices);
            if (managedHardwareDetected == null ||
                !managedHardwareDetected.Select(device => device.SerialNumber).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(devices.Select(device => device.SerialNumber)))
            {
                managedHardwareDetected = devices;
                RenderManagedHardwareRows();
                if (page == "首页")
                {
                    refreshHomeDevicePicker?.Invoke();
                    UpdateHardwareOverview();
                }
            }
        }
        catch (Exception ex)
        {
            managedHardwareDetected = null;
            RenderManagedHardwareRows();
            if (page == "设备管理") Notice.Text = "USB 设备查找失败：" + ex.Message;
        }
        finally { scanningManagedHardware = false; }
    }

    void RenderManagedHardwareRows()
    {
        if (page != "设备管理" || managedHardwareRows is not { } rows) return;
        rows.Children.Clear();
        var serials = KnownHardwareSerials();
        if (!IsPlaylistDemo)
        {
            DrawHardwareDeviceSettings(rows, serials);
            return;
        }
        if (serials.Length == 0)
        {
            rows.Children.Add(HomeText(managedHardwareDetected == null ? "正在查找 USB 屏幕…" :
                "未发现 D92 或 D69。接上 USB 数据线后，可从「连接新设备」开始。", 13, muted: true));
            return;
        }
        foreach (var serial in serials)
        {
            var selected = serial == hardwareApplySerial;
            var online = managedHardwareDetected?.Any(device => device.SerialNumber == serial);
            var rotation = hardwareOrientations.Get(serial);
            var spec = D92StaticImage.Geometry(rotation, HardwareModelFor(serial));
            var connection = online == true ? "USB 已连接" : online == false ? "USB 离线" : "正在检查 USB";
            var details = new StackPanel();
            details.Children.Add(HomeText($"{HardwareDeviceName(serial)}{(selected ? " · 当前输出" : "")}", 15, bold: true));
            details.Children.Add(HomeText($"{serial} · {connection} · {spec.Width} × {spec.Height} · {rotation}°", 13, muted: true));
            var actions = new WrapPanel();
            actions.Children.Add(HomeButton("名称与方向", () => ShowHardwareDeviceSetup(serial), "SmallButton"));
            if (!selected)
                actions.Children.Add(HomeButton("设为当前输出", () =>
                {
                    EnsureHardwareTargetCanChange(serial);
                    SetHardwareApplySerial(serial);
                }, "SmallPrimaryButton"));
            var row = HomeHeader(details, actions);
            row.Margin = new Thickness(0, 6, 0, 4);
            rows.Children.Add(row);
        }
    }

    void EnsureHardwareTargetCanChange(string serial)
    {
        if (HardwareOutputFor(serial) is { IsStopping: true })
            throw new InvalidOperationException("此设备正在停止输出，请稍后再试。");
    }

    void RegisterHardwareDevice(string serial, string name, int rotation, bool select)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 30)
            throw new ArgumentException("设备名称须为 1–30 个字符。");
        if (select) EnsureHardwareTargetCanChange(serial);
        SaveHardwareRotation(serial, rotation);
        hardwareOrientations.SaveName(serial, name);
        hardwareDeviceNames[serial] = name;
        if (select) SetHardwareApplySerial(serial);
        else Render();
    }

    void ConnectWizard() => ShowHardwareDeviceSetup(null);

    void ShowHardwareDeviceSetup(string? configuredSerial)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var discover = configuredSerial == null;
        var dialog = Dialog(discover ? "连接新设备" : "设备名称与方向", panel, 660, 620);
        var devices = new ComboBox { Margin = new Thickness(4) };
        var template = new DataTemplate();
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.MultiBinding
        {
            StringFormat = "{0} · {1}",
            Bindings = { new System.Windows.Data.Binding("ModelKey"), new System.Windows.Data.Binding("SerialNumber") }
        });
        template.VisualTree = label;
        devices.ItemTemplate = template;
        var name = new TextBox { MaxLength = 30, Margin = new Thickness(4) };
        var rotation = new ComboBox { ItemsSource = new[] { "0° · 竖屏", "90° · 横屏", "180° · 竖屏", "270° · 横屏" },
            Margin = new Thickness(4) };
        var status = HomeText("", 13, muted: true);
        var buttons = new WrapPanel();
        var scan = new Button { Content = "重新查找" };
        var identify = new Button { Content = "发送识别画面" };
        var done = new Button { Content = discover ? "完成并设为当前输出" : "保存" };
        var closed = false;
        D92StaticOutput? identification = null;
        string? Serial() => configuredSerial ?? (devices.SelectedItem as D92DeviceInfo)?.SerialNumber;

        if (discover)
        {
            panel.Children.Add(HomeText("选择 USB 屏幕（D92 / D69）", 16, bold: true));
            panel.Children.Add(devices);
            buttons.Children.Add(scan);
            buttons.Children.Add(identify);
        }
        else panel.Children.Add(HomeText(configuredSerial!, 13, muted: true));
        panel.Children.Add(HomeText("设备名称"));
        panel.Children.Add(name);
        panel.Children.Add(HomeText("发往屏幕的像素旋转"));
        panel.Children.Add(rotation);
        var dimensions = HomeText("", 13, muted: true);
        panel.Children.Add(dimensions);
        panel.Children.Add(status);
        buttons.Children.Add(done);
        panel.Children.Add(buttons);

        devices.SelectionChanged += (_, _) => LoadSelection();
        rotation.SelectionChanged += (_, _) =>
        {
            if (rotation.SelectedIndex < 0) return;
            var model = Serial() is { } serial ? HardwareModelFor(serial) : "D92";
            var spec = D92StaticImage.Geometry(rotation.SelectedIndex * 90, model);
            dimensions.Text = $"{model} · {spec.Width} × {spec.Height}" +
                (model == "D69" ? " · 支持横竖屏图片、视频和主题" : "");
        };
        scan.Click += async (_, _) => await Scan();
        identify.Click += async (_, _) =>
        {
            if (Serial() is not { } serial || rotation.SelectedIndex < 0) return;
            if (hardwareOutput != null)
            {
                status.Text = "请先停止当前输出，再发送识别画面。";
                return;
            }
            var angle = rotation.SelectedIndex * 90;
            try
            {
                identify.IsEnabled = done.IsEnabled = devices.IsEnabled = rotation.IsEnabled = scan.IsEnabled = false;
                identification = StartHardwareOutput(serial, angle, D92StaticImage.Encode(angle, DrawHardwareMarker, HardwareModelFor(serial)),
                    "识别画面", identification: true);
                status.Text = "正在发送识别画面，请观察实体屏。";
                await hardwareOutputCompletion;
                if (!closed) status.Text = "识别输出已结束。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally
            {
                if (!closed)
                {
                    devices.IsEnabled = scan.IsEnabled = true;
                    rotation.IsEnabled = Serial() != null;
                    identify.IsEnabled = done.IsEnabled = Serial() != null;
                }
            }
        };
        done.Click += (_, _) =>
        {
            if (Serial() is not { } serial || rotation.SelectedIndex < 0) return;
            try
            {
                RegisterHardwareDevice(serial, name.Text, rotation.SelectedIndex * 90, discover);
                SaveWorkspace();
                dialog.Close();
                Notice.Text = discover ? $"已添加「{HardwareDeviceName(serial)}」并设为当前输出。" : "设备设置已保存。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        dialog.Closed += (_, _) =>
        {
            closed = true;
            if (identification != null && ReferenceEquals(hardwareOutput, identification)) identification.Stop();
        };
        if (discover) dialog.Loaded += async (_, _) => await Scan();
        LoadSelection();
        dialog.ShowDialog();

        void LoadSelection()
        {
            var serial = Serial();
            name.Text = serial == null ? "" : HardwareDeviceName(serial);
            rotation.SelectedIndex = serial == null ? -1 : hardwareOrientations.Get(serial) / 90;
            rotation.IsEnabled = serial != null;
            identify.IsEnabled = done.IsEnabled = serial != null;
            if (serial == null) dimensions.Text = "";
        }

        async Task Scan()
        {
            var previous = Serial();
            scan.IsEnabled = devices.IsEnabled = identify.IsEnabled = done.IsEnabled = false;
            status.Text = "正在查找 USB 屏幕…";
            try
            {
                var found = await Task.Run(DiscoverHardware);
                if (closed) return;
                RememberHardwareModels(found);
                devices.ItemsSource = found;
                devices.SelectedItem = found.FirstOrDefault(device => device.SerialNumber == previous) ??
                    (found.Length == 1 ? found[0] : null);
                managedHardwareDetected = found;
                RenderManagedHardwareRows();
                status.Text = found.Length == 0 ? "未发现 D92 或 D69，请检查 USB 数据线。" : $"找到 {found.Length} 台 USB 屏幕，请核对型号和序列号。";
            }
            catch (Exception ex) { if (!closed) status.Text = "设备查找失败：" + ex.Message; }
            finally
            {
                if (!closed)
                {
                    scan.IsEnabled = devices.IsEnabled = true;
                    identify.IsEnabled = done.IsEnabled = Serial() != null;
                }
            }
        }
    }
}
