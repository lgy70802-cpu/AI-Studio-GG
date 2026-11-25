using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MiraBox.App;

internal sealed class StudioRecoveryWindow : Window
{
    public StudioRecoveryWindow(string root, string reason)
    {
        Title = "MiraBox Studio · 数据恢复"; Width = 660; Height = 400; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "原数据已保留，请选择完整备份包恢复。", FontSize = 20, Margin = new Thickness(0, 0, 0, 16) });
        var status = new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(status);
        var choose = new Button { Content = "选择并校验备份包", Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
        var restore = new Button { Content = "恢复这份备份（输出与定时先暂停）", Padding = new Thickness(12), IsEnabled = false };
        panel.Children.Add(choose); panel.Children.Add(restore); Content = panel;
        var packages = new StudioPackageStore(root, Path.Combine(AppContext.BaseDirectory, "Web"));
        StudioPackageStore.CheckedPackage? inspected = null;
        choose.Click += async (_, _) =>
        {
            var dialog = new OpenFileDialog { Filter = "MiraBox 完整备份|*.mirabox" }; if (dialog.ShowDialog(this) != true) return;
            choose.IsEnabled = restore.IsEnabled = false;
            try
            {
                if (inspected != null) Directory.Delete(inspected.Directory, true);
                inspected = await Task.Run(() => packages.Inspect(dialog.FileName));
                if (inspected.Manifest["scope"]?.GetValue<string>() != "workspace") throw new InvalidDataException("请选择完整备份包，单个工程包不能恢复全部状态。");
                status.Text = $"校验通过：{inspected.Snapshot["themes"]!.AsArray().Count} 项素材、{inspected.Snapshot["playlists"]!.AsArray().Count} 个列表。恢复会替换索引及设置，现有素材不删除。";
                restore.IsEnabled = true;
            }
            catch (Exception e) { status.Text = e.Message; }
            finally { choose.IsEnabled = true; }
        };
        restore.Click += async (_, _) =>
        {
            if (inspected == null) return; choose.IsEnabled = restore.IsEnabled = false;
            try
            {
                await Task.Run(() => packages.Import(inspected, true));
                var statePath = Path.Combine(root, "desktop-state.json");
                if (File.Exists(statePath))
                {
                    var state = JsonNode.Parse(File.ReadAllText(statePath))!;
                    foreach (var device in state["Devices"]!.AsArray()) device!["Playing"] = false;
                    foreach (var session in state["PlaylistSessions"]?.AsArray() ?? new JsonArray()) session!["Running"] = false;
                    StudioAtomicFile.Write(statePath, state.ToJsonString());
                }
                var schedules = Path.Combine(root, "schedule-state.json");
                if (File.Exists(schedules)) { var data = JsonNode.Parse(File.ReadAllText(schedules))!; data["Enabled"] = false; StudioAtomicFile.Write(schedules, data.ToJsonString()); }
                foreach (var name in new[] { "desktop-state.json", "schedule-state.json" }) if (File.Exists(Path.Combine(root, name))) File.Copy(Path.Combine(root, name), Path.Combine(root, name + ".previous"), true);
                var window = new StudioWindow(); Application.Current.MainWindow = window; window.Show(); Close();
            }
            catch (Exception e) { status.Text = e.Message; choose.IsEnabled = true; }
        };
        Closed += (_, _) => { if (inspected != null && Directory.Exists(inspected.Directory)) Directory.Delete(inspected.Directory, true); };
    }
}
