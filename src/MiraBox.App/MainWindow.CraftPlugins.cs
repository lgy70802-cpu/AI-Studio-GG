using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly CraftPluginStore craftPlugins = new(Path.Combine(DataRoot, "Plugins"));
    bool importingCraftPlugin;

    void RenderCraftPlugins()
    {
        var importButton = HomeButton(importingCraftPlugin ? "正在导入…" : "导入 Woodland Clock", PickWoodlandPlugin, "PrimaryButton");
        importButton.IsEnabled = !importingCraftPlugin;
        Body.Children.Add(HomeHeader(HomeText("Craft 插件", 20, bold: true), importButton));
        Body.Children.Add(HomeText("导入后可实时预览，或将 Woodland Clock 应用到首页当前设备。", 13, muted: true));
        try
        {
            var entries = craftPlugins.Entries;
            if (entries.Count == 0) Body.Children.Add(HomeCard(HomeText("还没有导入 Craft 插件", 15, muted: true)));
            foreach (var entry in entries)
            {
                var panel = new StackPanel();
                panel.Children.Add(HomeHeader(HomeText(entry.Name, 18, bold: true),
                    PlaylistBadge("可运行 · 本地时钟", "#E5F5FA", "#0F7FCF")));
                panel.Children.Add(HomeText($"版本 {entry.Version} · {entry.Author} · 默认尺寸 {entry.Width} × {entry.Height}", 13, muted: true));
                panel.Children.Add(HomeText(entry.Description, 13, muted: true));
                panel.Children.Add(HomeText($"{entry.Files} 个文件 · {entry.Bytes / 1024d / 1024:F1} MB · 已保存独立副本", 12, muted: true));
                var hour12 = Preference("使用 12 小时制", woodlandHour12, value => woodlandHour12 = value);
                panel.Children.Add(hour12);
                panel.Children.Add(HomeText("小时、分钟、日期与星期实时更新。D69 保留原比例完整显示，会有上下留边。", 12, muted: true));
                var actions = new WrapPanel();
                actions.Children.Add(HomeButton("实时预览", () => {
                    try { PreviewWoodland(hour12.IsChecked == true); }
                    catch (Exception ex) { Notice.Text = "Woodland Clock 预览失败：" + ex.Message; }
                }, "PillButton"));
                actions.Children.Add(HomeButton("应用到当前设备", () => _ = UseWoodland(hour12.IsChecked == true), "PrimaryButton"));
                panel.Children.Add(actions);
                var filesButton = HomeButton("查看插件文件", () => {
                    var path = craftPlugins.PackagePath(entry);
                    if (!Directory.Exists(path)) { Notice.Text = "插件副本已丢失。"; return; }
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }, "PillButton");
                filesButton.HorizontalAlignment = HorizontalAlignment.Left;
                filesButton.Margin = new Thickness(0, 8, 0, 0);
                panel.Children.Add(filesButton);
                var card = HomeCard(panel);
                card.Margin = new Thickness(0, 12, 0, 16);
                Body.Children.Add(card);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { Body.Children.Add(HomeText("无法读取插件库：" + ex.Message, 13)); }
        Body.Children.Add(HomeText("内置组件", 18, bold: true));
    }

    async void PickWoodlandPlugin()
    {
        if (importingCraftPlugin) return;
        var picker = new OpenFolderDialog { Title = "选择 Woodland Clock 或 .mPanelPlugin 文件夹", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            await ImportWoodlandPlugin(picker.FolderName);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { Notice.Text = "导入失败：" + ex.Message; }
    }

    async Task<CraftPluginImport> ImportWoodlandPlugin(string path)
    {
        if (importingCraftPlugin) throw new InvalidOperationException("插件正在导入，请稍候。");
        importingCraftPlugin = true;
        if (page == "插件中心") Render();
        try
        {
            var result = await Task.Run(() => craftPlugins.ImportWoodland(path, DateTimeOffset.UtcNow));
            Notice.Text = result.AlreadyImported ? "Woodland Clock 已导入，已跳过重复内容。" : "Woodland Clock 导入成功，可实时预览或应用到当前设备。";
            return result;
        }
        finally
        {
            importingCraftPlugin = false;
            if (page == "插件中心") Render();
        }
    }
}
