using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;
public sealed record MixedCandidate(ContentRef Content,string Name);

public sealed class MixedPlaylistRow(MixedPlaylistViewModel owner,MixedEntry entry):ObservableObject
{
    public void RefreshSource() { OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Kind)); OnPropertyChanged(nameof(Wallpaper)); OnPropertyChanged(nameof(ThemeCover)); }
    public MixedEntry Entry{get;private set;}=entry;
    public string Name=>owner.ResolveName(Entry.Content);
    public string Kind=>owner.ResolveKind(Entry.Content);
    public WallpaperItemViewModel? Wallpaper => owner.ResolveWallpaper(Entry.Content);
    public Uri? ThemeCover => owner.ResolveThemeCover(Entry.Content);
    public bool PlayToEnd{get=>Entry.PlayToEnd;set{Entry=Entry with{PlayToEnd=value};OnPropertyChanged();owner.QueueSave();}}
    public int Seconds{get=>Entry.Seconds;set{Entry=Entry with{Seconds=Math.Clamp(value,10,86400)};OnPropertyChanged();owner.QueueSave();}}
}
public sealed partial class MixedPlaylistViewModel:ObservableObject,IDisposable
{
    private readonly MainViewModel _main;private readonly ContentListStore _store;private readonly DispatcherTimer _timer;
    private Task _save=Task.CompletedTask;private Task? _initializing;private bool _initialized,_running,_paused,_applying,_shuffle,_disposed;private int _generation;private long _due;private TimeSpan _remaining;private Guid? _current;private string _status="尚未开始";
    public ObservableCollection<MixedPlaylistRow> Items{get;}=[];
    private Task _pendingAdvance=Task.CompletedTask;
    private bool _waitForVideoEnd,_videoCompletedWhilePaused;
    public WallpaperItemViewModel? ResolveWallpaper(ContentRef content) => content.Kind == "Wallpaper" ? _main.Library?.AllItems.FirstOrDefault(item => item.Asset.Id == content.Id) : null;
    public Uri? ResolveThemeCover(ContentRef content) => content.Kind == "Theme" ? _main.Creator?.Projects.FirstOrDefault(item => item.Project.Id == content.Id)?.CoverUri : null;
    public string ResolveKind(ContentRef content)=>content.Kind=="Theme"?"主题":_main.Library?.AllItems.FirstOrDefault(w=>w.Asset.Id==content.Id)?.Asset.IsVideo==true?"视频":"壁纸";
    public IReadOnlyList<ManagedDeviceCard> Devices=>_main.Devices.Managed?.Cards.ToArray()??[];
    public IReadOnlyList<MixedCandidate> Candidates=>(_main.Creator?.Projects.Select(t=>new MixedCandidate(new("Theme",t.Project.Id),"主题 · "+t.Name))??[]).Concat((_main.Library?.AllItems??[]).Select(w=>new MixedCandidate(new("Wallpaper",w.Asset.Id),(w.Asset.IsVideo?"视频 · ":"壁纸 · ")+w.Name))).ToArray();
    private ManagedDeviceCard? _target;
    public ManagedDeviceCard? Target{get=>_target;set{if(CanEdit)SetProperty(ref _target,value);}}
    public MixedPlaylistRow? Selected{get;set;}
    public bool IsRunning{get=>_running;private set{if(SetProperty(ref _running,value))OnPropertyChanged(nameof(CanEdit));}}
    public bool CanEdit=>!IsRunning&&!_applying&&!_listChanging;
    public bool IsPaused=>_paused;
    public bool IsApplying=>_applying;
    public string PauseLabel=>_paused?"继续切换":"暂停切换";
    public string NextName
    {
        get { if(!IsRunning||Items.Count==0)return "—";if(Shuffle)return "随机选择";var index=Items.ToList().FindIndex(i=>i.Entry.EntryId==_current);return Items[(index+1)%Items.Count].Name; }
    }
    public string CountdownText => !IsRunning?"未启动":_applying?"正在准备下一项":Target?.Connected!=true?"等待原设备重连":_paused?(_videoCompletedWhilePaused?"本轮视频已结束，继续后切换":"已暂停切换"):_waitForVideoEnd?(Items.Count==1?"单项循环播放":_main.Devices.Managed?.HomeBackgroundPaused==true?"背景已暂停，等待继续":"等待视频播完"):$"{Math.Max(0,Math.Ceiling((_due-Environment.TickCount64)/1000d))} 秒后切换";
    private void NotifyPlayback(){OnPropertyChanged(nameof(ListSummary));OnPropertyChanged(nameof(CurrentName));OnPropertyChanged(nameof(NextName));OnPropertyChanged(nameof(CountdownText));}
    public bool Shuffle{get=>_shuffle;set{if(SetProperty(ref _shuffle,value))QueueSave();}}
    public string Status{get=>_status;private set=>SetProperty(ref _status,value);}
    public string CurrentName=>Items.FirstOrDefault(i=>i.Entry.EntryId==_current)?.Name??"尚未播放";
    public MixedPlaylistViewModel(MainViewModel main,string root)
    {_main=main;_store=new(root);_timer=new(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1)};_timer.Tick+=Tick;}
    public Task InitializeAsync()=>_initializing??=InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        if(_initialized)return;var data=await _store.ReadAsync("mixed-playlist.json",new MixedList(1,[]));
        if(data.Version!=1||data.Items is null||data.Items.Count>100||data.Items.Select(i=>i.EntryId).Distinct().Count()!=data.Items.Count)throw new InvalidDataException("混合轮播列表无效。");
        foreach(var item in data.Items){ContentListStore.Validate(item.Content);if(item.EntryId==Guid.Empty||item.Seconds is <10 or >86400)throw new InvalidDataException("轮播条目无效。");}
        foreach(var item in data.Items)Items.Add(new(this,item));
        _shuffle=data.Shuffle;_initialized=true;OnPropertyChanged(nameof(Shuffle));OnPropertyChanged(nameof(Devices));
        var selected=Devices.Where(c=>c.Device.Identity.SerialNumber==_main.Usb?.SelectedSerial).ToArray();if(selected.Length==1)Target=selected[0];OnPropertyChanged(nameof(Target));
        await LoadNamedListsAsync();
        RefreshSources();
    }
    public void RefreshSources(){OnPropertyChanged(nameof(Candidates));OnPropertyChanged(nameof(FilteredCandidates));OnPropertyChanged(nameof(Devices));foreach(var item in Items)item.RefreshSource();}
    public string ResolveName(ContentRef content)=>content.Kind=="Theme"?_main.Creator?.Projects.FirstOrDefault(p=>p.Project.Id==content.Id)?.Name??"缺失主题":_main.Library?.AllItems.FirstOrDefault(p=>p.Asset.Id==content.Id)?.Name??"缺失壁纸";
    public void Add(ContentRef content){if(!_initialized||!CanEdit)return;ContentListStore.Validate(content);if(Items.Count>=100||Items.Any(i=>i.Entry.Content==content))return;Items.Add(new(this,new(Guid.NewGuid(),content)));QueueSave();}
    public void Remove(){if(CanEdit&&Selected is { } row){Items.Remove(row);QueueSave();}}
    public void Move(int delta){if(!CanEdit||Selected is not { } row)return;var index=Items.IndexOf(row);Items.Move(index,Math.Clamp(index+delta,0,Items.Count-1));QueueSave();}
    public void MoveTo(Guid id,Guid target){if(!CanEdit)return;var a=Items.FirstOrDefault(i=>i.Entry.EntryId==id);var b=Items.FirstOrDefault(i=>i.Entry.EntryId==target);if(a is not null&&b is not null){Items.Move(Items.IndexOf(a),Items.IndexOf(b));QueueSave();}}
    public void QueueSave(){if(!_initialized||_batchSeconds)return;var snapshot=new MixedList(1,Items.Select(i=>i.Entry).ToList(),Shuffle);_save=SaveAfterAsync(_save,snapshot,_activeListFile);NotifyPlayback();}
    private async Task SaveAfterAsync(Task prior,MixedList value,string file){try{await prior;await _store.SaveAsync(file,value);}catch(Exception ex){Status="列表保存失败："+ex.Message;}}
    public Task FlushAsync()=>_save;
    public async Task StartAsync(Guid? entry=null,bool paused=false)
    {
        await InitializeAsync();if(IsRunning||_applying||_listChanging)return;
        if(Target is not {Connected:true,Confirmed:true} || _main.Devices.Managed is null){Status="请选择已连接并确认的目标设备。";return;}
        if(Items.Count==0){Status="请先添加壁纸、视频或主题。";return;}
        _main.Library?.StopRotation();_main.Creator?.StopThemePlaylist();_generation++;IsRunning=true;_paused=false;
        var index=Items.ToList().FindIndex(i=>i.Entry.EntryId==entry);_current=index>=0?Items[(index-1+Items.Count)%Items.Count].Entry.EntryId:null;
        await NextAsync();if(paused&&IsRunning)TogglePause();if(IsRunning)_timer.Start();
    }
    private async Task<ThemeProject> SourceAsync(ContentRef content,ManagedDeviceCard target)
    {
        if(content.Kind=="Theme")return await _main.Creator!.ReadSavedProjectAsync(content.Id);
        var asset=_main.Library?.AllItems.SingleOrDefault(i=>i.Asset.Id==content.Id)?.Asset??throw new InvalidOperationException("壁纸已移除。");
        if(!System.IO.File.Exists(asset.Path))throw new InvalidOperationException("壁纸文件缺失。");
        var size=_main.Devices.Managed!.GetProfile(target).SurfaceSize;
        var source=new ThemeProject{Id=asset.Id,Name=asset.Name,Orientation=size.Width>=size.Height?ThemeOrientation.Landscape:ThemeOrientation.Portrait};source.ActiveLayout.Width=size.Width;source.ActiveLayout.Height=size.Height;
        source.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.Wallpaper,Path=asset.Path,IsVideo=asset.IsVideo,Width=size.Width,Height=size.Height});return source;
    }
    public Task NextAsync()=>_applying?Task.CompletedTask:_pendingAdvance=NextCoreAsync();
    private async Task NextCoreAsync()
    {
        if(!IsRunning||_applying||Target is not { } target||_main.Devices.Managed is not { } devices)return;
        if(_main.Output.IsBusy&&!_main.Output.OwnsTransition){Status="正在处理其他输出操作，稍后继续切换。";return;}
        if(!target.Connected){Status="等待原设备重连，暂停切换。";return;}
        _applying=true;var generation=_generation;var order=Items.ToList();var index=order.FindIndex(i=>i.Entry.EntryId==_current);
        var candidates=Enumerable.Range(1,order.Count).Select(o=>order[(index+o)%order.Count]).ToArray();if(Shuffle)candidates=candidates.OrderBy(i=>i.Entry.EntryId==_current).ThenBy(_=>Random.Shared.Next()).ToArray();
        try
        {
            foreach(var row in candidates)
            {
                if(generation!=_generation||!IsRunning)return;
                try
                {
                    var source=await SourceAsync(row.Entry.Content,target);var revision=target.AcceptedOutputRevision;
                    if(generation!=_generation||!IsRunning)return;
                    await devices.ApplySourceAsync(source,[target.DeviceId],LayoutAdaptation.Contain,true);
                    if(target.AcceptedOutputRevision==revision)throw new InvalidOperationException(target.Message);
                    if(generation!=_generation||!IsRunning)return;
                    var selectedLayout=LayoutSelector.Select(LegacyThemeAdapter.Read(source),devices.GetProfile(target).SurfaceSize).Layout;
                    _current=row.Entry.EntryId;_waitForVideoEnd=row.PlayToEnd&&selectedLayout.Layers.Any(l=>l.Visible&&l.CreateLegacyLayer().IsVideo);_videoCompletedWhilePaused=false;_remaining=TimeSpan.FromSeconds(row.Seconds);_due=Environment.TickCount64+(long)_remaining.TotalMilliseconds;
                    if(row.Entry.Content.Kind=="Wallpaper")_main.Library?.RecordExternalUse(row.Entry.Content.Id,target.Alias);
                    Status="正在轮播："+row.Name+(_waitForVideoEnd?" · 视频播完一轮切换":$" · {row.Seconds}秒后切换");OnPropertyChanged(nameof(CurrentName));Remember();return;
                }
                catch(Exception ex){Status="已跳过 "+row.Name+"："+ex.Message;}
            }
            Stop();Status="没有可播放的条目，原内容按输出结果保留。";
        }
        finally{_applying=false;OnPropertyChanged(nameof(CanEdit));}
    }
    public void TogglePause(){if(!IsRunning||_applying)return;if(!_paused)_remaining=TimeSpan.FromMilliseconds(Math.Max(1,_due-Environment.TickCount64));else _due=Environment.TickCount64+(long)_remaining.TotalMilliseconds;_paused=!_paused;OnPropertyChanged(nameof(PauseLabel));OnPropertyChanged(nameof(IsPaused));Remember();if(!_paused&&_videoCompletedWhilePaused){_videoCompletedWhilePaused=false;_pendingAdvance=NextAsync();}}
    public async Task VideoCompletedAsync(string serial,Guid contentId)
    {
        if(!IsRunning||_applying||!_waitForVideoEnd||Target?.Device.Identity.SerialNumber!=serial||Items.FirstOrDefault(i=>i.Entry.EntryId==_current)?.Entry.Content.Id!=contentId)return;
        if(Items.Count==1)return;
        if(_paused||_main.Output.IsBusy&&!_main.Output.OwnsTransition){_videoCompletedWhilePaused=true;return;}await NextAsync();
    }
    private void Remember(){if(Target?.Device.Identity.SerialNumber is {Length:>0} serial)_main.RememberOutput(new(serial,"MixedPlaylist",LastAssetId:_current,Paused:_paused),serial);}
    public void ObserveAccepted(ManagedDeviceCard card){if(IsRunning&&!_applying&&card.DeviceId==Target?.DeviceId)Stop();}
    public void ObserveLegacy(string serial){if(IsRunning&&!_applying&&serial==Target?.Device.Identity.SerialNumber)Stop();}
    public void ObserveStop(ManagedDeviceCard card){if(card.DeviceId==Target?.DeviceId)Stop();}
    public void Stop()
    {
        var was=IsRunning;_generation++;IsRunning=false;_paused=false;_timer.Stop();OnPropertyChanged(nameof(IsPaused));OnPropertyChanged(nameof(PauseLabel));Status="已停止自动切换，当前内容仍可继续输出。";
        if(was&&!_applying&&!_disposed&&Target?.Device.Identity.SerialNumber is {Length:>0} serial && Items.FirstOrDefault(i=>i.Entry.EntryId==_current) is { } row)
        {
            var content=row.Entry.Content;
            var resume=content.Kind=="Theme"?new D92ResumeContent(serial,"Theme",ThemeId:content.Id):_main.Library?.AllItems.FirstOrDefault(i=>i.Asset.Id==content.Id) is { } asset?new(serial,asset.Asset.IsVideo?"Video":"Image",asset.Asset.Path):null;
            _main.RememberOutput(resume,serial);
        }
    }
    private async void Tick(object? sender,EventArgs e){try{NotifyPlayback();if(_applying)return;if(!IsRunning||Target?.IsActive!=true){Stop();return;}if(!_paused&&_videoCompletedWhilePaused&&!_main.Output.IsBusy){_videoCompletedWhilePaused=false;await NextAsync();return;}if(!_paused&&!_waitForVideoEnd&&Environment.TickCount64>=_due){if(Items.Count==1)_due=Environment.TickCount64+Items[0].Seconds*1000L;else await NextAsync();}}catch(Exception ex){Stop();Status=ex.Message;}}
    public void Dispose(){if(_disposed)return;_disposed=true;Stop();_timer.Tick-=Tick;}
    public async Task PrepareForCloseAsync(){Stop();await _pendingAdvance;await FlushAsync();}
}
