using DeskDisplay.Core.Models;
using DeskDisplay.Core.Interfaces;
using System.Windows.Threading;

namespace DeskDisplay.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _closeToTray,_launchAtLogin,_restoreD92;
    private bool _registeredLogin;
    private Task _runtimeSave=Task.CompletedTask;
    private List<D92ResumeContent> _resume=[];
    private Func<Task>? _restorePreview;
    private string _previewLabel="";
    private bool _endingPreview;
    private bool _outputShutdown;
    private readonly DispatcherTimer _responsiveness=new();
    private long _lastPulse;
    private double _maxPulseDelay;
    public double MaxUiDelayMilliseconds=>_maxPulseDelay;
    public string RenderPerformanceSummary {get{var s=DeskDisplay.Display.Usb.RenderPerformance.Snapshot;return $"实际编码 {s.Frames} 帧 · 静态缓存命中 {DeskDisplay.Display.Usb.RenderPerformance.CacheHits} 次 · 平均 {s.AverageMilliseconds:F1} ms · 最大 {s.MaxMilliseconds:F1} ms";}}
    public string RuntimeFeedback {get;private set;}="";
    public Func<bool,Task>? SetLoginStartupAsync {get;set;}
    public bool CloseToTray { get=>_closeToTray;set {if(SetProperty(ref _closeToTray,value))SaveRuntime();} }
    public bool LaunchAtLogin { get=>_launchAtLogin;set {if(SetProperty(ref _launchAtLogin,value))SaveRuntime();} }
    public bool RestoreD92OnStartup { get=>_restoreD92;set {if(SetProperty(ref _restoreD92,value))SaveRuntime();} }
    public bool HasDevicePreview=>_restorePreview is not null;
    public string DevicePreviewLabel=>_previewLabel;
    public AsyncCommand EndDevicePreviewCommand {get;private set;}=null!;
    public Task RuntimePending=>_runtimeSave;
    public IReadOnlyList<D92ResumeContent> RememberedContents=>_resume.ToArray();
    public void ConfigureOutputExperience(ISettingsService settings,AppSettings initial)
    {
        _settings=settings;_closeToTray=initial.CloseToTray;_launchAtLogin=initial.LaunchAtLogin;_restoreD92=initial.RestoreD92OnStartup;
        _registeredLogin=initial.LaunchAtLogin;
        _resume=initial.D92ResumeContents?.Where(c=>c is not null && !string.IsNullOrWhiteSpace(c.Serial)).GroupBy(c=>c.Serial).Select(g=>g.Last()).Take(64).ToList()??[];
        EndDevicePreviewCommand=new(EndDevicePreviewAsync,()=>HasDevicePreview&&!Output.IsBusy&&!_endingPreview,Serilog.Log.Logger);
        Output.PropertyChanged+=OnOutputTransition;
        foreach(var name in new[]{nameof(CloseToTray),nameof(LaunchAtLogin),nameof(RestoreD92OnStartup),nameof(EndDevicePreviewCommand)})OnPropertyChanged(name);
        _responsiveness.Interval=TimeSpan.FromMilliseconds(250);_responsiveness.Tick+=MeasureUiPulse;_lastPulse=Environment.TickCount64;_responsiveness.Start();
    }
    private void MeasureUiPulse(object? sender,EventArgs e)
    {
        var now=Environment.TickCount64;var gap=now-_lastPulse;_lastPulse=now;
        RefreshNowPlaying();
        if(gap>10000)return;
        _maxPulseDelay=Math.Max(_maxPulseDelay,Math.Max(0,gap-250));OnPropertyChanged(nameof(MaxUiDelayMilliseconds));
        OnPropertyChanged(nameof(RenderPerformanceSummary));
    }
    public void SetDevicePreview(string name,Func<Task> restore)
    { _restorePreview=restore;_previewLabel="设备预览 · "+name+"（未保存版本）";NotifyDevicePreview(); }
    public void ClearDevicePreview(){_restorePreview=null;_previewLabel="";NotifyDevicePreview();}
    public async Task EndDevicePreviewAsync()
    {
        if(_endingPreview || Output.IsBusy || _restorePreview is not { } restore)return;
        _endingPreview=true;NotifyDevicePreview();
        try {await Output.RunAsync("恢复原内容",restore);_restorePreview=null;_previewLabel="";Output.Report("设备预览已结束");}
        catch(Exception ex){Output.Report("无法恢复："+ex.Message,true);}
        finally{_endingPreview=false;NotifyDevicePreview();}
    }
    private void OnOutputTransition(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(OutputCoordinator.IsBusy))EndDevicePreviewCommand?.NotifyCanExecuteChanged();}
    private void NotifyDevicePreview(){OnPropertyChanged(nameof(HasDevicePreview));OnPropertyChanged(nameof(DevicePreviewLabel));EndDevicePreviewCommand?.NotifyCanExecuteChanged();}
    public void RememberOutput(D92ResumeContent? content,string serial)
    { if(_outputShutdown)return;_resume.RemoveAll(c=>c.Serial==serial);if(content is not null)_resume.Add(content);SaveRuntime(); }
    public D92ResumeContent? DescribeLegacyOutput(string serial,Scene? scene)=>scene switch
    {
        ThemeScene theme when Creator?.IsThemeRotating==true && Creator.ThemePlaylistTarget=="D92 USB"=>new(serial,"ThemePlaylist",ThemeId:theme.Project.Id,Paused:Creator.IsThemePaused),
        ImageScene image when Library?.IsRotating==true && Library.RotationTarget=="D92 USB"=>new(serial,"WallpaperPlaylist",LastAssetId:Library.AllItems.FirstOrDefault(i=>string.Equals(i.Asset.Path,image.Path,StringComparison.OrdinalIgnoreCase))?.Asset.Id,Paused:Library.IsCarouselPaused),
        ImageScene image=>new(serial,"Image",image.Path,ImageMode:image.Mode),
        VideoFrameScene video when Library?.IsRotating==true && Library.RotationTarget=="D92 USB"=>new(serial,"WallpaperPlaylist",LastAssetId:Library.AllItems.FirstOrDefault(i=>string.Equals(i.Asset.Path,video.Path,StringComparison.OrdinalIgnoreCase))?.Asset.Id,Paused:Library.IsCarouselPaused),
        VideoFrameScene video=>new(serial,"Video",video.Path,Paused:Usb?.IsVideoPaused==true),
        ThemeScene theme when Creator?.Projects.Any(p=>p.Project.Id==theme.Project.Id)==true=>new(serial,"Theme",ThemeId:theme.Project.Id),
        StockScene=>new(serial,"Stock"),RetroCameraClockScene=>new(serial,"Camera"),_=>null
    };
    public void RememberLegacyOutput(string serial,Scene? scene)=>RememberOutput(DescribeLegacyOutput(serial,scene),serial);
    public D92ResumeContent? DescribeManagedOutput(string serial,ThemeProject? source)
    {
        if(source is null)return null;
        if(Creator?.IsThemeRotating==true&&Creator.ThemePlaylistTarget=="D92 USB")return new(serial,"ThemePlaylist",ThemeId:source.Id,Paused:Creator.IsThemePaused);
        if(Creator?.Projects.Any(p=>p.Project.Id==source.Id)==true)return new(serial,"Theme",ThemeId:source.Id);
        var asset=Library?.AllItems.FirstOrDefault(i=>i.Asset.Id==source.Id)?.Asset;
        return asset is null?null:new(serial,asset.IsVideo?"Video":"Image",asset.Path);
    }
    public async Task CaptureOutputShutdownAsync()
    {
        if(Usb is {IsRunning:true,SelectedSerial:{Length:>0} serial} usb)RememberLegacyOutput(serial,usb.Scene);
        _outputShutdown=true;await RuntimePending;
    }
    public void CancelOutputShutdown()=>_outputShutdown=false;
    private void SaveRuntime()
    {
        var previous=_runtimeSave;var tray=_closeToTray;var login=_launchAtLogin;var restore=_restoreD92;var contents=_resume.ToList();
        _runtimeSave=SaveRuntimeAsync(previous,tray,login,restore,contents);
    }
    private async Task SaveRuntimeAsync(Task previous,bool tray,bool login,bool restore,List<D92ResumeContent> contents)
    {
        try
        {
            await previous;if(_settings is null)return;
            if(!await _settings.UpdateAsync(s=>{s.CloseToTray=tray;s.LaunchAtLogin=login;s.RestoreD92OnStartup=restore;s.D92ResumeContents=contents;}))throw new InvalidOperationException("设置文件未能保存");
            if(login!=_registeredLogin && SetLoginStartupAsync is { } apply){await apply(login);_registeredLogin=login;}
            RuntimeFeedback="设置已保存";
        }
        catch(Exception ex){RuntimeFeedback="设置未完全生效："+ex.Message;Serilog.Log.Warning(ex,"Runtime preferences save failed");}
        OnPropertyChanged(nameof(RuntimeFeedback));
    }
    public async Task RestoreRememberedOutputsAsync(Func<D92ResumeContent,Task> apply)
    {
        if(!RestoreD92OnStartup)return;
        var report=new List<string>();
        foreach(var content in _resume.ToArray())
        { try {await apply(content);report.Add(content.Serial+"：恢复请求已处理");}catch(Exception ex){report.Add(content.Serial+"：未恢复，"+ex.Message);Output.Report("未恢复 "+content.Serial+"："+ex.Message,true);Output.SetRetry(()=>apply(content));Serilog.Log.Warning(ex,"Cannot restore D92 content");} }
        RuntimeFeedback=report.Count==0?"没有可恢复的 D92 内容":string.Join("\n",report);OnPropertyChanged(nameof(RuntimeFeedback));
    }
    private void DisposeOutputExperience(){_responsiveness.Stop();_responsiveness.Tick-=MeasureUiPulse;Output.PropertyChanged-=OnOutputTransition;}
}
