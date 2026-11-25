using System.Windows;
using DeskDisplay.App.ViewModels;
using Serilog;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.App.Views;

/// <summary>Uses a fresh saved snapshot. Never replaces the editor's project or its undo history.</summary>
public static class SavedThemeUse
{
    public static async Task UseDirectAsync(MainViewModel main, ThemeCard card, Window? owner)
    {
        try { await ApplyDirectAsync(main,card); }
        catch(Exception ex) { Log.Warning(ex,"Cannot directly use saved theme {Id}",card.Project.Id); MessageBox.Show(owner,"无法使用主题："+ex.Message,"使用主题"); }
    }
    public static async Task ApplyDirectAsync(MainViewModel main, ThemeCard card, ManagedDeviceCard? target = null)
    {
        if(main.Creator is not { } creator || main.Devices.Managed is not {PreviewFactory:not null} devices)
            throw new InvalidOperationException("请先在设备管理中启用并配置目标设备。");
        if(devices.IsBusy)throw new InvalidOperationException("设备正在处理其他请求，请稍后再试。");
        target ??= devices.SelectConnectedD92(main.Usb?.SelectedSerial);
        var source=await creator.ReadSavedProjectAsync(card.Project.Id);
        var revision=target.AcceptedOutputRevision;
        await devices.ApplySourceAsync(source,[target.DeviceId],LayoutAdaptation.Contain,replaceOwned:true);
        creator.ReportDevicePreview("使用主题 · "+target.Alias+" · "+target.Message);
        if(target.AcceptedOutputRevision==revision)throw new InvalidOperationException(target.Message);
    }
    public static async Task OpenAsync(MainViewModel main, ThemeCard card, Window? owner)
    {
        CreatorDevicePreviewViewModel? preview = null;
        try
        {
            if (main.Creator is not { } creator || main.Devices.Managed is not { PreviewFactory: not null } devices)
                throw new InvalidOperationException("请先在设备管理中启用并配置目标设备。");
            var source = await creator.ReadSavedProjectAsync(card.Project.Id);
            preview = new(source, devices, Log.Logger);
            var dialog = new CreatorDevicePreview(preview); if (owner is not null) dialog.Owner = owner;
            dialog.ShowDialog();
        }
        catch (Exception ex) { Log.Warning(ex, "Cannot use saved theme {Id}", card.Project.Id); MessageBox.Show(owner, "无法使用主题：" + ex.Message, "使用主题"); }
        finally { if (preview is not null) await preview.DisposeAsync(); }
    }
}
