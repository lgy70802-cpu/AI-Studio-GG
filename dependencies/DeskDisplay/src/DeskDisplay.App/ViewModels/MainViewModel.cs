using System.ComponentModel;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Interfaces;

namespace DeskDisplay.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public OutputCoordinator Output { get; } = new();
    public string AppVersionLabel => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "开发版";
    private string _selectedNavigation = "首页";
    private AppPage _selectedPage = AppPage.Home;
    private bool _homePortrait;
    private ISettingsService? _settings;
    private Task _homeSettingsSave = Task.CompletedTask;
    public IReadOnlyList<string> NavigationItems { get; } = ["首页", "设备", "设置", "诊断"];
    public IReadOnlyList<NavigationItem> SidebarItems { get; } =
    [
        new(AppPage.Home, "首页", "\uE80F"),
        new(AppPage.Online, "在线资源", "\uE753"),
        new(AppPage.Library, "我的资源库", "\uE8F1"),
        new(AppPage.Creator, "创作中心", "\uE70F"),
        new(AppPage.Carousel, "轮播列表", "\uE8FD"),
        new(AppPage.Plugins, "插件中心", "\uE74C"),
        new(AppPage.Devices, "设备管理", "\uE7F4"),
        new(AppPage.Account, "个人中心", "\uE77B"),
        new(AppPage.Settings, "设置", "\uE713")
    ];
    public IEnumerable<NavigationItem> WorkspaceNavigation => SidebarItems.Where(item => item.Page is not (AppPage.Account or AppPage.Settings));
    public IEnumerable<NavigationItem> PersonalNavigation => SidebarItems.Where(item => item.Page is AppPage.Account or AppPage.Settings);
    public IReadOnlyList<DemoContentItem> DemoWallpapers { get; } =
    [
        new("晨雾山谷", "自然风景", "柔和冷色 · 静态壁纸演示", "#DCE8F4", "#7FA7CF"),
        new("深海微光", "科幻未来", "深蓝光影 · 静态壁纸演示", "#172B4D", "#4C85F6"),
        new("日落海岸", "治愈", "暖色天空 · 静态壁纸演示", "#F6D8C5", "#E89274"),
        new("月夜城市", "氛围写意", "夜景灯光 · 静态壁纸演示", "#202A46", "#9A8AEF"),
        new("春日花园", "清新可爱", "明亮花色 · 静态壁纸演示", "#E8F2E3", "#86B88A"),
        new("极简沙丘", "高级设计", "中性色调 · 静态壁纸演示", "#EEE8DF", "#B89B78")
    ];
    public IReadOnlyList<DemoContentItem> DemoHomeWallpapers => DemoWallpapers.Take(6).ToArray();
    public IReadOnlyList<string> DemoPrimaryCategories { get; } = ["推荐", "动漫插画", "治愈", "科幻未来", "美女人像", "萌宠", "风光美景", "高级设计"];
    public IReadOnlyList<string> DemoSecondaryCategories { get; } = ["清新可爱", "古风文艺", "氛围写意", "欧美女神"];
    public string Title => "Desk Display Alpha";
    public string Description => "场景预览 · 时钟 / 图片 / 组合 / 视频 / 性能";
    public DevicesViewModel Devices { get; }
    private Scene _scene;
    public Scene Scene => _scene;
    private ThemeScene? _homeOutput;
    public Scene HomeScene => _homeOutput ?? (Usb is {IsRunning:true} usb ? usb.Scene : Scene);
    public void UseSharedHomeScene(){_homeOutput=null;OnPropertyChanged(nameof(HomeScene));OnPropertyChanged(nameof(HomePreviewAspect));}
    public void SetHomeOutputScene(ThemeScene scene)
    {
        _homeOutput=scene;
        OnPropertyChanged(nameof(HomeScene));OnPropertyChanged(nameof(HomePreviewAspect));
        RefreshNowPlaying();
    }
    public ImageSceneViewModel? Images { get; private set; }
    public VideoSceneViewModel? Videos { get; private set; }
    public WorkspaceViewModel? Workspace { get; private set; }
    public LibraryViewModel? Library { get; private set; }
    public UsbOutputViewModel? Usb { get; private set; }
    public PluginManagerViewModel? Plugins { get; private set; }
    public CloudLibraryViewModel? Cloud { get; private set; }
    public StudioAccountViewModel? Account { get; private set; }
    public CreatorViewModel? Creator { get; private set; }
    public void SetCreator(CreatorViewModel creator) { if (Creator is not null) Creator.PropertyChanged -= OnCreatorSourceChanged; Creator = creator; Creator.PropertyChanged += OnCreatorSourceChanged; OnPropertyChanged(nameof(Creator)); NotifyHome(); Library?.NotifyThemeResources(); }
    private void OnCreatorSourceChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(CreatorViewModel.Projects)) { NotifyHome(); Library?.NotifyThemeResources(); } }
    public void SetUsb(UsbOutputViewModel usb) { Usb = usb; usb.PropertyChanged += OnHomeSourceChanged; OnPropertyChanged(nameof(Usb)); NotifyHome(); }
    public void SetLibrary(LibraryViewModel library) { Library = library; library.PropertyChanged += OnHomeSourceChanged; OnPropertyChanged(nameof(Library)); NotifyHome(); }
    public void SetWorkspace(WorkspaceViewModel workspace) { Workspace = workspace; OnPropertyChanged(nameof(Workspace)); }
    public void SetPlugins(PluginManagerViewModel plugins) { Plugins = plugins; OnPropertyChanged(nameof(Plugins)); }
    public void SetCloud(CloudLibraryViewModel cloud) { Cloud = cloud; cloud.PropertyChanged += OnHomeSourceChanged; OnPropertyChanged(nameof(Cloud)); NotifyHome(); }
    public void SetAccount(StudioAccountViewModel account) { Account = account; OnPropertyChanged(nameof(Account)); }
    public NavigationItem? SelectedSidebarItem
    {
        get => SidebarItems.FirstOrDefault(item => item.Page == _selectedPage);
        set
        {
            if (value is null || value.Page == _selectedPage) return;
            SelectedPage = value.Page;
        }
    }
    public AppPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (!SetProperty(ref _selectedPage, value)) return;
            _selectedNavigation = value switch
            {
                AppPage.Home => "首页", AppPage.Carousel => "轮播管理", AppPage.Library => "资源库", AppPage.Online => "在线资源",
                AppPage.Creator => "创作中心", AppPage.Plugins => "插件", AppPage.Devices => "设备", AppPage.Account => "个人中心", _ => "设置"
            };
            OnPropertyChanged(nameof(SelectedNavigation));
            OnPropertyChanged(nameof(SelectedSidebarItem));
        }
    }
    public bool HomePortrait
    {
        get => _homePortrait;
        set { if (SetProperty(ref _homePortrait, value)) { OnPropertyChanged(nameof(HomePreviewAspect)); QueueHomeSettingsSave(); } }
    }
    public double HomePreviewAspect => _homeOutput?.AspectRatio ?? (Usb is {IsRunning:true} usb ? usb.Scene.AspectRatio : HomePortrait ? 462d / 1920d : 1920d / 462d);
    private ManagedDevicesViewModel? _homeDevices;
    public string HomeDeviceStatus => HomeDeviceConnected ? "已连接" : "未连接";
    public bool HomeDeviceConnected => Usb?.IsSelectedConnected==true || string.IsNullOrEmpty(Usb?.SelectedSerial) && _homeDevices?.Cards.Count(c=>c.Connected && c.Device.ModelKey=="D92")==1;
    public IReadOnlyList<WallpaperItemViewModel> HomeRecentItems => Library?.RecentItems ?? [];
    public IReadOnlyList<HomeRecentEntry> HomeRecentEntries => HomeRecentItems.Select(w => new HomeRecentEntry(w.Name, w, Library!.LastUsedAt(w.Asset.Id)))
        .Concat((Creator?.RecentUsedProjects ?? []).Select(t => new HomeRecentEntry(t.Name, t, t.LastUsedAt!.Value)))
        .OrderByDescending(e => e.UsedAt).Take(6).ToArray();
    public IReadOnlyList<DeskDisplay.Library.StudioResource> HomeRecommendedItems => Cloud?.Items
        .OrderByDescending(item => item.UpdatedAt).Take(6).ToArray() ?? [];
    public string HomeRecommendedStatus => Cloud is null || Cloud.IsBusy ? "正在读取在线资源…" : Cloud.Items.Count == 0 ? "暂无可用推荐" : string.Empty;
    public bool HomeHasRecent => HomeRecentEntries.Count > 0;
    public bool HomeHasRecommendations => HomeRecommendedItems.Count > 0;
    public bool HomeShowDemoRecent => !HomeHasRecent;
    public bool HomeShowDemoRecommendations => !HomeHasRecommendations && Cloud?.IsBusy != true;
    public ActionCommand ShowLandscapeCommand { get; }
    public ActionCommand ShowPortraitCommand { get; }
    public ActionCommand OpenDeviceSettingsCommand { get; }
    public ActionCommand OpenCarouselManagerCommand { get; }
    public ActionCommand OpenHomeResourceCommand { get; }
    public ActionCommand OpenHomeRecommendationCommand { get; }
    public ActionCommand NavigateSectionCommand { get; }
    public AsyncCommand? PerformanceCommand { get; private set; }
    public AsyncCommand? ApplyPerformanceCommand { get; private set; }
    public void SetPerformanceScene(PerformanceScene scene, Serilog.ILogger logger)
    {
        PerformanceCommand = new AsyncCommand(() => { PreviewScene(scene); return Task.CompletedTask; }, () => true, logger);
        ApplyPerformanceCommand = new AsyncCommand(() => Devices.ApplySceneAsync(scene),
            () => ReferenceEquals(Scene, scene) && Devices.TestDisplayCommand.CanExecute(null), logger);
        Devices.TestDisplayCommand.CanExecuteChanged += OnCommandsChanged;
        OnPropertyChanged(nameof(PerformanceCommand)); OnPropertyChanged(nameof(ApplyPerformanceCommand));
    }
    private void OnCommandsChanged(object? sender, EventArgs e) => ApplyPerformanceCommand?.NotifyCanExecuteChanged();
    public void SetVideoEditor(VideoSceneViewModel editor) { Videos = editor; OnPropertyChanged(nameof(Videos)); }
    public void SetImageEditor(ImageSceneViewModel editor) { Images = editor; OnPropertyChanged(nameof(Images)); }
    public void PreviewScene(Scene scene)
    {
        if (_scene is VideoScene previous && !ReferenceEquals(_scene, scene))
        {
            Devices.CloseRender();
            previous.Playback.Stop();
        }
        _scene = scene;
        OnPropertyChanged(nameof(HomeScene));OnPropertyChanged(nameof(HomePreviewAspect));
        UpdatePreview();
        OnPropertyChanged(nameof(Scene));
        ApplyPerformanceCommand?.NotifyCanExecuteChanged();
    }
    public string PreviewDescription => Usb?.IsRunning == true ? $"USB 共享画布 · {Scene.PixelWidth} × {Scene.PixelHeight}" : Devices.HasSavedTarget
        ? Devices.TargetPixelWidth > 0 && Devices.TargetPixelHeight > 0
            ? $"{Devices.TargetName} · {Scene.PixelWidth} × {Scene.PixelHeight} · {(Devices.TargetDisplay is null ? "未连接，使用最近一次比例" : "已连接") }"
            : $"{Devices.TargetName} · 分辨率未知，当前为 16:9 示例比例"
        : "尚未设置目标副屏 · 当前为 16:9 示例比例";
    public string SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (!SetProperty(ref _selectedNavigation, value)) return;
            _selectedPage = value switch
            {
                "首页" => AppPage.Home, "轮播管理" => AppPage.Carousel, "资源库" => AppPage.Library, "在线资源" => AppPage.Online,
                "插件" => AppPage.Plugins, "创作中心" => AppPage.Creator, "设备" or "USB屏" => AppPage.Devices,
                "个人中心" => AppPage.Account, "设置" or "诊断" => AppPage.Settings, _ => _selectedPage
            };
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(SelectedSidebarItem));
        }
    }

    private void OnHomeSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(sender is UsbOutputViewModel && e.PropertyName is nameof(UsbOutputViewModel.Scene) or nameof(UsbOutputViewModel.IsRunning))
        {OnPropertyChanged(nameof(HomeScene));OnPropertyChanged(nameof(HomePreviewAspect));}
        if (sender is UsbOutputViewModel && e.PropertyName == nameof(UsbOutputViewModel.IsSelectedConnected)
            || sender is LibraryViewModel && e.PropertyName == nameof(LibraryViewModel.RecentItems)
            || sender is CloudLibraryViewModel && e.PropertyName is nameof(CloudLibraryViewModel.Items) or nameof(CloudLibraryViewModel.IsBusy))
            NotifyHome();
    }
    private void NotifyHome()
    {
        NotifyPins();
        OnPropertyChanged(nameof(HomeDeviceStatus)); OnPropertyChanged(nameof(HomeDeviceConnected));
        OnPropertyChanged(nameof(HomeRecentItems)); OnPropertyChanged(nameof(HomeRecommendedItems));
        OnPropertyChanged(nameof(HomeRecentEntries));
        OnPropertyChanged(nameof(HomeRecommendedStatus));
        OnPropertyChanged(nameof(HomeHasRecent)); OnPropertyChanged(nameof(HomeHasRecommendations));
        OnPropertyChanged(nameof(HomeShowDemoRecent)); OnPropertyChanged(nameof(HomeShowDemoRecommendations));
    }

    public MainViewModel(DevicesViewModel devices, Scene scene)
    {
        Devices = devices;
        _scene = scene;
        ShowLandscapeCommand = new(_ => HomePortrait = false);
        ShowPortraitCommand = new(_ => HomePortrait = true);
        OpenDeviceSettingsCommand = new(_ => SelectedPage = AppPage.Devices);
        OpenCarouselManagerCommand = new(_ => {CarouselSection=2;SelectedPage = AppPage.Carousel;});
        OpenHomeResourceCommand = new(value =>
        {
            if (value is not WallpaperItemViewModel item || Library is null) return;
            SelectedPage = AppPage.Library; Library.OpenDetailCommand.Execute(item);
        });
        OpenHomeRecommendationCommand = new(value =>
        {
            if (value is not DeskDisplay.Library.StudioResource item || Cloud is null) return;
            SelectedPage = AppPage.Online; Cloud.OpenDetailCommand.Execute(item);
        });
        NavigateSectionCommand = new(parameter =>
        {
            if (parameter is not string tab) return;
            if (Library?.Tabs.Contains(tab) == true) { Library.SelectedTab = tab; SelectedPage = AppPage.Library; }
            else SelectedNavigation = tab;
        });
        Devices.PropertyChanged += OnDevicesChanged;
        UpdatePreview();
    }

    public void ConfigureHome(ISettingsService settings, AppSettings initial)
    {
        _settings = settings;
        _homePortrait = initial.HomePortrait;
        OnPropertyChanged(nameof(HomePortrait)); OnPropertyChanged(nameof(HomePreviewAspect));
    }

    private void QueueHomeSettingsSave()
    {
        if (_settings is null) return;
        var previous = _homeSettingsSave;
        var portrait = _homePortrait;
        _homeSettingsSave = SaveHomeSettingsAsync(previous, portrait);
    }

    private async Task SaveHomeSettingsAsync(Task previous, bool portrait)
    {
        try { await previous; await _settings!.UpdateAsync(settings => settings.HomePortrait = portrait); }
        catch { }
    }

    public Task PrepareForCloseAsync() => Task.WhenAll(_homeSettingsSave,RuntimePending,FlushContentListsAsync(),_mixed?.PrepareForCloseAsync()??Task.CompletedTask);

    private void OnDevicesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(DevicesViewModel.Managed))
        {
            if(_homeDevices is not null)_homeDevices.PropertyChanged-=OnManagedHomeChanged;
            _homeDevices=Devices.Managed;
            if(_homeDevices is not null)_homeDevices.PropertyChanged+=OnManagedHomeChanged;
            NotifyHome();
        }
        if (e.PropertyName == nameof(DevicesViewModel.TargetDisplay)) UpdatePreview();
    }
    private void OnManagedHomeChanged(object? sender,PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(ManagedDevicesViewModel.HasDevices))NotifyHome();}

    public void UpdatePreview()
    {
        if (Usb?.ApplyCanvas() == true) { OnPropertyChanged(nameof(PreviewDescription)); return; }
        if (Scene is StockScene or RetroCameraClockScene)
        {
            Scene.SetCanvasSize(1920, 462);
            OnPropertyChanged(nameof(PreviewDescription));
            return;
        }
        var width = Devices.TargetPixelWidth;
        var height = Devices.TargetPixelHeight;
        Scene.SetCanvasSize(width > 0 && height > 0 ? width : 1600, width > 0 && height > 0 ? height : 900);
        OnPropertyChanged(nameof(PreviewDescription));
    }

    public void Dispose()
    {
        _mixed?.Dispose();
        DisposeOutputExperience();
        if(_homeDevices is not null)_homeDevices.PropertyChanged-=OnManagedHomeChanged;
        if (Creator is not null) Creator.PropertyChanged -= OnCreatorSourceChanged;
        if (Usb is not null) Usb.PropertyChanged -= OnHomeSourceChanged;
        if (Library is not null) Library.PropertyChanged -= OnHomeSourceChanged;
        if (Cloud is not null) Cloud.PropertyChanged -= OnHomeSourceChanged;
        Usb?.Dispose();
        Devices.PropertyChanged -= OnDevicesChanged;
        Devices.TestDisplayCommand.CanExecuteChanged -= OnCommandsChanged;
        Library?.Dispose();
        Creator?.Dispose();
        Workspace?.Dispose();
        Images?.Dispose();
        Videos?.Dispose();
        Plugins?.Dispose();
        Cloud?.Dispose();
        Account?.Dispose();
    }
}
