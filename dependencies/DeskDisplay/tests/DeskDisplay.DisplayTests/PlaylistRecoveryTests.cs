using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Services;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class PlaylistRecoveryTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root=Path.Combine(Path.GetTempPath(),"D92-playlist-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var initial=new AppSettings{RestoreD92OnStartup=true,UsbOutput=new(){SerialNumber="playlist-device",Width=1920,Height=462}};
            var settings=new JsonSettingsService(logger,Path.Combine(root,"settings.json"));await settings.SaveAsync(initial);
            using var displays=new WorkspaceTests.Manager();using var windows=new WorkspaceTests.Output();
            using var devices=new DevicesViewModel(displays,new TargetDisplayService(initial,settings,logger),logger,windows);
            var clock=new ClockScene();using var main=new MainViewModel(devices,clock);main.ConfigureOutputExperience(settings,initial);
            var transport=new Transport();using var usb=new UsbOutputViewModel(main,settings,initial,logger,transport,()=>[new("offline","playlist-device",1025)]);main.SetUsb(usb);await usb.RefreshAsync();
            main.SetImageEditor(new ImageSceneViewModel(devices,settings,initial,clock,main.PreviewScene,logger));
            main.SetVideoEditor(new VideoSceneViewModel(main,new WorkspaceTests.Playback(),logger));
            main.SetWorkspace(new WorkspaceViewModel(main,clock,new PerformanceScene(),windows,settings,new WorkspaceTests.Sensors(),initial,logger,false));
            await main.Workspace!.RestoreAsync();
            var catalog=new WallpaperCatalog(logger,Path.Combine(root,"library"));main.SetLibrary(new LibraryViewModel(main,catalog,logger));await main.Library!.InitializeAsync();
            var store=new ThemeProjectStore(Path.Combine(root,"themes"));var a=new ThemeProject{Name="playlist A"};a.Landscape.Background="#AD2211";a.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Text,Text="A",FontSize=180,Width=500,Height=300});
            var b=a.Clone();b.Id=Guid.NewGuid();b.Name="playlist B";b.Landscape.Background="#123E9A";b.Landscape.Layers[0].Text="B";
            await store.SaveAsync(a);await store.SaveAsync(b);
            var creator=new CreatorViewModel(main,logger,store);main.SetCreator(creator);await creator.InitializeAsync();await creator.OpenProjectAsync(new(){Name="untouched editor"});creator.Add(ThemeElementKind.Text);creator.Text="unsaved";var editing=JsonSerializer.Serialize(creator.Project);
            var registry=new DeviceRegistry();var descriptor=new DeviceDescriptor("playlist-target","D92",DeviceTransport.UsbHid,new("test","playlist-device",IdentityConfidence.VerifiedStable),new("offline","offline",1),true);registry.Replace(DeviceTransport.UsbHid,[descriptor]);
            await using var sessions=new OutputSessionManager(registry,new InMemoryDeviceLeaseProvider(),(_,_)=>ValueTask.FromResult<IOutputSessionDriver>(new Driver()));
            using var managed=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(root,"devices.json")),()=>[],()=>[],_=>Task.CompletedTask,logger){Coordinator=main.Output};
            var factory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger);managed.PreviewFactory=factory.CreatePreviewAsync;devices.AttachManaged(managed);await managed.InitializeAsync();var card=managed.Cards.Single();card.Width="1920";card.Height="462";card.Confirmed=true;
            usb.ReleaseManagedOutputAsync=managed.ReleaseForLegacyUsbAsync;
            usb.ContentAccepted=(serial,scene)=>{main.RememberLegacyOutput(serial,scene);return main.RuntimePending;};
            managed.HasLegacyOwnedOutput=_=>usb.OwnsOutputForSerial("playlist-device");
            managed.ReleaseLegacyOwnedOutputAsync=async _=>{creator.StopThemePlaylist();main.Library.StopRotation();return await usb.StopOwnedSerialAsync("playlist-device");};
            await creator.LoadThemePlaylistAsync();creator.AddThemePlaylist(a.Id);creator.AddThemePlaylist(b.Id);creator.ThemePlaylistTarget="D92 USB";
            await creator.StartThemePlaylistAsync(a.Id,true);await Until(()=>transport.Last is not null);
            var first=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(transport.Last!));
            await creator.AdvanceThemePlaylistAsync();await Until(()=>transport.Last is not null && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(transport.Last))!=first);
            Check(usb.Scene is ThemeScene theme && theme.Project.Id==b.Id && creator.IsThemePaused,"USB theme playlist explicitly switches the actual render source while automatic rotation is paused");
            Check(JsonSerializer.Serialize(creator.Project)==editing && creator.IsDirty,"Playlist switching preserves the unsaved editing project");
            await main.RuntimePending;var themeResume=main.RememberedContents.Single();
            Check(themeResume is {Kind:"ThemePlaylist",Paused:true} && themeResume.ThemeId==b.Id,"Theme playlist records the actual current theme and paused mode");
            creator.StopThemePlaylist();await usb.StopAsync();
            await D92StartupRestore.RestoreAsync(main,managed,themeResume);
            Check(creator.IsThemeRotating && creator.IsThemePaused && creator.ThemeCurrentName=="playlist B" && usb.Scene is ThemeScene resumed && resumed.Project.Id==b.Id,"Startup restore resumes the saved USB theme entry and pause state without opening an editor");
            await main.CaptureOutputShutdownAsync();creator.StopThemePlaylist();await usb.StopAsync();await main.RuntimePending;
            Check((await settings.LoadAsync()).D92ResumeContents.Single().Kind=="ThemePlaylist","Normal shutdown retains playlist resume intent despite stopping timers and transport");main.CancelOutputShutdown();

            var red=Path.Combine(root,"red.png");var blue=Path.Combine(root,"blue.png");WriteImage(red,Colors.Red);WriteImage(blue,Colors.Blue);
            await main.Library.ImportAsync([red,blue]);var redAsset=main.Library.AllItems.Single(i=>i.Asset.Path==red).Asset;var blueAsset=main.Library.AllItems.Single(i=>i.Asset.Path==blue).Asset;
            main.Library.AddCarouselItems([redAsset.Id,blueAsset.Id]);main.Library.RotationTarget="D92 USB";
            await main.Library.StartRotationAsync(blueAsset.Id,true);await main.RuntimePending;
            Check(main.Library.IsRotating && main.Library.IsCarouselPaused && usb.Scene is ImageScene image && image.Path==blue,"Wallpaper playlist can resume at its saved entry with automatic switching paused");
            var imageResume=main.RememberedContents.Single();Check(imageResume is {Kind:"WallpaperPlaylist",Paused:true} && imageResume.LastAssetId==blueAsset.Id,"Wallpaper playlist records actual asset identity rather than a disposable thumbnail path");
            main.Library.StopRotation();await usb.StopAsync();File.Delete(blue);
            await D92StartupRestore.RestoreAsync(main,managed,imageResume);
            Check(main.Library.IsRotating && main.Library.IsCarouselPaused && usb.Scene is ImageScene remaining && remaining.Path==red,"Restored wallpaper playlist skips a missing saved entry and displays an available item");
            await main.Library.UseWallpaperAsync(main.Library.AllItems.Single(i=>i.Asset.Id==redAsset.Id));
            Check(!main.Library.IsRotating && main.Library.IsGallery && usb.Scene is ImageScene applied && applied.Path==red,"Direct wallpaper use replaces owned output without opening details or a preview dialog");
            main.RefreshNowPlaying();Check(main.NowPlaying.Contains("正在输出") && main.NowPlaying.Contains("壁纸") && main.NowPlaying.Contains("red"),"Home output summary describes the active wallpaper rather than a merely browsed scene");
            main.PreviewScene(new ClockScene());main.RefreshNowPlaying();Check(main.NowPlaying.Contains("red") && usb.Scene is ImageScene,"Browsing another local scene does not relabel the active device output");
            var carousel=new DeskDisplay.App.Views.CarouselManagementView{DataContext=main};
            Check(carousel.Content is System.Windows.Controls.TabControl {Items.Count:3},"One carousel page contains wallpaper and theme management without replacing their output engines");
            var themePanel=DeskDisplay.App.Views.CreatorThemePlaylist.CreatePanel(creator);themePanel.Measure(new(1100,700));themePanel.Arrange(new(0,0,1100,700));themePanel.UpdateLayout();
            Check(FindButtons(themePanel).Any(b=>Equals(b.Content,"添加已保存主题")) && FindButtons(themePanel).Any(b=>Equals(b.Content,"下一项")),"Integrated theme playlist creates real add, order and playback controls against saved projects");
            Check(new HomeRecentEntry("wallpaper",main.Library.AllItems.First(),DateTimeOffset.Now).UseLabel=="使用壁纸","Home static wallpaper action keeps its explicit direct-use label");
            var libraryView=new DeskDisplay.App.Views.LibraryView{DataContext=main,Width=1100,Height=800};libraryView.Measure(new(1100,800));libraryView.Arrange(new(0,0,1100,800));libraryView.UpdateLayout();
            Check(FindButtons(libraryView).Any(b=>Equals(b.Content,"使用壁纸") && b.Visibility==System.Windows.Visibility.Visible && b.IsEnabled),"Library wallpaper cards expose an enabled permanent use button without hovering");
            Check(main.Library.RefreshCount>0 && main.Library.LastRefreshMilliseconds>=0,"Resource filtering reports bounded aggregate timing instead of retaining frame traces");

            using var renderer=new SceneJpegRenderer();var staticScene=(ImageScene)usb.Scene;
            var watch=System.Diagnostics.Stopwatch.StartNew();var jpeg=renderer.Render(staticScene,0);var cold=watch.Elapsed.TotalMilliseconds;jpeg[0]=0;
            var cached=renderer.Render(staticScene,0);Check(renderer.LastRenderWasCached && cached[0]==0xFF,"Frozen static image reuses encoded JPEG without exposing the cached array to mutation");
            watch.Restart();for(var i=0;i<32;i++)renderer.Render(staticScene,0);var cachedAverage=watch.Elapsed.TotalMilliseconds/32;
            renderer.Render(staticScene,90);Check(!renderer.LastRenderWasCached,"Changing transport rotation invalidates the static JPEG cache");
            renderer.Render(staticScene,90,89);Check(!renderer.LastRenderWasCached,"Changing JPEG quality invalidates the static cache");
            staticScene.SetCanvasSize(800,240);renderer.Render(staticScene,90,89);Check(!renderer.LastRenderWasCached,"Changing actual output dimensions invalidates the static cache");
            using var live=new ThemeScene(new ThemeProject());live.Project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,ShowSeconds=true,FontSize=100,Width=800,Height=250});live.Tick(DateTimeOffset.Now);var now=renderer.Render(live,0);live.Tick(live.CurrentTime.AddSeconds(1));var later=renderer.Render(live,0);
            Check(!renderer.LastRenderWasCached && !now.SequenceEqual(later),"Editable clock themes continue to render changing seconds and never use static image caching");
            Console.WriteLine($"METRIC: frozen wallpaper JPEG cold={cold:F3}ms; 32 cached calls average={cachedAverage:F4}ms; cache hits={RenderPerformance.CacheHits}. Fake transport only, no HID throughput claim.");
            var attempts=0;main.Output.SetRetry(()=>main.Output.RunAsync("retry",()=>{attempts++;return Task.CompletedTask;}));await main.Output.RetryAsync();
            Check(attempts==1 && !main.Output.HasRetry,"Retry is explicit, executes once and clears after a successful new transition");
            main.Output.SetRetry(()=>Task.FromException(new IOException("retry cause")));await main.Output.RetryAsync();
            Check(main.Output.HasRetry && main.Output.IsError && main.Output.Message.Contains("retry cause"),"Failed retry retains its action and reports the concrete reason without an automatic loop");
            await main.Output.RunAsync("new choice",()=>Task.CompletedTask);Check(!main.Output.HasRetry,"A new user transition clears retry for obsolete content");
            var stressCatalog=new WallpaperCatalog(logger,Path.Combine(root,"stress-library"));
            await stressCatalog.SaveAsync(new(){Assets=Enumerable.Range(0,5000).Select(i=>new WallpaperAsset{Path=Path.Combine(root,"fixture-"+i+".png"),Name="entry-"+i.ToString("D5"),Category="test",AddedAt=DateTimeOffset.UnixEpoch.AddSeconds(i)}).ToList()});
            using(var stress=new LibraryViewModel(main,stressCatalog,logger))
            {
                await stress.InitializeAsync();stress.SelectedTab="我的资源";stress.SelectedResourceType="壁纸";
                for(var filter=0;filter<10;filter++){stress.Search="entry-049";stress.Search="";}
                stress.LoadMore();stress.LoadMore();
                Check(stress.AllItems.Count==5000 && stress.VisibleItems.Count<=36 && stress.CanNextPage,"Five thousand metadata fixtures retain bounded visible cards and working pagination while filtering");
                Console.WriteLine($"METRIC: 5000 catalog metadata fixtures; last refresh={stress.LastRefreshMilliseconds:F2}ms; max={stress.MaxRefreshMilliseconds:F2}ms. Excludes image decoding and HID.");
                await stress.PrepareForCloseAsync();
            }
            await main.StopHomeOutputAsync();Check(!usb.IsRunning && main.NowPlaying.Contains("尚未输出"),"Home stop releases its actual USB output and labels the remaining picture as a local preview");
            main.Library.StopRotation();await usb.PrepareForCloseAsync();await main.Library.PrepareForCloseAsync();await main.Workspace!.PrepareForCloseAsync();await main.Images!.PrepareForCloseAsync();await managed.PrepareForCloseAsync();await main.RuntimePending;
        }
        finally{Directory.Delete(root,true);}
    }
    private static void WriteImage(string path,Color color)
    {var pixels=new byte[16*4*4];for(var i=0;i<pixels.Length;i+=4){pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=255;}var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16,4,96,96,PixelFormats.Bgra32,null,pixels,64)));using var file=File.Create(path);encoder.Save(file);}
    private static async Task Until(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(8);while(!ready()){if(DateTime.UtcNow>until)throw new TimeoutException("Offline frame did not advance");await Task.Delay(20);}}
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
    private static IEnumerable<System.Windows.Controls.Button> FindButtons(System.Windows.DependencyObject parent)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is System.Windows.Controls.Button button)yield return button;foreach(var nested in FindButtons(child))yield return nested;}}
    private sealed class Driver:IOutputSessionDriver
    {public async Task RunAsync(Action<OutputActualState> report,CancellationToken ct){report(OutputActualState.Streaming);await Task.Delay(Timeout.InfiniteTimeSpan,ct);}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    private sealed class Transport:ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;public byte[]? Last;
        public async Task RunAsync(string serialNumber,Func<CancellationToken,Task<byte[]>> render,TimeSpan interval,CancellationToken ct)
        {long frames=0;StateChanged?.Invoke(new("Connected",0,1));while(!ct.IsCancellationRequested){Last=await render(ct);StateChanged?.Invoke(new("Streaming",++frames,1));await Task.Delay(40,ct);}}
    }
}
