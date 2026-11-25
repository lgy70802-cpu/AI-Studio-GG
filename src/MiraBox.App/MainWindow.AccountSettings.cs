using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    TextBlock? accountDeviceCount;
    StackPanel? settingsDiagnostics;
    string? settingsDiagnosticsKey;

    string[] KnownHardwareSerials() => hardwareDeviceNames.Keys
        .Concat(hardwareApplySerial is { } serial ? [serial] : Array.Empty<string>())
        .Distinct(StringComparer.Ordinal).ToArray();

    void AddServicePageHeading(string title, string subtitle, FrameworkElement? actions = null)
    {
        var heading = new StackPanel();
        heading.Children.Add(HomeText(title, 26, bold: true));
        heading.Children.Add(HomeText(subtitle, 13, muted: true));
        var row = HomeHeader(heading, actions ?? new Border());
        row.Margin = new Thickness(0, 0, 0, 20);
        Body.Children.Add(row);
    }

    StackPanel ServicePanel(string title)
    {
        var panel = new StackPanel();
        panel.Children.Add(HomeText(title, 19, bold: true));
        return panel;
    }

    void AddServiceCards(params StackPanel[] panels)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new());
        grid.ColumnDefinitions.Add(new());
        var cards = panels.Select(HomeCard).ToArray();
        foreach (var panel in panels)
            foreach (var button in panel.Children.OfType<Button>()) button.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (var card in cards) { card.VerticalAlignment = VerticalAlignment.Top; grid.Children.Add(card); }
        Body.Children.Add(grid);
        grid.SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        Arrange(Math.Max(0, Width - 290));

        void Arrange(double width)
        {
            var columns = width >= 720 ? 2 : 1;
            grid.ColumnDefinitions[1].Width = columns == 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            grid.RowDefinitions.Clear();
            for (var row = 0; row < (cards.Length + columns - 1) / columns; row++)
                grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (var index = 0; index < cards.Length; index++)
            {
                Grid.SetColumn(cards[index], index % columns);
                Grid.SetRow(cards[index], index / columns);
                cards[index].Margin = new Thickness(0, 0, index % columns == 0 && columns == 2 ? 18 : 0, 18);
            }
        }
    }

    void AddServiceNote(Panel panel, string text)
    {
        var note = HomeText(text, 13, muted: true);
        note.Margin = new Thickness(0, 10, 0, 10);
        panel.Children.Add(note);
    }

    void RenderAccount()
    {
        AddServicePageHeading("个人中心", "本地内容无需登录；在线权益与账号能力分阶段接入。");
        var guest = new StackPanel();
        var profile = new StackPanel();
        profile.Children.Add(HomeText("本地访客", 22, bold: true));
        profile.Children.Add(HomeText("你的设备和创作保留在本机", 13, muted: true));
        var avatar = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(16),
            Background = (System.Windows.Media.Brush)FindResource("BrandBrush"), Margin = new Thickness(0, 0, 14, 0) };
        avatar.Child = new TextBlock { Text = "U", FontSize = 23, Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var identity = new DockPanel();
        DockPanel.SetDock(avatar, Dock.Left);
        identity.Children.Add(avatar);
        identity.Children.Add(profile);
        guest.Children.Add(identity);
        guest.Children.Add(new Border { Height = 1, Background = (System.Windows.Media.Brush)FindResource("LineBrush"),
            Margin = new Thickness(0, 18, 0, 18) });
        guest.Children.Add(HomeText("账号连接规划", 16, bold: true));
        AddServiceNote(guest, "登录、注册和账号状态将在接入账号服务后提供。本地资源可直接使用。");
        guest.Children.Add(HomeButton("查看登录流程说明", () => MessageBox.Show(this,
            "账号接入后，可在个人中心登录或注册，并查看账号状态。设备和创作保存在本机，本地内容可直接使用。",
            "登录流程说明", MessageBoxButton.OK), "PillButton"));
        var summary = ServicePanel("本地内容概况");
        Stat("资源", $"{library.Entries.Count(entry => entry.Resource.Local)} 项");
        Stat("收藏", $"{library.Entries.Count(entry => entry.Favorite)} 项");
        accountDeviceCount = Stat("设备", "");
        summary.Children.Add(HomeButton("备份与数据管理 →", () => Navigate("设置"), "PillButton"));
        AddServiceCards(guest, summary);
        Body.Children.Add(HomeText("收藏同步、已购资源和会员权益属于后续服务规划。", 13, muted: true));
        nextManagedHardwareScan = TimeSpan.Zero;
        UpdateServicePageState();

        TextBlock Stat(string label, string value)
        {
            var number = HomeText(value, 16, bold: true);
            var row = HomeHeader(HomeText(label, 14), number);
            row.Margin = new Thickness(0, 14, 0, 14);
            summary.Children.Add(row);
            return number;
        }
    }

    void RenderSettings()
    {
        AddServicePageHeading("设置", "管理启动行为、应用偏好与本地数据。");
        var general = ServicePanel("常规");
        general.Children.Add(Preference("关闭窗口后留在托盘", preferences.Tray, value =>
            UpdatePreferences(preferences with { Tray = value })));
        general.Children.Add(Preference("登录 Windows 后启动", preferences.Start, value =>
            UpdatePreferences(preferences with { Start = value })));
        general.Children.Add(Preference("恢复上次设备内容", preferences.Restore, value =>
            UpdatePreferences(preferences with { Restore = value })));
        foreach (var toggle in general.Children.OfType<CheckBox>())
            toggle.SetResourceReference(StyleProperty, "SettingsSwitch");
        AddServiceNote(general, "恢复时分别匹配各设备。设备离线则等待原设备重新连接，内容缺失时跳过。");
        general.Children.Add(HomeText(HardwarePlaybackSelected && HardwareModel == "D92" ?
            "当前 D92 视频档位" : "D92 默认视频档位", 15, bold: true));
        const string quality = "画质优先 · 24 FPS";
        const string smooth = "流畅优先 · 30 FPS";
        var smoothSelected = HardwarePlaybackSelected && HardwareModel == "D92" ? D92VideoProfile.AutoReduceFrameRate :
            preferences.D92VideoMode == DeskDisplay.Core.Models.VideoPlaybackMode.Smooth;
        var mode = Choose([quality, smooth], smoothSelected ? smooth : quality, value =>
            UpdatePreferences(preferences with { D92VideoMode = value == smooth
                ? DeskDisplay.Core.Models.VideoPlaybackMode.Smooth : DeskDisplay.Core.Models.VideoPlaybackMode.Quality }));
        general.Children.Add(mode);
        AddServiceNote(general, "当前 D92 的视频模式在下次播放时生效。D69 视频档位在设备高级设置中调整。");
        var data = ServicePanel("数据与备份");
        AddServiceNote(data, "导出设备、资源元数据、轮播列表和应用偏好，便于保存当前配置记录。");
        data.Children.Add(HomeButton("导出规划数据 JSON", ExportPlan, "PrimaryButton"));
        AddServiceNote(data, "导入素材和创作保存在本机。");
        if (IsPlaylistDemo) data.Children.Add(HomeButton("重置演示", ResetDemo, "PillButton"));
        else data.Children.Add(HomeButton("打开演示模式", OpenPlaylistDemo, "PillButton"));
        var version = ServicePanel("版本与更新");
        AddServiceNote(version, "D92 Studio · V7");
        AddServiceNote(version, "安装包更新、校验和回滚将在版本服务接入后提供。");
        var help = ServicePanel("诊断与帮助");
        AddServiceNote(help, "按设备显示连接、输出状态与恢复建议。");
        settingsDiagnostics = new StackPanel();
        settingsDiagnosticsKey = null;
        help.Children.Add(settingsDiagnostics);
        help.Children.Add(HomeButton("查看实机发送诊断", OpenHardwareDiagnostics, "PillButton"));
        help.Children.Add(HomeButton("查看页面规划", () => Navigate("完整页面规划"), "LinkButton"));
        AddServiceCards(general, data, version, help);
        nextManagedHardwareScan = TimeSpan.Zero;
        UpdateServicePageState();
    }

    void UpdateServicePageState()
    {
        if (backgroundHardwareContext || page is not ("设置" or "个人中心")) return;
        var serials = KnownHardwareSerials();
        if (page == "个人中心" && accountDeviceCount != null)
            accountDeviceCount.Text = $"{serials.Length + (IsPlaylistDemo ? state.Devices.Count : 0)} 台";
        if (page != "设置" || settingsDiagnostics == null) return;
        var lines = serials.Select(serial =>
        {
            var online = managedHardwareDetected?.Any(device => device.SerialNumber == serial);
            var output = HardwareOutputFor(serial);
            var status = online == null ? "正在检查连接" : online == false ? "等待同一设备重连" :
                output is { IsStopping: false } ? "连接正常 · 输出中" : "连接正常 · 已停止输出";
            return (Name: HardwareDeviceName(serial), Status: status, Online: online == true);
        }).Concat(IsPlaylistDemo ? state.Devices.Values.Select(device =>
            (Name: device.Name, Status: device.Online ? "模拟设备 · 已连接" : "模拟设备 · 未连接", Online: device.Online)) : []);
        var entries = lines.ToArray();
        var key = string.Join("|", entries.Select(item => item.Name + item.Status));
        if (key == settingsDiagnosticsKey) return;
        settingsDiagnosticsKey = key;
        settingsDiagnostics.Children.Clear();
        foreach (var item in entries)
        {
            var row = HomeHeader(HomeText(item.Name, 13),
                PlaylistBadge(item.Status, item.Online ? "#E2F7EF" : "#EEF3F6", item.Online ? "#067647" : "#53677C"));
            row.Margin = new Thickness(0, 6, 0, 6);
            settingsDiagnostics.Children.Add(row);
        }
        if (entries.Length == 0) settingsDiagnostics.Children.Add(HomeText("尚未连接设备。", 13, muted: true));
    }
}
