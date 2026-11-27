using System.ComponentModel;
using System.IO;
using System.Windows;
using MiraBox.Core;
using Forms = System.Windows.Forms;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly StartupPreferenceStore preferenceStore = new(Path.Combine(DataRoot, "startup-preferences.json"));
    readonly WindowsStartupRegistration startupRegistration = new(DataRoot);
    StartupPreferences preferences = new();
    Forms.NotifyIcon? trayIcon;
    bool exitRequested;
    bool closePrepared;
    bool restoringStartup;
    HardwareOutputSnapshot? pendingHardwareRestore;
    TimeSpan nextHardwareRestoreCheck;
    const string HardwareOfflineSaved = "设备离线，已保存修改，重新连接后自动显示。";

    void LoadStartupPreferences()
    {
        try
        {
            preferences = preferenceStore.Read();
            preferences = preferences with { Start = startupRegistration.IsEnabled() };
        }
        catch (Exception ex)
        {
            Notice.Text = "无法读取启动偏好：" + ex.Message;
        }
    }

    void UpdatePreferences(StartupPreferences updated)
    {
        var previous = preferences;
        try
        {
            preferenceStore.Save(updated);
            if (updated.D92VideoMode != previous.D92VideoMode && hardwareApplySerial is { } serial && HardwareModel == "D92")
            {
                var deviceOptions = hardwareOrientations.GetOptions(serial);
                hardwareOrientations.SaveOptions(serial, deviceOptions with { VideoMode = updated.D92VideoMode.ToString() });
            }
            if (updated.Start != previous.Start)
            {
                try { startupRegistration.Set(updated.Start, Environment.ProcessPath ?? throw new InvalidOperationException("无法定位应用程序。")); }
                catch
                {
                    preferenceStore.Save(previous);
                    throw;
                }
            }
            preferences = updated;
            if (!updated.Restore)
            {
                foreach (var pendingSerial in HardwareSessionSerials())
                    InHardwareSession(pendingSerial, () =>
                    {
                        if (pendingHardwareRestore == null) return;
                        hardwareStatus = "自动恢复已关闭，可手动恢复输出";
                        RenderHardwareStatus();
                    });
            }
            Notice.Text = "设置已保存。";
        }
        catch (Exception ex)
        {
            Render();
            Notice.Text = "保存设置失败：" + ex.Message;
        }
    }

    bool StartupResourceAvailable(Resource resource)
    {
        if (state.Themes.GetValueOrDefault(resource.Id)?.CraftPlugin is { } craft && !WoodlandFilesAvailable(craft)) return false;
        var entry = library.Entries.FirstOrDefault(item => item.Resource.Id == resource.Id);
        if (entry is not { Resource.Local: true }) return false;
        if (state.Themes.GetValueOrDefault(resource.Id)?.BackgroundFile is { } background && !File.Exists(background))
            return false;
        return library.MediaPath(entry) is not { } file || File.Exists(file);
    }

    void ConfigureWindowLifecycle()
    {
        StateChanged += (_, _) => _ = UpdateEditorVideoVisibility();
        IsVisibleChanged += (_, _) => _ = UpdateEditorVideoVisibility();
        Closing += HandleWindowClosing;
        Closing += StopHardwareBeforeClose;
        if (Application.Current is { } app) app.SessionEnding += OnWindowsSessionEnding;
        Closed += (_, _) =>
        {
            themeEditorBrowser?.Dispose(); themeEditorBrowser = null;
            timer.Stop();
            onlineLifetime.Cancel();
            if (Application.Current is { } app) app.SessionEnding -= OnWindowsSessionEnding;
            if (trayIcon is { } icon)
            {
                icon.Visible = false;
                icon.ContextMenuStrip?.Dispose();
                icon.Dispose();
                trayIcon = null;
            }
        };
    }

    void OnWindowsSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        exitRequested = true;
        try { SaveWorkspace(); }
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("Windows 退出时保存工作区失败：{0}", ex.Message); }
    }

    void HandleWindowClosing(object? sender, CancelEventArgs e)
    {
        if (closePrepared) return;
        if (preferences.Tray && !exitRequested)
        {
            e.Cancel = true;
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    ShowTrayIcon();
                    Hide();
                }
                catch (Exception ex) { Notice.Text = "无法显示托盘图标：" + ex.Message; }
            });
            return;
        }
        if (importing || !LeaveEditor())
        {
            if (importing) Notice.Text = "正在导入素材，请等待完成。";
            exitRequested = false;
            e.Cancel = true;
            return;
        }
        try
        {
            SaveWorkspace();
            closePrepared = true;
            timer.Stop();
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            exitRequested = false;
            Notice.Text = "保存失败：" + ex.Message;
        }
    }

    void ShowTrayIcon()
    {
        if (trayIcon == null)
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开 D92 Studio", null, (_, _) => _ = Dispatcher.InvokeAsync(OpenFromTray));
            menu.Items.Add("退出", null, (_, _) => _ = Dispatcher.InvokeAsync(ExitFromTray));
            trayIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "D92 Studio",
                ContextMenuStrip = menu
            };
            trayIcon.DoubleClick += (_, _) => _ = Dispatcher.InvokeAsync(OpenFromTray);
        }
        trayIcon.Visible = true;
    }

    void OpenFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    void ExitFromTray()
    {
        OpenFromTray();
        exitRequested = true;
        Close();
    }

    void ResumeSavedHardwareOnLoad(HardwareOutputSnapshot saved)
    {
        InHardwareSession(saved.Serial, () =>
        {
            pendingHardwareRestore = saved;
            hardwareContent = ResourceName(saved.VideoResourceId ?? saved.BaseResourceId);
            if (!preferences.Restore) hardwareStatus = "自动恢复已关闭，可手动恢复输出";
        });
        async void Resume(object sender, RoutedEventArgs e)
        {
            Loaded -= Resume;
            await RestoreSavedHardware(saved);
        }
        Loaded += Resume;
    }

    void PollPendingHardwareRestore(TimeSpan now)
    {
        if (!IsLoaded || !preferences.Restore || pendingHardwareRestore is not { } saved || restoringStartup ||
            hardwareOutput != null || now < nextHardwareRestoreCheck) return;
        nextHardwareRestoreCheck = now + TimeSpan.FromSeconds(1);
        _ = RestoreSavedHardware(saved);
    }

    async Task RestoreSavedHardware(HardwareOutputSnapshot saved)
    {
        if (saved.Serial != hardwareApplySerial && !hardwareSessions.ContainsKey(saved.Serial) &&
            !hardwareDeviceNames.ContainsKey(saved.Serial)) return;
        var prepared = InHardwareSession(saved.Serial, () =>
        {
            if (restoringStartup || closePrepared || !preferences.Restore || hardwareOutput != null) return false;
            if (Available(saved.BaseResourceId, IsHardwareStill) == null && Available(saved.VideoResourceId, IsHardwareVideo) == null)
            {
                pendingHardwareRestore = null;
                return false;
            }
            pendingHardwareRestore = saved;
            restoringStartup = true;
            return true;
        });
        if (!prepared) return;
        try
        {
            var online = await Task.Run(() => HardwarePresent(saved.Serial));
            if (online && saved.BaseResourceId is { } baseId && state.Themes.GetValueOrDefault(baseId)?.CraftPlugin is { } craft)
            {
                var geometry = D92StaticImage.Geometry(hardwareOrientations.Get(saved.Serial), HardwareModelFor(saved.Serial));
                var runtime = Woodland(craft, new(geometry.Width, geometry.Height));
                await runtime.Ready.WaitAsync(TimeSpan.FromSeconds(30));
                if (runtime.Error != null) throw new InvalidOperationException(runtime.Error);
            }
            InHardwareSession(saved.Serial, () =>
            {
                if (closePrepared || !ReferenceEquals(pendingHardwareRestore, saved) || !preferences.Restore ||
                    hardwareOutput != null) return;
                var still = Available(saved.BaseResourceId, IsHardwareStill);
                var video = Available(saved.VideoResourceId, IsHardwareVideo);
                if (!online)
                {
                    hardwareContent = video?.Name ?? still?.Name ?? "内容已删除";
                    hardwareStatus = $"等待同一 {HardwareModel} 重连 · 将恢复上次内容";
                    RenderHardwareStatus();
                    return;
                }
                pendingHardwareRestore = null;
                if (still != null) UseHardwareStill(still, recordUse: false);
                if (video != null) PlayHardwareVideo(video, recordUse: false);
            });
        }
        catch (Exception ex) { Notice.Text = "无法恢复 D92 实机输出：" + ex.Message; }
        finally { InHardwareSession(saved.Serial, () => restoringStartup = false); }

        Resource? Available(string? id, Func<Resource, bool> supported) =>
            id != null && state.Resources.TryGetValue(id, out var resource) && supported(resource) &&
            StartupResourceAvailable(resource) ? resource : null;
    }
}
