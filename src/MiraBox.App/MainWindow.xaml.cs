using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MiraBox.Core;
using Size=MiraBox.Core.Size;
namespace MiraBox.App;
public partial class MainWindow : Window
{
    static readonly string DataRoot=Environment.GetEnvironmentVariable("MIRABOX_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiraBoxStudio.V67");
    readonly LibraryStore library=new(Path.Combine(DataRoot,"Library"),!File.Exists(Path.Combine(DataRoot,"workspace.json")));
    readonly string sessionFile=Path.Combine(DataRoot,"workspace.json");
    Workspace state;string page="首页";string? target;string? detailDeviceId;string query="",collection="全部资源",contentType="全部",category="全部",subcategory="全部",sort="最近添加";int browseRotation;bool showAll,selecting,initializing=true;
    bool refreshingTargets;
    readonly HashSet<string> selected=[];readonly Dictionary<string,string> images=[];readonly HashSet<string> favorites=[];
    int activeScenario;
    sealed record BrowseState(string Query,string Collection,string Type,string Category,string Subcategory,string Sort,int Rotation,bool All,double Offset);
    readonly Dictionary<string,BrowseState> pageMemory=[];
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};readonly Stopwatch watch=Stopwatch.StartNew();TimeSpan previous,lastSave;
    public MainWindow()
    {
        CreateHardwareOutput = CreateModelHardwareOutput;
        state = new Workspace(clock: IsPlaylistDemo ? demoScheduleClock : null, library: library);
        InitializeComponent();
        Title = $"D92 Studio · V7 · {Path.GetFileName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))}";
        if (IsPlaylistDemo) Title += " · 演示模式";
        LoadStartupPreferences();
        ApplyShellWidth(Width);
        BuildNavigation();
        StateChanged += (_, _) => MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        Scenario.ItemsSource=new[]{"只有一台 D92","只有一台 D69","两台 D92（一横一竖）","三台设备","没有连接设备"};
        var scenarioActions = new WrapPanel();
        if (IsPlaylistDemo) scenarioActions.Children.Add(Action("重置演示数据", ResetDemo));
        scenarioActions.Children.Add(Action("查看实机发送诊断", OpenHardwareDiagnostics));
        ScenarioActions.Content = scenarioActions;
        var saved=SessionStore.Read(sessionFile);
        if(saved!=null){state=SessionStore.Restore(saved,library,IsPlaylistDemo ? demoScheduleClock : null);Scenario.SelectedIndex=saved.Scenario;}
        else
        {
            // One-time migration from the earlier split library/list files. Existing files remain intact.
            library.Replace(library.Entries.Select(x=>x.Downloaded?x with{Online=true}:x));
            library.Seed(DemoSeedResources());
            state.Playlists.AddRange(PlaylistStore.Load(Path.Combine(DataRoot,"playlists.json")));Scenario.SelectedIndex=0;
        }
        activeScenario=Math.Max(0,Scenario.SelectedIndex);
        HideSimulationInDailyMode();
        initializing=false;
        if(saved==null && IsPlaylistDemo)SetScenario();else RefreshTargets();
        SyncLibrary();
        Scenario.Visibility = Target.Visibility = ScenarioLabel.Visibility = TargetLabel.Visibility =
            IsPlaylistDemo ? Visibility.Visible : Visibility.Collapsed;
        DemoToggle.Content = IsPlaylistDemo ? "演示模式" : "打开演示模式";
        if (saved != null) state.PrepareStartupOutput(preferences.Restore, StartupResourceAvailable);
        timer.Tick += (_, _) =>
        {
            try
            {
                var now = watch.Elapsed;
                var elapsed = now - previous;
                state.Advance(elapsed);
                previous = now;
                TickHardwareSessions(elapsed, now);
                TickDisplayPower();
                UpdateHardwareVideoPreviewDemand();
                TrimWoodlandRuntimes();
                PollManagedHardwareDevices(now);
                if (hardwareOutput != null) RenderHardwareStatus();
                if (state.TickSchedules(ExecuteHardwareSchedule, TakeHardwareScheduleReceipt))
                {
                    SaveWorkspace();
                    if (page == "定时切换") Render();
                }
                RenderOutputs();
                UpdatePlaylistStatus();
                UpdateHomePreview();
                UpdateHardwareOverview();
                UpdateManagedDeviceOutput();
                UpdateServicePageState();
                UpdateSchedulePageClock();
                if (now - lastSave > TimeSpan.FromSeconds(1)) { SaveWorkspace(); lastSave = now; }
            }
            catch (Exception ex) { Notice.Text = "状态保存失败：" + ex.Message; }
        };
        timer.Start();
        ConfigureHardwareVideoPreview();
        ConfigureWindowLifecycle();
        LoadHardwareApplyTarget();
        stoppedHardwareOutput = saved?.StoppedHardwareOutput;
        if (CanResumeHardware && stoppedHardwareOutput is { } stopped)
        {
            hardwareContent = ResourceName(stopped.VideoResourceId ?? stopped.BaseResourceId);
            lastHardwareContentSerial = stopped.Serial;
            hardwareStatus = "已停止发送";
        }
        if (saved?.HardwareOutput is { } output) ResumeSavedHardwareOnLoad(output);
        if (saved != null)
        {
            foreach (var savedStopped in saved.StoppedHardwareOutputs ?? [])
                InHardwareSession(savedStopped.Serial, () => stoppedHardwareOutput = savedStopped);
            foreach (var active in saved.HardwareOutputs ?? [])
                if (active.Serial != saved.HardwareOutput?.Serial) ResumeSavedHardwareOnLoad(active);
        }
        Render();
    }
    Button Action(string title,Action action){var b=new Button{Content=title};b.Click+=(_,_)=>{try{action();SaveWorkspace();}catch(Exception ex){Notice.Text=ex.Message;}};return b;}
    TextBlock Text(string value,int size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,6,4,6)};
    ComboBox Choose(IEnumerable<string> values,string? value,Action<string> changed){var c=new ComboBox{ItemsSource=values.ToArray(),SelectedItem=value,MinWidth=110,Margin=new Thickness(4)};c.SelectionChanged+=(_,_)=>{try{if(c.SelectedItem is string s){changed(s);SaveWorkspace();}}catch(Exception ex){Notice.Text=ex.Message;}};return c;}
    void SaveWorkspace()
    {
        if (initializing || closePrepared) return;
        state.PurgeTrash(DateTimeOffset.UtcNow);
        var selectedSerial = backgroundHardwareContext ? visibleHardwareSerial : hardwareApplySerial;
        var selectedOutput = selectedSerial == null ? null : InHardwareSession(selectedSerial, CaptureHardwareOutput);
        var selectedStopped = selectedSerial == null ? null :
            InHardwareSession(selectedSerial, () => stoppedHardwareOutput);
        var snapshot = SessionStore.Capture(state, library, Math.Max(0, Scenario.SelectedIndex)) with
        {
            HardwareOutput = selectedOutput,
            StoppedHardwareOutput = selectedStopped,
            HardwareOutputs = CaptureAllHardwareOutputs(),
            StoppedHardwareOutputs = CaptureAllHardwareOutputs(stopped: true)
        };
        SessionStore.Save(sessionFile, PreserveHiddenSimulation(snapshot));
    }
    void Navigate(string name)
    {
        if (page == name || !LeaveEditor()) return;
        ChangePage(name);
    }
    void ChangePage(string name)
    {
        if (page == name) { Render(); return; }
        if (page == "主题编辑器") { themeEditorBrowser?.Dispose(); themeEditorBrowser = null; }
        pageMemory[page]=new(query,collection,contentType,category,subcategory,sort,browseRotation,showAll,PageScroll.VerticalOffset);
        Notice.Text = "";
        page=name;selecting=false;selected.Clear();
        if(pageMemory.TryGetValue(name,out var saved)){query=saved.Query;collection=saved.Collection;contentType=saved.Type;category=saved.Category;subcategory=saved.Subcategory;sort=saved.Sort;browseRotation=saved.Rotation;showAll=saved.All;}
        else{query="";collection="全部资源";contentType=category=subcategory="全部";sort=name=="在线资源"?"热门":"最近添加";browseRotation=DefaultBrowseRotation();showAll=false;}
        Render();
        PageScroll.UpdateLayout();
        PageScroll.ScrollToVerticalOffset(saved?.Offset ?? 0);
    }
    void ScenarioChanged(object sender,SelectionChangedEventArgs e){if(initializing||Scenario.SelectedIndex<0)return;if(page=="主题编辑器"&&draftDirty){initializing=true;Scenario.SelectedIndex=activeScenario;initializing=false;Notice.Text="请先保存编辑内容再切换场景。";return;}activeScenario=Scenario.SelectedIndex;SetScenario();SaveWorkspace();}
    void SetScenario()
    {
        if (!IsPlaylistDemo) return;
        pageMemory.Clear();query="";collection="全部资源";contentType=category=subcategory="全部";showAll=false;selecting=false;selected.Clear();
        if(state.Preview is {} pv)state.EndPreview(pv.Token);foreach(var p in state.Playlists)p.Pause();state.Devices.Clear();SyncLibrary();var i=Scenario.SelectedIndex;
        if(i!=4){var id=i==1?"D69":"左侧 D92";state.Devices.Add(id,new(id,i==1?new(1280,480):new(1920,462)));}
        if(i is 2 or 3)state.Devices.Add("右侧 D92",new("右侧 D92",new(1920,462)){Rotation=90});if(i==3)state.Devices.Add("桌面 D69",new("桌面 D69",new(1280,480)));
        foreach(var d in state.Devices.Values)if(state.Resources.TryGetValue("clock",out var r)&&r.Local)state.ApplyDirect(d.Id,r.Id);
        if (IsPlaylistDemo) { AddDemoPlaylists(); SeedDemoRecent(); }
        RefreshTargets();previous=watch.Elapsed;Render();
        PageScroll.ScrollToTop();
    }
    void RefreshTargets(string? preferred = null, bool keepContext = false)
    {
        refreshingTargets = true;
        try
        {
            Target.DisplayMemberPath = "Name";
            Target.SelectedValuePath = "Id";
            Target.ItemsSource = state.Devices.Values.ToArray();
            Target.SelectedValue = preferred != null && state.Devices.ContainsKey(preferred)
                ? preferred : state.Devices.Keys.FirstOrDefault();
            target = Target.SelectedValue as string;
        }
        finally { refreshingTargets = false; }
        if (keepContext) return;
        detailDeviceId = target;
        browseRotation = DefaultBrowseRotation();
    }

    void TargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingTargets) return;
        detailDeviceId = target = Target.SelectedValue as string;
        browseRotation = DefaultBrowseRotation();
        if (!initializing) Render();
    }
    void Render()
    {
        if (backgroundHardwareContext) return;
        if(initializing)return;Body.Children.Clear();
        if (page != "设备管理") managedDeviceOutput = null;
        PageScroll.Tag = page is "首页" or "轮播列表" ? "Home" : null;
        if (page == "轮播列表" && Notice.Text.StartsWith("D92 实机支持", StringComparison.Ordinal)) Notice.Text = "";
        if (page != "轮播列表")
        {
            PlaylistToast.Visibility = Visibility.Collapsed;
            DrawPlaylistUndo(false, "");
        }
        if (page is not ("资源库" or "首页" or "轮播列表" or "设备管理" or "设置" or "个人中心" or "定时切换")) Body.Children.Add(Text(page, 26));
        foreach (var button in Navigation.Children.OfType<Button>().Concat(AccountNavigation.Children.OfType<Button>()))
            button.Tag = Equals(System.Windows.Automation.AutomationProperties.GetName(button), page) ? "Selected" : null;
        lastOutputs = null;
        playlistFooterKey = null;
        OutputFooter.Visibility = page == "资源库" ? Visibility.Collapsed : Visibility.Visible;
        var home = page == "首页";
        Notice.Visibility = home ? Visibility.Collapsed : Visibility.Visible;
        DemoToggle.Visibility = Visibility.Collapsed;
        HomeDeviceToolbar.Content = null;
        refreshHomeDevicePicker = null;
        TopSearch.Visibility = home || page == "资源库" ? Visibility.Collapsed : Visibility.Visible;
        HardwareTargetButton.Visibility = home && HardwarePlaybackSelected ? Visibility.Collapsed : Visibility.Visible;
        if (home || page == "资源库")
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal };
            tools.Children.Add(BuildHomeDevicePicker());
            tools.Children.Add(HomeButton("设备管理", () => Navigate("设备管理"), "SmallButton"));
            HomeDeviceToolbar.Content = tools;
        }
        RenderHardwareStatus();
        if(page=="首页")RenderHome();else if(page=="设备管理")RenderDevices();else if(page is "资源库" or "在线资源")RenderLibrary();else if(page=="轮播列表")RenderPlaylists();else if(page=="创作中心")RenderCreator();else if(page=="主题编辑器")RenderEditor();else if(page=="定时切换")RenderSchedules();else if(page=="插件中心")RenderPlugins();else if(page=="个人中心")RenderAccount();else if(page=="设置")RenderSettings();else if(page=="完整页面规划")RenderGuide();else Body.Children.Add(Text("该模块仍待后续接入。当前为模拟设备开发版。"));RenderOutputs();
    }
    void SyncLibrary(){images.Clear();favorites.Clear();state.Resources.Clear();foreach(var entry in library.Entries){state.Resources[entry.Resource.Id]=entry.Resource;if(entry.Favorite)favorites.Add(entry.Resource.Id);if(library.MediaPath(entry) is string file)images[entry.Resource.Id]=file;}}
    string ResourceName(string? id)=>id!=null&&state.Resources.TryGetValue(id,out var r)?r.Name:"未选择内容";
    int DefaultBrowseRotation() => HardwarePlaybackSelected ? HardwareBrowseRotation :
        target == null ? 0 : state.Devices[target].Rotation;

    Size BrowseCanvas()
    {
        var geometry = D92StaticImage.Geometry(90, HardwareModel);
        var native = HardwarePlaybackSelected ? new Size(geometry.Width, geometry.Height) :
            target == null ? new Size(1920, 462) : state.Devices[target].NativeSize;
        return browseRotation % 180 == 0 ? native : new(native.Height, native.Width);
    }
    static BitmapImage Load(string path, int? width = null)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        if (width is { } pixels) bitmap.DecodePixelWidth = pixels;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    void BuildNavigation()
    {
        AddNavigationItem("首页", "M2,9 L10,2 L18,9 M4,8 V18 H8 V12 H12 V18 H16 V8");
        AddGroup("发现",
            ("在线资源", "M17,13 H5 A3,3 0 1 1 6,7 A5,5 0 0 1 15,6 A4,4 0 0 1 17,13 M10,9 V18 M7,15 L10,18 L13,15"));
        AddGroup("我的",
            ("资源库", "M2,7 V4 H9 L11,6 H18 V17 H2 Z M2,9 H18"),
            ("创作中心", "M4,12 L13,3 L17,7 L8,16 L3,17 Z M11,5 L15,9"),
            ("轮播列表", "M2,5 H18 M2,10 H10 M2,15 H10 M13,10 L18,14 L13,18 Z"),
            ("定时切换", "M18,10 A8,8 0 1 1 2,10 A8,8 0 1 1 18,10 M10,5 V10 L14,12"),
            ("插件中心", "M3,3 H9 V6 A2,2 0 1 0 13,6 V3 H17 V9 H14 A2,2 0 1 0 14,13 H17 V17 H3 Z"));
        AddGroup("设备与偏好",
            ("设备管理", "M2,4 H18 V15 H2 Z M7,18 H13 M10,15 V18"),
            ("设置", "M10,2 L17,6 V14 L10,18 L3,14 V6 Z M13,10 A3,3 0 1 1 7,10 A3,3 0 1 1 13,10"));

        AddNavigationItem("个人中心", "M14,6 A4,4 0 1 1 6,6 A4,4 0 1 1 14,6 M3,18 V16 A7,5 0 0 1 17,16 V18");

        void AddGroup(string label, params (string Name, string Icon)[] items)
        {
            var heading = new TextBlock
            {
                Text = label, FontSize = 12,
                Margin = new Thickness(24, Navigation.Children.Count == 0 ? 0 : 16, 0, 4)
            };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "NavigationLabelBrush");
            Navigation.Children.Add(heading);
            foreach (var (name, icon) in items) AddNavigationItem(name, icon);
        }

        void AddNavigationItem(string name, string icon)
        {
            var button = Action(name, () => Navigate(name));
            button.SetResourceReference(StyleProperty, "NavigationButton");
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(icon), Width = 20, Height = 20, StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(0, 0, 12, 0)
            };
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = button });
            var caption = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
            caption.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = button });
            content.Children.Add(path);
            content.Children.Add(caption);
            button.Content = content;
            (name == "个人中心" ? AccountNavigation : Navigation).Children.Add(button);
        }
    }

    void ResizeShell(object sender, SizeChangedEventArgs e) => ApplyShellWidth(e.NewSize.Width);

    void ApplyShellWidth(double width)
    {
        if (TopSearch == null || ContentPanel == null) return;
        TopSearch.Visibility = page is "首页" or "资源库" || width < 1100 ? Visibility.Collapsed : Visibility.Visible;
        TopSearch.Width = Math.Clamp(width - 740, 240, 420);
        ContentPanel.Margin = new Thickness(width < 760 ? 8 : 14);
        ResizePlaylist(width);
        if (FooterChangeLabel != null) FooterChangeLabel.Visibility = width < 900 ? Visibility.Collapsed : Visibility.Visible;
    }
    void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
    void MaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    void TopSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        Navigate("资源库");
        if (page != "资源库") return;
        query = TopSearch.Text.Trim();
        collection = "全部资源";
        contentType = category = subcategory = "全部";
        showAll = true;
        Render();
        PageScroll.ScrollToTop();
        e.Handled = true;
    }
}
