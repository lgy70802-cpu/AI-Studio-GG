using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.App.Views;

internal static class QuickDeviceUse
{
    public static async Task ApplyAsync(MainViewModel main, object source, Window? owner, bool chooseOther = false)
    {
        try
        {
            if (main.Devices.Managed is not { } devices)
                throw new InvalidOperationException("请先在设备管理中配置目标设备。");

            var video = source is WallpaperItemViewModel { Asset.IsVideo: true };
            var candidates = devices.Cards.Where(card => card.Connected && card.Confirmed && !card.Mirrored
                && card.Device.Transport == DeviceTransport.UsbHid
                && (card.Model == "D92" || card.Model == "D69" && !video)).ToArray();
            var target = candidates.SingleOrDefault(card => card.DeviceId == main.QuickDevice?.DeviceId);
            if (chooseOther || target is null)
            {
                if (candidates.Length == 0)
                    throw new InvalidOperationException(video ? "请选择已确认且支持视频输出的 D92。" : "没有已连接且已确认的目标设备。");
                if (!chooseOther && main.QuickDevice is not null)
                    throw new InvalidOperationException("当前目标已离线或不支持此内容，请点击「选择设备」。");
                target = candidates.Length == 1 ? candidates[0] : Choose(candidates, owner);
                if (target is null) return;
                main.QuickDevice = target;
            }

            if (source is ThemeCard theme)
            {
                await SavedThemeUse.ApplyDirectAsync(main, theme, target);
                return;
            }
            if (source is not WallpaperItemViewModel wallpaper || main.Library is not { } library) return;
            if (!wallpaper.IsAvailable) throw new InvalidOperationException("素材文件已丢失，请修复后重试。");

            if (target.Model == "D92")
            {
                var usb = main.Usb ?? throw new InvalidOperationException("D92 输出尚未初始化。");
                var serial = target.Device.Identity.SerialNumber;
                if (string.IsNullOrEmpty(serial)) throw new InvalidOperationException("目标设备缺少唯一序列号。");
                if (usb.SelectedSerial != serial) usb.SelectedSerial = serial;
                if (usb.SelectedSerial != serial) throw new InvalidOperationException("当前 USB 输出正在使用另一台设备，请停止该输出后再切换。");
                await library.UseWallpaperAsync(wallpaper);
                return;
            }

            var profile = devices.GetProfile(target);
            var size = profile.SurfaceSize;
            var project = new ThemeProject { Name = wallpaper.Name[..Math.Min(160, wallpaper.Name.Length)] };
            project.Landscape = new ThemeLayout { Width = size.Width, Height = size.Height, Layers =
            [
                new ThemeLayer { Kind = ThemeElementKind.Wallpaper, Path = wallpaper.Asset.Path,
                    Width = size.Width, Height = size.Height, Fill = ImageDisplayMode.Uniform }
            ] };
            var revision = target.AcceptedOutputRevision;
            await devices.ApplySourceAsync(project, [target.DeviceId], LayoutAdaptation.Contain, replaceOwned: true);
            if (target.AcceptedOutputRevision == revision) throw new InvalidOperationException(target.Message);
            library.RecordExternalUse(wallpaper.Asset.Id, target.Alias);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Quick device output failed");
            main.Output.Report(ex.Message, true);
        }
        finally { main.RefreshNowPlaying(); }
    }

    private static ManagedDeviceCard? Choose(ManagedDeviceCard[] candidates, Window? owner)
    {
        var list = new ListBox { ItemsSource = candidates, DisplayMemberPath = nameof(ManagedDeviceCard.PreviewLabel), MinHeight = 80 };
        var apply = new Button { Content = "使用到所选设备", Margin = new Thickness(0, 10, 0, 0), IsDefault = true, IsEnabled = false };
        var panel = new StackPanel { Children = { list, apply } };
        var dialog = StudioDialog.Create("选择输出设备", panel, owner);
        apply.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        list.SelectionChanged += (_, _) => apply.IsEnabled = list.SelectedItem is not null;
        apply.Click += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true ? list.SelectedItem as ManagedDeviceCard : null;
    }
}
