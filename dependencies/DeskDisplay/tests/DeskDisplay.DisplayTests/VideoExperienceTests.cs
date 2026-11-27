using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Library;
using Serilog;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace DeskDisplay.DisplayTests;
internal static class VideoExperienceTests
{
    public static async Task RunAsync()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var cadence = new VideoPreviewCadence();
        var uploads = 0;
        for (var i = 0; i < 300; i++) if (cadence.TryPublish(i / 30d, new object())) uploads++;
        Check(uploads <= 150 && uploads >= 100, "UI preview uploads are bounded independently of 300 device frames");
        var unchangedFrame = new object();
        Check(cadence.TryPublish(11, unchangedFrame) && !cadence.TryPublish(12, unchangedFrame) && cadence.TryPublish(12, new object()), "Unchanged paused preview bitmap is skipped while a new live clock frame can publish");
        var pixelProject=new ThemeProject{Landscape=new(){Width=64,Height=16,Layers=[new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Width=64,Height=16},new(){Kind=ThemeElementKind.Rectangle,X=5,Y=3,Width=25,Height=10,Color="#3388EE",Opacity=.5,ZIndex=1}]}};
        using(var pixelScene=new ThemeScene(pixelProject))
        {
            var data=Enumerable.Range(0,64*16).SelectMany(_=>new byte[]{30,90,200,255}).ToArray();var image=BitmapSource.Create(64,16,96,96,System.Windows.Media.PixelFormats.Bgra32,null,data,256);image.Freeze();
            using var fast=new ThemeVideoComposition(pixelScene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));
            using var reference=new ThemeVideoComposition(pixelScene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()),false);
            var a=await fast.ComposeAsync(image,default);var b=await reference.ComposeAsync(image,default);var av=new byte[data.Length];var bv=new byte[data.Length];a.CopyPixels(av,256,0);b.CopyPixels(bv,256,0);
            Check(fast.FastPathFrames==1&&av.Zip(bv).All(pair=>Math.Abs(pair.First-pair.Second)<=2),"Cached foreground premultiplied alpha blending matches the full WPF renderer without replacing video pixels");
            var document=LegacyThemeAdapter.Read(pixelProject);
            var resolved=DeskDisplay.Core.Adaptation.LayoutResolver.Resolve(document.Layouts.First(l=>l.DesignSize.Width==64),new(64,16));
            using var resolvedScene=new ThemeScene(pixelProject,resolved);
            using var resolvedComposition=new ThemeVideoComposition(resolvedScene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));
            await resolvedComposition.ComposeAsync(image,default);
            Check(resolvedComposition.FastPathFrames==1,"Device-resolved layouts retain the same cached video composition path as editor scenes");
            pixelProject.Landscape.Layers[0].Rotation=5;using var transformed=new ThemeVideoComposition(pixelScene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));await transformed.ComposeAsync(image,default);
            Check(transformed.FastPathFrames==0,"Transformed video backgrounds retain the original full scene rendering path");
        }
        Check(DeskDisplay.App.ViewModels.OutputTroubleshooting.Explain("视频文件已丢失").Destination=="Library"&&DeskDisplay.App.ViewModels.OutputTroubleshooting.Explain("检测到 CRAFT 占用").Destination=="Devices","Recovery actions distinguish missing assets from an occupied device");
        var feedback=new DeskDisplay.App.ViewModels.OutputCoordinator();feedback.Report("正在输出");var ordinary=feedback.Details;feedback.Report("视频文件已丢失",true);Check(ordinary=="正在输出"&&feedback.Details.Contains("资源库"),"Ordinary status keeps its actual tooltip while real errors provide actionable recovery guidance");
        Check(new UsbVideoPreferences(60,true).Normalize()==new UsbVideoPreferences(30,true),"Invalid saved USB video FPS normalizes without changing the opt-in adaptive preference");
        Check(JsonSerializer.Deserialize<UsbVideoPreferences>("{\"FramesPerSecond\":30,\"AutoReduceFrameRate\":true}")!.JpegQuality==90,"Existing video preferences preserve JPEG quality 90 when the new field is absent");
        Check(new UsbVideoPreferences(20,true,80).Normalize()==new UsbVideoPreferences(20,true,80)&&new UsbVideoPreferences(20,true,0).Normalize()==new UsbVideoPreferences(20,true,90),"Manual JPEG quality is retained and invalid quality falls back without changing FPS preferences");
        Check(new UsbVideoPreferences(24,true,80).Normalize()==new UsbVideoPreferences(24,true,80),"24 FPS preferences normalize without silently returning to 30 FPS");
        var twentyFour=new VideoRateController(24,true);twentyFour.Observe(0);for(var i=1;i<=100;i++)twentyFour.Observe(i/20d);
        Check(twentyFour.EffectiveFps==24,"24 FPS does not reduce when measured throughput stays above its overload threshold");
        var slow24=new VideoRateController(24,true);slow24.Observe(0);for(var i=1;i<=64;i++)slow24.Observe(i/8d);
        Check(slow24.EffectiveFps==20,"Sustained overload at 24 FPS falls back to 20 FPS");
        var fixed24=new VideoRateController(24,false);fixed24.Observe(0);for(var i=1;i<=100;i++)fixed24.Observe(i/10d);
        Check(fixed24.EffectiveFps==24,"Disabling adaptation preserves a 24 FPS request even when actual throughput is lower");
        var rate=new VideoRateController(30,true);rate.Observe(0);
        for(var i=1;i<=64;i++)rate.Observe(i/8d);
        Check(rate.EffectiveFps==24,"Warm-up followed by sustained slow windows reduces 30 FPS to 24 without changing requested FPS");
        for(var i=65;i<=104;i++)rate.Observe(i/8d);
        Check(rate.EffectiveFps==20,"Further sustained overload reduces 24 FPS to 20 without skipping that fallback");
        for(var i=105;i<=144;i++)rate.Observe(i/8d);
        Check(rate.EffectiveFps==15,"Further overload uses an intermediate 15 FPS tier");
        for(var i=145;i<=224;i++)rate.Observe(i/8d);
        Check(rate.EffectiveFps==10,"A sustainable rate does not oscillate or continue reducing automatically");
        var manual=new VideoRateController(30,false);manual.Observe(0);for(var i=1;i<=80;i++)manual.Observe(i/8d);
        Check(manual.EffectiveFps==30,"Disabled automatic reduction never silently changes the video limit");
        var paused=new VideoRateController(30,true);paused.Observe(0);for(var i=1;i<=32;i++)paused.Observe(i/8d);paused.Observe(5,true);paused.Observe(100);
        for(var i=1;i<=32;i++)paused.Observe(100+i/8d);
        Check(paused.EffectiveFps==30,"Pause or disconnect resets slow-window evidence instead of triggering a false reduction");
        var root=Path.Combine(Path.GetTempPath(),"D92-video-experience-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var list=new ContentListStore(root);var entry=new MixedEntry(Guid.NewGuid(),new("Wallpaper",Guid.NewGuid()),17,true);
            await list.SaveAsync("mixed.json",new MixedList(1,[entry]));
            Check((await list.ReadAsync("mixed.json",new MixedList(1,[]))).Items.Single().PlayToEnd,"Play-to-end persists alongside existing mixed entry duration and resource identity");
            Check(!JsonSerializer.Deserialize<MixedEntry>("{\"EntryId\":\""+Guid.NewGuid()+"\",\"Content\":{\"Kind\":\"Wallpaper\",\"Id\":\""+Guid.NewGuid()+"\"},\"Seconds\":30}")!.PlayToEnd,"Old mixed playlist entries retain timed switching when the new field is absent");
            var video=await MakeVideoAsync(root);
            var project=new ThemeProject{Name="Live video and editable clock",Landscape=new(){Width=640,Height=154,Layers=[
                new(){Kind=ThemeElementKind.Wallpaper,Path=video,IsVideo=true,Width=640,Height=154,ZIndex=0},
                new(){Kind=ThemeElementKind.Rectangle,Name="overlay",X=8,Y=8,Width=24,Height=24,Color="#00FF00",ZIndex=1},
                new(){Kind=ThemeElementKind.DigitalClock,X=160,Y=30,Width=450,Height=90,FontSize=44,ShowSeconds=true,ZIndex=2}]}};
            var original=JsonSerializer.Serialize(project);
            var device=new DeviceDescriptor("video-composition","D92",DeviceTransport.UsbHid,new("test","video-composition",IdentityConfidence.VerifiedStable),new("test","virtual-hid",1),true);
            var registry=new DeviceRegistry();registry.Replace(DeviceTransport.UsbHid,[device]);var leases=new InMemoryDeviceLeaseProvider();var target=new PullTarget();
            using var displays=new WorkspaceTests.Manager();var loopEvents=0;
            var factory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger,usbTargetFactory:_=>target,videoOptions:_=>new(24,false),videoCompleted:(_,_)=>Interlocked.Increment(ref loopEvents));
            await using var manager=new OutputSessionManager(registry,leases,factory.CreateAsync);
            var profile=new DisplayProfile("video-composition",new(640,154),new(154,640),90,new(CapabilityEvidence.Declared,CapabilityEvidence.Declared,CapabilityEvidence.Declared));
            Check((await manager.StartAsync(device.DeviceId,profile,LegacyThemeAdapter.Read(project))).Started,"A real MP4 and editable clock can prepare an isolated video theme session");
            await Until(()=>target.Frames>=8&&target.Hashes.Count>2&&Volatile.Read(ref loopEvents)>0&&target.SawRed&&target.SawBlue);
            Check(target.Hashes.Count>2&&loopEvents>0,"Video theme output advances real decoded content and reports a completed native playback cycle");
            Check(target.Width==154&&target.Height==640,"Composed video output applies transport rotation once at the configured resolution");
            Check(Math.Abs(target.LastInterval.TotalSeconds-1d/24)<.000001,"The production video-theme session passes a 24 FPS interval to its isolated transport");
            Check(target.SawGreen,"Actual JPEG output contains the separately editable shape over the video background");
            Check(target.SawRed&&target.SawBlue,"Separate background samples prove the actual video changes from red to blue beneath live overlays");
            Check(JsonSerializer.Serialize(project)==original,"Video theme composition does not replace editable layers or mutate the saved source project");
            var control=manager.GetVideoControl(device.DeviceId)??throw new Exception("Missing video control");await control.SetBackgroundPausedAsync(true);var pauseAt=target.Frames;await Until(()=>target.Frames>pauseAt+3);var heldColor=target.BackgroundColor;var heldHash=target.LastHash;
            await Task.Delay(1300);
            Check(control.BackgroundPaused&&target.BackgroundColor==heldColor&&target.LastHash!=heldHash,"Pausing only the video background freezes decoded pixels while the real clock keeps updating");
            await control.SetBackgroundPausedAsync(false);await Until(()=>target.BackgroundColor!=heldColor);
            Check(!control.BackgroundPaused,"Continuing the background resumes real native video samples without restarting the theme");
            target.Connected=false;await Until(()=>manager.Snapshot.Single().Snapshot.ActualState==OutputActualState.WaitingForDevice);var disconnectedFrames=target.Frames;await Task.Delay(150);
            Check(target.Frames==disconnectedFrames,"Simulated transport loss pauses full-frame sends without building an output queue");
            using(var blockedLease=leases.TryAcquire(device.DeviceId))Check(blockedLease is null,"A waiting video theme keeps ownership of its original device");
            target.Connected=true;await Until(()=>target.Frames>disconnectedFrames&&manager.Snapshot.Single().Snapshot.ActualState==OutputActualState.Streaming);
            Check(target.Active==1,"Simulated reconnect resumes the same video session without a competing writer");
            await manager.StopAsync(device.DeviceId);var count=target.Frames;await Task.Delay(150);
            Check(target.Frames==count&&target.Active==0,"Stopping a video theme drains pending work and releases its independent output session");
            using(var lease=leases.TryAcquire(device.DeviceId))Check(lease is not null,"Video theme stop releases device ownership for another output");
            Check((await manager.StartAsync(device.DeviceId,profile,LegacyThemeAdapter.Read(project))).Started,"A stopped video theme can prepare a fresh decoder and output session");await Until(()=>target.Frames>count);await manager.StopAsync(device.DeviceId);
            var invalid=project.Clone();invalid.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Image,IsVideo=true,Path=video,Width=20,Height=20});
            var starts=target.Starts;
            Check(!(await manager.StartAsync(device.DeviceId,profile,LegacyThemeAdapter.Read(invalid))).Started&&target.Starts==starts,"Two visible videos are rejected during preparation before any HID render target starts");
            var missing=project.Clone();missing.Landscape.Layers[0].Path=Path.Combine(root,"missing.mp4");
            Check(!(await manager.StartAsync(device.DeviceId,profile,LegacyThemeAdapter.Read(missing))).Started&&target.Starts==starts,"Missing video material fails preparation without touching device output");
            Check((await manager.StartAsync(device.DeviceId,profile with{Capabilities=profile.Capabilities with{Video=CapabilityEvidence.Unknown}},LegacyThemeAdapter.Read(project))).Code=="CapabilityUnknown","Unknown video capability stays blocked rather than being advertised as supported");
            var monitor=new DisplayInfo("video-monitor","test video monitor","video-monitor-id",0,0,640,154,false);displays.Displays=[monitor];var surface=new WindowSurface();
            var windowDevice=WindowsDeviceDiscovery.DescribeDisplays([monitor]).Single();var windowProfile=profile with{ContentToTransportRotation=0,TransportSize=new(640,154)};var document=LegacyThemeAdapter.Read(project);
            var resolved=DeskDisplay.Core.Adaptation.LayoutResolver.Resolve(document.Layouts.First(l=>l.DesignSize.Width==640),new(640,154));
            using(var ownership=leases.TryAcquire(windowDevice.DeviceId)!)
            {
                var windowFactory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger,windowFactory:(context,scene)=>new RenderWindowService(logger,scene,()=>surface,context.Ownership));
                await using var driver=await windowFactory.CreateAsync(new(windowDevice,windowProfile,document,resolved,ownership),default);var windowControl=(IVideoOutputControl)driver;
                using var cancel=new CancellationTokenSource();var run=driver.RunAsync(_=>{},cancel.Token);await Until(()=>surface.Scene is ThemeScene);
                await windowControl.SetBackgroundPausedAsync(true);var time=((ThemeScene)surface.Scene!).CurrentTime;await Task.Delay(1200);
                Check(windowControl.BackgroundPaused&&((ThemeScene)surface.Scene!).CurrentTime>time,"Windows extended-display video background can pause while scene time continues updating");
                await windowControl.SetBackgroundPausedAsync(false);Check(!windowControl.BackgroundPaused,"Windows extended-display background resumes through the same session control contract");cancel.Cancel();try{await run;}catch(OperationCanceledException){}
            }
        }
        finally{Directory.Delete(root,true);}
    }
    internal static async Task<string> MakeVideoAsync(string root,uint width=640,uint height=154)
    {
        var file=await(await StorageFolder.GetFolderFromPathAsync(root)).CreateFileAsync("background.mp4");var composition=new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,230,15,15),TimeSpan.FromSeconds(1)));composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,15,15,230),TimeSpan.FromSeconds(1)));
        var profile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);profile.Video.Width=width;profile.Video.Height=height;profile.Audio=null;
        if(await composition.RenderToFileAsync(file,MediaTrimmingPreference.Precise,profile)!=TranscodeFailureReason.None)throw new IOException("Cannot encode fixture.");composition.Clips.Clear();return file.Path;
    }
    private static async Task Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(12);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException("Video composition did not produce frames.");await Task.Delay(25);}}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private sealed class WindowSurface:DeskDisplay.Display.Rendering.IRenderSurface
    {
        public Scene? Scene;public event EventHandler? Closed{add{}remove{}}public event Action<Exception>? Faulted{add{}remove{}}public event EventHandler? TopologyInvalidated{add{}remove{}}
        public void ShowOnDisplay(DeskDisplay.Core.Models.DisplayInfo target){}public void Render(Scene scene)=>Scene=scene;public void Dispose(){}
    }
    private sealed class PullTarget:ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;public int Frames,Active,Starts,Width,Height,BackgroundColor;public string LastHash="";public bool SawGreen,SawRed,SawBlue;public volatile bool Connected=true;public HashSet<string> Hashes=new();
        public TimeSpan LastInterval;
        public async Task RunAsync(string serial,Func<CancellationToken,Task<byte[]>> render,TimeSpan interval,CancellationToken ct)
        {
            Active++;Starts++;LastInterval=interval;
            var waiting=false;var connections=1;
            try{while(!ct.IsCancellationRequested){if(!Connected){if(!waiting){StateChanged?.Invoke(new("WaitingForDevice",Frames,connections));waiting=true;}await Task.Delay(10,ct);continue;}if(waiting){waiting=false;StateChanged?.Invoke(new("Connected",Frames,++connections));}var jpeg=await render(ct);Hashes.Add(Convert.ToHexString(SHA256.HashData(jpeg)));using var stream=new MemoryStream(jpeg);var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];Width=bitmap.PixelWidth;Height=bitmap.PixelHeight;
                var converted=new FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgra32,null,0);var pixel=new byte[4];converted.CopyPixels(new System.Windows.Int32Rect(154-1-16,16,1,1),pixel,4,0);SawGreen|=pixel[1]>150&&pixel[0]<80&&pixel[2]<80;
                converted.CopyPixels(new System.Windows.Int32Rect(53,100,1,1),pixel,4,0);SawRed|=pixel[2]>150&&pixel[0]<80;SawBlue|=pixel[0]>150&&pixel[2]<80;BackgroundColor=(pixel[2]<<16)|(pixel[1]<<8)|pixel[0];LastHash=Convert.ToHexString(SHA256.HashData(jpeg));
                StateChanged?.Invoke(new("Streaming",++Frames,1));await Task.Delay(interval,ct);}}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){}finally{Active--;}
        }
    }
}
