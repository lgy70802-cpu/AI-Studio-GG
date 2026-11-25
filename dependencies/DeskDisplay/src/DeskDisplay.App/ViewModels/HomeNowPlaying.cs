using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed partial class MainViewModel
{
    private ManagedDeviceCard? _quickDevice;
    public ManagedDeviceCard? QuickDevice
    {
        get => _quickDevice;
        internal set => SetProperty(ref _quickDevice, value);
    }
    private ClientToolsViewModel? _clientTools;
    public ClientToolsViewModel ClientTools=>_clientTools??=new(this);
    private int _carouselSection;
    public int CarouselSection{get=>_carouselSection;set{if(value is 0 or 1 or 2)SetProperty(ref _carouselSection,value);}}
    private string _nowPlaying="本地预览 · 尚未输出到设备";
    public string NowPlaying{get=>_nowPlaying;private set=>SetProperty(ref _nowPlaying,value);}
    private bool ShowsManagedOutput=>_homeOutput is not null && Devices.Managed?.HomeOutputCard?.IsActive==true;
    public bool HomeHasVideoOutput => HomeScene is VideoFrameScene && Usb?.IsRunning == true || ShowsManagedOutput && HomeScene is ThemeScene {ContainsVideo:true};
    public string HomeVideoPerformanceText => Devices.Managed?.HomeBackgroundPaused == true ? "背景已暂停 · 时钟和组件继续更新" : Usb?.IsVideoPaused == true ? "视频已暂停" : Usb?.VideoPerformanceText ?? "";
    public AsyncCommand StopHomeOutputCommand=>_stopHome??=new(StopHomeOutputAsync,()=>!Output.IsBusy && (ShowsManagedOutput || Usb?.IsRunning==true),Serilog.Log.Logger);
    private AsyncCommand? _stopHome;
    private ActionCommand? _openOutputRecovery;
    public ActionCommand OpenOutputRecoveryCommand=>_openOutputRecovery??=new(_=>SelectedPage=Output.RecoveryAdvice.Destination switch{"Library"=>AppPage.Library,"Devices"=>AppPage.Devices,_=>AppPage.Settings});
    public ActionCommand LocateHomeContentCommand=>_locateHome??=new(_=>LocateHomeContent());
    private ActionCommand? _locateHome;
    public void RefreshNowPlaying()
    {
        OnPropertyChanged(nameof(HomeHasVideoOutput));
        OnPropertyChanged(nameof(HomeVideoPerformanceText));
        if(ShowsManagedOutput && Devices.Managed?.HomeOutputCard is { } activeCard)NowPlaying=$"{activeCard.OutputStatus} · {activeCard.Alias} · 主题 · {activeCard.AppliedTheme}";
        else if(Usb is {IsRunning:true} usb)
        {
            var name=usb.Scene switch{VideoFrameScene v=>"视频 · "+System.IO.Path.GetFileNameWithoutExtension(v.Path)+" · "+(usb.IsVideoPaused?"已暂停":"循环播放"),ThemeScene t=>"主题 · "+t.Project.Name,ImageScene i=>"壁纸 · "+(Library?.AllItems.FirstOrDefault(a=>a.Asset.Path==i.Path)?.Name??System.IO.Path.GetFileNameWithoutExtension(i.Path)),StockScene=>"股票插件",RetroCameraClockScene=>"相机时钟",_=>usb.Scene.GetType().Name};
            NowPlaying=$"{(usb.IsWaitingForDevice?"等待重连":"正在输出")} · D92 {usb.SelectedSerial} · {name}";
        }
        else NowPlaying="本地预览 · 尚未输出到设备";
        if(ActiveMixedPlaylist is {IsRunning:true} mixed)NowPlaying=(mixed.IsPaused?"混合轮播（暂停切换） · ":"混合轮播 · ")+NowPlaying;
        if(HasDevicePreview)NowPlaying=DevicePreviewLabel+" · "+NowPlaying;
        _stopHome?.NotifyCanExecuteChanged();
    }
    public async Task StopHomeOutputAsync()
    {
        if(Devices.Managed?.HomeOutputCard is { } current)ActiveMixedPlaylist?.ObserveStop(current);
        if(ShowsManagedOutput && Devices.Managed?.HomeOutputCard is { } card)await Devices.Managed.StopAsync(card);
        else if(Usb is {IsRunning:true} usb){if(Creator?.ThemePlaylistTarget=="D92 USB")Creator.StopThemePlaylist();if(Library?.RotationTarget=="D92 USB")Library.StopRotation();await usb.StopAsync();}
        RefreshNowPlaying();
    }
    private void LocateHomeContent()
    {
        var scene=HomeScene;
        if(scene is VideoFrameScene video && Library?.AllItems.FirstOrDefault(a=>a.Asset.Path==video.Path) is { } videoItem)
        {SelectedPage=AppPage.Library;Library.OpenDetailCommand.Execute(videoItem);return;}
        if(scene is ImageScene image && Library?.AllItems.FirstOrDefault(a=>a.Asset.Path==image.Path) is { } item)
        {SelectedPage=AppPage.Library;Library.OpenDetailCommand.Execute(item);return;}
        SelectedPage=AppPage.Library;if(Library is { } library){library.SelectedResourceScope="全部资源";library.SelectedResourceType="主题";library.Search=scene is ThemeScene theme?theme.Project.Name:"";}
    }
}
