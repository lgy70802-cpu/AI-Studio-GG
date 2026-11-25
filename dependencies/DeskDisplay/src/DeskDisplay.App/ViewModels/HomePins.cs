using DeskDisplay.Library;
namespace DeskDisplay.App.ViewModels;
public sealed partial class MainViewModel
{
    private ContentListStore? _contentStore;private List<ContentRef> _pins=[];private Task _pinSave=Task.CompletedTask;private bool _pinsReady;
    private MixedPlaylistViewModel? _mixed;
    public MixedPlaylistViewModel MixedPlaylist=>_mixed??=new(this,ClientTools.Root);
    public MixedPlaylistViewModel? ActiveMixedPlaylist=>_mixed;
    public IReadOnlyList<HomeRecentEntry> HomePinnedEntries=>_pins.Select(p=>p.Kind=="Theme"?(object?)Creator?.Projects.FirstOrDefault(t=>t.Project.Id==p.Id):Library?.AllItems.FirstOrDefault(w=>w.Asset.Id==p.Id)).Where(s=>s is not null).Select(s=>new HomeRecentEntry(s is ThemeCard t?t.Name:((WallpaperItemViewModel)s!).Name,s!,DateTimeOffset.MinValue)).ToArray();
    public bool HasHomePins=>HomePinnedEntries.Count>0;
    public ActionCommand ToggleHomePinCommand=>_togglePin??=new(value=>{if(ToContentRef(value) is { } reference)TogglePin(reference);});
    private ActionCommand? _togglePin;
    public ActionCommand AddToMixedCommand=>_addMixed??=new(value=>{if(ToContentRef(value) is { } reference)_=AddMixedAsync(reference);});
    private async Task AddMixedAsync(ContentRef content){try{await MixedPlaylist.InitializeAsync();MixedPlaylist.Add(content);Output.Report("已加入混合轮播。请到轮播管理查看。");}catch(Exception ex){Output.Report("未加入轮播："+ex.Message,true);}}
    private ActionCommand? _addMixed;
    private static ContentRef? ToContentRef(object? value){var source=value is HomeRecentEntry entry?entry.Source:value;return source switch{ThemeCard t=>new("Theme",t.Project.Id),WallpaperItemViewModel w=>new("Wallpaper",w.Asset.Id),_=>null};}
    public async Task InitializeContentListsAsync(string? root=null)
    {
        root??=ClientTools.Root;_contentStore=new(root);var pins=await _contentStore.ReadAsync("home-pins.json",new List<ContentRef>());
        if(pins.Count>12)throw new System.IO.InvalidDataException("首页固定内容超过12项。");foreach(var pin in pins)ContentListStore.Validate(pin);_pins=pins.Distinct().ToList();_pinsReady=true;NotifyPins();
        _mixed??=new(this,root);await _mixed.InitializeAsync();
    }
    public void TogglePin(ContentRef reference)
    {
        if(!_pinsReady){Output.Report("固定列表尚未读取，不会覆盖原文件。",true);return;}
        ContentListStore.Validate(reference);if(!_pins.Remove(reference)){if(_pins.Count>=12){Output.Report("首页最多固定12项，请先取消一项。",true);return;}_pins.Add(reference);}
        NotifyPins();if(_contentStore is not null){var snapshot=_pins.ToList();_pinSave=SavePinsAfterAsync(_pinSave,snapshot);}
    }
    private async Task SavePinsAfterAsync(Task previous,List<ContentRef> pins){try{await previous;await _contentStore!.SaveAsync("home-pins.json",pins);}catch(Exception ex){Output.Report("固定内容保存失败："+ex.Message,true);}}
    private void NotifyPins(){OnPropertyChanged(nameof(HomePinnedEntries));OnPropertyChanged(nameof(HasHomePins));_mixed?.RefreshSources();}
    public async Task FlushContentListsAsync(){await _pinSave;if(_mixed is not null)await _mixed.FlushAsync();}
}
