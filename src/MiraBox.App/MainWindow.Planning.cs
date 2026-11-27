using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record PluginConfiguration(bool Enabled = true, bool Option = false, string Symbols = "");

    readonly PluginConfiguration[] pluginConfigurations = [new(), new()];

    Grid PlanningGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new());
        grid.ColumnDefinitions.Add(new());
        Body.Children.Add(grid);
        return grid;
    }

    StackPanel PlanningPanel(Grid grid, int column, int row, string title)
    {
        while (grid.RowDefinitions.Count <= row)
            grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var panel = new StackPanel();
        panel.Children.Add(Text(title, 18));
        var card = new Border
        {
            Child = panel,
            Background = ColorBrush("#FFFFFF"),
            BorderBrush = ColorBrush("#DDE4ED"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(4, 6, 8, 6)
        };
        Grid.SetColumn(card, column);
        Grid.SetRow(card, row);
        grid.Children.Add(card);
        return panel;
    }

    static CheckBox Preference(string label, bool value, Action<bool> change)
    {
        var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(4, 8, 4, 8) };
        check.Click += (_, _) => change(check.IsChecked == true);
        return check;
    }

    void RenderPlugins()
    {
        RenderCraftPlugins();
        Body.Children.Add(Text("管理内置组件，并从插件中心新建主题。"));
        var grid = PlanningGrid();
        AddPlugin(0, "复古相机翻页时钟", "使用本机时间。主题保存后会保留当前布局的时间制式。", "12 小时制");
        AddPlugin(1, "A 股行情", "自选行情组件。当前为配置演示。", "显示涨跌幅");

        void AddPlugin(int index, string name, string description, string optionName)
        {
            var panel = PlanningPanel(grid, index, 0, name);
            var config = pluginConfigurations[index];
            panel.Children.Add(Text(description));
            var enable = Preference("启用插件", config.Enabled, enabled =>
            {
                pluginConfigurations[index] = pluginConfigurations[index] with { Enabled = enabled };
                Notice.Text = enabled ? "插件已在演示中启用。" : "插件已在演示中停用。";
            });
            panel.Children.Add(enable);
            var option = Preference(optionName, config.Option, _ => { });
            panel.Children.Add(option);
            var symbols = new TextBox { Text = config.Symbols, Margin = new Thickness(4) };
            if (index == 1)
            {
                panel.Children.Add(Text("股票代码（如 600519, 300750, 600036）"));
                panel.Children.Add(symbols);
            }
            var actions = new WrapPanel();
            actions.Children.Add(Action("保存配置", () =>
            {
                pluginConfigurations[index] = pluginConfigurations[index] with
                {
                    Option = option.IsChecked == true,
                    Symbols = symbols.Text.Trim()
                };
                Notice.Text = "插件配置已在本次演示中保存。";
            }));
            var use = Action("在新主题中使用", () => NewTheme(flipClock: index == 0 ? new(option.IsChecked == true) : null));
            use.IsEnabled = config.Enabled;
            enable.Click += (_, _) => use.IsEnabled = enable.IsChecked == true;
            actions.Children.Add(use);
            panel.Children.Add(actions);
        }
    }

    void ExportPlan()
    {
        var snapshot = SessionStore.Capture(state, library, activeScenario);
        var payload = new
        {
            Version = "6.7-multi-device",
            snapshot.Devices,
            PhysicalDevices = KnownHardwareSerials().Select(serial => new
            {
                Serial = serial, Model = HardwareModelFor(serial), Name = HardwareDeviceName(serial),
                Rotation = hardwareOrientations.Get(serial), Options = hardwareOrientations.GetOptions(serial)
            }).ToArray(),
            Resources = snapshot.Library.Select(e => new
            {
                e.Resource.Id, e.Resource.Name, e.Resource.Layouts, e.Resource.Local,
                e.Type, e.Category, e.Subcategory, e.Favorite, e.Downloaded, e.Online
            }),
            snapshot.Playlists,
            PlayingPlaylistIds = snapshot.Playing,
            Settings = preferences
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(Text("设备、资源元数据、轮播列表与偏好。复制后可保存为 JSON 文件。"));
        var exportText = new TextBox
        {
            Text = json,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Height = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        panel.Children.Add(exportText);
        var feedback = Text("");
        panel.Children.Add(feedback);
        panel.Children.Add(Action("复制 JSON", () =>
        {
            try
            {
                Clipboard.SetText(json);
                feedback.Text = Notice.Text = "规划数据已复制。";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                feedback.Text = "剪贴板暂时不可用，已选中全文。请按 Ctrl+C 手动复制。";
                exportText.Focus();
                exportText.SelectAll();
            }
        }));
        Dialog("规划数据", panel, 720, 490).ShowDialog();
    }

    void ResetDemo()
    {
        if (catalogLoading || activeDownload != null)
        {
            Notice.Text = "请等在线资源操作完成后再重置演示。";
            return;
        }
        var backup = Path.Combine(DataRoot, $"workspace-before-reset-{Guid.NewGuid():N}.json");
        var answer = MessageBox.Show(this,
            "将重置当前工作区的设备、资源库、轮播、主题和回收站，媒体文件保留。\n\n" +
            $"会话备份将保存到：\n{backup}\n\n继续吗？",
            "重置演示？", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;
        if (!LeaveEditor()) return;

        var before = SessionStore.Capture(state, library, activeScenario);
        SessionStore.Save(backup, before);
        try
        {
            state = new Workspace(clock: IsPlaylistDemo ? demoScheduleClock : null, library: library);
            library.Replace(DemoSeedResources());
            SelectDemoScenario(0);
            SetScenario();
            SaveWorkspace();
        }
        catch
        {
            state = SessionStore.Restore(before, library);
            SelectDemoScenario(before.Scenario);
            SyncLibrary();
            RefreshTargets();
            Render();
            throw;
        }

        Array.Fill(pluginConfigurations, new PluginConfiguration());
        selectedList = null;
        lastResourcePlaylist = null;
        catalogAttempted = false;
        catalogStatus = null;
        downloadFailures.Clear();
        undo = null;
        Navigate("首页");
        Notice.Text = $"演示已重置。原会话备份：{backup}";
    }

    void SelectDemoScenario(int index)
    {
        initializing = true;
        try { Scenario.SelectedIndex = activeScenario = index; }
        finally { initializing = false; }
    }

    void RenderGuide()
    {
        Body.Children.Add(Text("设备按稳定身份管理。资源由设备共享。布局解决不同设备规格的适配。"));
        var grid = PlanningGrid();
        var map = PlanningPanel(grid, 0, 0, "页面地图");
        foreach (var name in new[] { "首页", "在线资源", "资源库", "创作中心", "轮播列表", "插件中心", "设备管理", "个人中心", "设置" })
            map.Children.Add(Action("打开 " + name, () => Navigate(name)));
        var rules = PlanningPanel(grid, 1, 0, "适配规则");
        rules.Children.Add(Text("精确尺寸优先使用专属布局。"));
        rules.Children.Add(Text("比例误差不超过 5% 时保留完整内容。"));
        rules.Children.Add(Text("比例明显不同时，壁纸可裁切，主题建议另建布局。"));
        rules.Children.Add(Text("尺寸未知时，先检查资源再应用。"));
    }
}
