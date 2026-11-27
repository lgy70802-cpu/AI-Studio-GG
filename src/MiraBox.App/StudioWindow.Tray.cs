using System.IO;
using System.Text.Json;
using System.Windows;
namespace MiraBox.App;
public sealed partial class StudioWindow
{
    bool closeToTray;
    bool exitRequested;
    System.Windows.Forms.NotifyIcon? trayIcon;
    Dictionary<string, string> deviceNames = new();
    sealed record WindowPreferences(bool CloseToTray, Dictionary<string, string>? DeviceNames);
    void LoadWindowPreferences()
    {
        var path = Path.Combine(dataRoot, "window-preferences.json");
        if (!File.Exists(path)) return;
        try
        {
            var preferences = JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllText(path));
            closeToTray = preferences?.CloseToTray ?? false;
            deviceNames = preferences?.DeviceNames ?? new();
        }
        catch (Exception ex) { Log(new { subsystem = "window-preferences", error = ex.Message }); }
    }
    void SaveWindowPreferences()
    {
        var path = Path.Combine(dataRoot, "window-preferences.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new WindowPreferences(closeToTray, deviceNames)));
        File.Move(temporary, path, true);
    }
    void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (trayIcon != null) trayIcon.Visible = false;
    }
    void HideToTray()
    {
        if (trayIcon == null)
        {
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "小屏宝PixBuddy",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("打开 小屏宝PixBuddy", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
            menu.Items.Add("退出软件", null, (_, _) => Dispatcher.Invoke(() => { exitRequested = true; Close(); }));
            trayIcon.ContextMenuStrip = menu;
            trayIcon.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) Dispatcher.Invoke(ShowFromTray); };
        }
        trayIcon.Visible = true;
        Hide();
    }
}
