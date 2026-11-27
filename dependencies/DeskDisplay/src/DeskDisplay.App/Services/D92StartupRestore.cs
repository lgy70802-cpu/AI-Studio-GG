using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Devices;

namespace DeskDisplay.App.Services;

public static class D92StartupRestore
{
    public static async Task RestoreAsync(MainViewModel main,ManagedDevicesViewModel managed,D92ResumeContent content)
    {
        var intent=main.Output.CompletedTransitions;
        var matches=managed.Cards.Where(c=>c.Connected && c.Device.Transport==DeviceTransport.UsbHid && c.Device.Identity.SerialNumber==content.Serial).ToArray();
        if(matches.Length!=1 || !matches[0].Confirmed || matches[0].Device.Identity.Confidence!=IdentityConfidence.VerifiedStable)
            throw new InvalidOperationException("设备未连接或尚未确认，未自动输出。");
        var card=matches[0];ThemeProject source;
        if(content.Kind=="Video")
        {
            if(!System.IO.File.Exists(content.Path))throw new InvalidOperationException("上次播放的视频文件已丢失，未自动输出。");
            if(main.Usb is not {CanEdit:true} videoOutput)throw new InvalidOperationException("设备正在输出其他内容，未自动替换。");
            if(main.Output.CompletedTransitions!=intent)throw new InvalidOperationException("已取消恢复，保留用户的新选择。");
            videoOutput.SelectedSerial=content.Serial;videoOutput.Width=card.Width;videoOutput.Height=card.Height;videoOutput.Rotation=card.Rotation;
            if(!await videoOutput.PlayVideoAsync(content.Path!))throw new InvalidOperationException(videoOutput.Status);
            if(content.Paused)await videoOutput.SetVideoPausedAsync(true);
            return;
        }
        if(content.Kind=="MixedPlaylist")
        {await main.MixedPlaylist.InitializeAsync();main.MixedPlaylist.Target=card;await main.MixedPlaylist.StartAsync(content.LastAssetId,content.Paused);if(!main.MixedPlaylist.IsRunning)throw new InvalidOperationException(main.MixedPlaylist.Status);return;}
        if(content.Kind is "ThemePlaylist" or "WallpaperPlaylist")
        {
            if(main.Output.IsBusy && !main.Output.OwnsTransition)throw new InvalidOperationException("正在处理其他输出请求。");
            if(main.Usb is not {CanEdit:true} usb)throw new InvalidOperationException("当前轮播仅支持一个 USB 目标，请先停止已有 USB 输出。");
            usb.SelectedSerial=content.Serial;usb.Width=card.Width;usb.Height=card.Height;usb.Rotation=card.Rotation;
            if(content.Kind=="ThemePlaylist")
            {
                if(main.Creator is not { } creator)throw new InvalidOperationException("主题资源尚未就绪。");
                await creator.LoadThemePlaylistAsync();
                if(main.Output.IsBusy && !main.Output.OwnsTransition || main.Output.CompletedTransitions!=intent)
                    throw new InvalidOperationException("已取消启动恢复，保留用户新选择的内容。");
                creator.ThemePlaylistTarget="D92 USB";
                await creator.StartThemePlaylistAsync(content.ThemeId,content.Paused);
                if(!creator.IsThemeRotating)throw new InvalidOperationException(creator.ThemePlaylistStatus);
            }
            else
            {
                if(main.Library is not { } library)throw new InvalidOperationException("资源库尚未就绪。");
                library.RotationTarget="D92 USB";await library.StartRotationAsync(content.LastAssetId,content.Paused);
                if(!library.IsRotating)throw new InvalidOperationException(library.Status);
            }
            return;
        }
        if(content.Kind=="Theme" && content.ThemeId is { } id)source=await main.Creator!.ReadSavedProjectAsync(id);
        else
        {
            var size=managed.GetProfile(card).SurfaceSize;
            source=new(){Name=content.Kind=="Image" ? System.IO.Path.GetFileNameWithoutExtension(content.Path)??"壁纸" : content.Kind=="Stock"?"股票插件":"复古相机时钟",Orientation=size.Width>=size.Height?ThemeOrientation.Landscape:ThemeOrientation.Portrait};
            source.ActiveLayout.Width=size.Width;source.ActiveLayout.Height=size.Height;
            var kind=content.Kind switch {"Image"=>ThemeElementKind.Image,"Stock"=>ThemeElementKind.StockPlugin,"Camera"=>ThemeElementKind.CameraClock,_=>throw new InvalidOperationException("此内容尚不支持启动恢复。")};
            if(kind==ThemeElementKind.Image && !System.IO.File.Exists(content.Path))throw new InvalidOperationException("原图片文件已丢失。");
            source.ActiveLayout.Layers.Add(new(){Kind=kind,Path=content.Path??"",Width=size.Width,Height=size.Height,Fill=content.ImageMode});
        }
        var revision=card.AcceptedOutputRevision;
        if(main.Output.IsBusy && !main.Output.OwnsTransition || main.Output.CompletedTransitions!=intent)throw new InvalidOperationException("已取消启动恢复，保留用户新选择的内容。");
        await managed.ApplySourceAsync(source,[card.DeviceId],DeskDisplay.Core.Themes.LayoutAdaptation.Contain,false,true);
        if(card.AcceptedOutputRevision==revision)throw new InvalidOperationException(card.Message);
    }
}
