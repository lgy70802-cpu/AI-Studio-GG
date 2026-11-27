using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace DeskDisplay.DisplayTests;
internal static class ClientVideoTests
{
    public static async Task RunAsync()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var realSerial = Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_CLIENT_REAL_SERIAL");
        var root = Path.Combine(Path.GetTempPath(), "D92-client-video-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var serial = realSerial ?? "video-test-target";
            if(realSerial is not null)
            {
                if(D92HidDevice.Enumerate().Count(d=>d.SerialNumber==realSerial)!=1)throw new IOException("Expected a unique matching D92.");
                if(System.Diagnostics.Process.GetProcessesByName("DeskDisplay.App").Length!=0 || System.Diagnostics.Process.GetProcessesByName("MiraBox Craft").Length!=0)throw new IOException("Close D92 Studio and CRAFT first.");
            }
            var initial = new AppSettings { UsbOutput = new() { SerialNumber = serial, Width = 1920, Height = 462, RotationDegrees = 90 } };
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); await settings.SaveAsync(initial);
            using var displays = new WorkspaceTests.Manager(); using var windows = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(displays, new TargetDisplayService(initial, settings, logger), logger, windows);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock); main.ConfigureOutputExperience(settings, initial);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger)); main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), windows, settings, new WorkspaceTests.Sensors(), initial, logger, false)); await main.Workspace!.RestoreAsync();
            var fake = new Target(); ID92RenderTarget target = realSerial is null ? fake : new D92HidRenderTarget(logger, experimentalHighFrameRate: true);
            var states=new List<D92OutputState>();target.StateChanged+=s=>{lock(states)states.Add(s);};
            using var usb = new UsbOutputViewModel(main, settings, initial, logger, target, realSerial is null ? () => [new("fake", serial, 1025)] : D92HidDevice.Enumerate); main.SetUsb(usb); await usb.RefreshAsync();
            var video = Path.Combine(root, "video.mp4");
            if (Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_CLIENT_SOURCE") is { } source) File.Copy(source, video);
            else
            {
                var file = await (await StorageFolder.GetFolderFromPathAsync(root)).CreateFileAsync("video.mp4"); var composition = new MediaComposition();
                composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,240,20,20), TimeSpan.FromSeconds(1))); composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,20,20,240), TimeSpan.FromSeconds(1)));
                var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga); profile.Video.Width = 384; profile.Video.Height = 92; profile.Audio = null;
                if(await composition.RenderToFileAsync(file,MediaTrimmingPreference.Precise,profile)!=TranscodeFailureReason.None)throw new IOException("Cannot generate video fixture."); composition.Clips.Clear();
            }
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "Library")); main.SetLibrary(new LibraryViewModel(main, catalog, logger)); await main.Library!.InitializeAsync(); await main.Library.ImportAsync([video]);
            var item = main.Library.AllItems.Single();
            if(realSerial is null)
            {
                var store=new ThemeProjectStore(Path.Combine(root,"Themes"));var project=new ThemeProject{Name="prior theme playlist"};await store.SaveAsync(project);
                main.SetCreator(new CreatorViewModel(main,logger,store));await main.Creator!.InitializeAsync();await main.Creator.LoadThemePlaylistAsync();main.Creator.ThemePlaylistTarget="D92 USB";main.Creator.AddThemePlaylist(project.Id);await main.Creator.StartThemePlaylistAsync();
                if(!main.Creator.IsThemeRotating)throw new InvalidOperationException("Playlist fixture did not start: "+main.Creator.ThemePlaylistStatus);
            }
            Check(item.UseLabel=="使用视频" && main.Library.ApplyCardUsbCommand.CanExecute(item),"Imported video exposes a usable direct D92 action");
            await main.Library.UseWallpaperAsync(item);
            if(realSerial is null)Check(usb.VideoFps==30 && usb.VideoFpsOptions.SequenceEqual(new[]{5,10,20,24,30}) && Math.Abs(fake.LastInterval.TotalSeconds-1d/30)<.000001,"Direct video retains the default 30 FPS interval and exposes the new 24 FPS option");
            Check(usb.IsVideoRunning && usb.Scene is VideoFrameScene && ReferenceEquals(main.HomeScene,usb.Scene),"Direct use starts native video and binds home to the actual output scene");
            if(realSerial is null)Check(!main.Creator!.IsThemeRotating,"Explicit video application stops the prior theme playlist so it cannot overwrite playback");
            Check(main.Library.RecentItems.Any(i=>i.Asset.Id==item.Asset.Id) && main.NowPlaying.Contains("视频"),"Successful first video frame records history and updates now-playing state");
            if(realSerial is null)Check(main.DescribeLegacyOutput(serial,usb.Scene) is {Kind:"Video",Path:var rememberedPath}&&rememberedPath==item.Asset.Path,"Standalone video is described as a restorable video rather than a stale image or unsupported content");
            var scene=(VideoFrameScene)usb.Scene; var before=scene.Frame; await Task.Delay(500); await Idle();
            Check(scene.Frame is WpfSceneImage {Bitmap.IsFrozen:true} && !ReferenceEquals(before,scene.Frame),"Home receives advancing frozen frames from the same output decoder");
            usb.VideoPauseCommand.Execute(null); await Wait(()=>usb.IsVideoPaused); await Task.Delay(300); var paused=scene.Frame;
            await Task.Delay(350); Check(ReferenceEquals(((WpfSceneImage)paused!).Bitmap,((WpfSceneImage)scene.Frame!).Bitmap),"Pausing video freezes the real home preview frame");
            usb.VideoPauseCommand.Execute(null);await Wait(()=>!usb.IsVideoPaused);await Task.Delay(400);Check(!ReferenceEquals(((WpfSceneImage)paused!).Bitmap,((WpfSceneImage)scene.Frame!).Bitmap),"Continuing video resumes native frame delivery");
            var calls=fake.Starts; Check(!await usb.PlayVideoAsync(Path.Combine(root,"missing.mp4")) && usb.IsVideoRunning && ReferenceEquals(usb.Scene,scene) && fake.Starts==calls,"Missing replacement video preserves the already-running video session");
            var corrupt=Path.Combine(root,"corrupt.mp4");await File.WriteAllTextAsync(corrupt,"invalid MP4");
            Check(!await usb.PlayVideoAsync(corrupt)&&usb.IsVideoRunning&&ReferenceEquals(usb.Scene,scene),"Corrupt replacement video preserves current playback before device handoff");
            var pixels=BitmapSource.Create(32,8,96,96,PixelFormats.Bgra32,null,Enumerable.Range(0,32*8).SelectMany(_=>new byte[]{180,110,35,255}).ToArray(),128);pixels.Freeze();var image=new ImageScene("switch.png",new WpfSceneImage(pixels,32,8),ImageDisplayMode.Uniform);
            await usb.ApplySceneAsync(image);Check(usb.IsRunning&&!usb.IsVideoRunning&&ReferenceEquals(usb.Scene,image),"Applying a static wallpaper releases video and replaces the same device output");
            await main.Library.UseWallpaperAsync(item);Check(usb.IsVideoRunning,"Video can replace a running static wallpaper without manually stopping output");
            if(realSerial is not null)
            {
                var seconds=int.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_CLIENT_SECONDS"),out var value)?Math.Clamp(value,30,1800):120;
                var evidence=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_CLIENT_EVIDENCE")??throw new IOException("Missing evidence path.");
                if(File.Exists(evidence)||File.Exists(evidence+".progress.json"))throw new IOException("Evidence already exists; choose a new path.");
                long completedFrames=0;
                var measuredTarget=(D92HidRenderTarget)target;
                void Completed(D92FrameTiming timing)=>Interlocked.Increment(ref completedFrames);
                measuredTarget.FrameCompleted+=Completed;
                var measurement=System.Diagnostics.Stopwatch.StartNew();
                using var process=System.Diagnostics.Process.GetCurrentProcess();
                var cpuBefore=process.TotalProcessorTime;
                var samples=new List<RuntimeSample>();
                var complete=false;string? error=null;
                void Sample()
                {
                    process.Refresh();var at=measurement.Elapsed.TotalSeconds;var frames=Interlocked.Read(ref completedFrames);var prior=samples.LastOrDefault();
                    samples.Add(new(at,frames,prior is null?0:(frames-prior.CompletedFrames)/(at-prior.Seconds),process.WorkingSet64,process.PrivateMemorySize64,GC.GetTotalMemory(false),process.HandleCount,(process.TotalProcessorTime-cpuBefore).TotalSeconds));
                }
                async Task Save(bool progress)
                {
                    List<D92OutputState> snapshot;lock(states)snapshot=states.ToList();
                    var frames=Interlocked.Read(ref completedFrames);
                    await File.WriteAllTextAsync(evidence+(progress?".progress.json":""),JsonSerializer.Serialize(new{Serial=realSerial,Seconds=seconds,RequestedFps=usb.VideoFps,CompletedFrames=frames,MeasuredSeconds=measurement.Elapsed.TotalSeconds,CompletionFps=frames/Math.Max(.001,measurement.Elapsed.TotalSeconds),CompletedFullDuration=complete,Error=error,State=usb.Status,ReconnectObserved=snapshot.Any(s=>s.Status=="WaitingForDevice")&&snapshot.Any(s=>s.Connections>1),Samples=samples,States=snapshot},new JsonSerializerOptions{WriteIndented=true}));
                }
                try
                {
                    Sample();await Save(true);
                    for(var i=0;i<seconds;i++)
                    {
                        await Task.Delay(1000);
                        if(!usb.IsRunning)throw new IOException("Client video stopped during runtime check: "+usb.Status);
                        if((i+1)%10==0){Sample();await Save(true);Console.WriteLine($"VIDEO_RUNTIME: {i+1}/{seconds}s; frames={Interlocked.Read(ref completedFrames)}; {usb.Status}");}
                    }
                    complete=true;
                }
                catch(Exception failure){error=failure.ToString();throw;}
                finally {measurement.Stop();measuredTarget.FrameCompleted-=Completed;Sample();await Save(false);}
            }
            await usb.StopAsync();Check(!usb.IsRunning&&!usb.IsVideoRunning&&usb.CanEdit,"Stopping video awaits decoder and HID cleanup");
            if(realSerial is null)
            {
                foreach(var fps in new[]{5,10,20})
                {
                    usb.VideoFps=fps;Check(await usb.PlayVideoAsync(video)&&Math.Abs(fake.LastInterval.TotalSeconds-1d/fps)<.000001,$"Video fallback {fps} FPS uses its own output interval");await usb.StopAsync();
                }
                usb.VideoFps=30;
                fake.FailNext=true;Check(!await usb.PlayVideoAsync(video)&&main.Output.HasRetry,"Failed first video transfer exposes an explicit retry without reporting success");
                await main.Output.RetryAsync();Check(usb.IsVideoRunning,"Explicit retry reopens the video decoder and starts the selected device");await usb.StopAsync();
                fake.Hold=true;var pending=usb.PlayVideoAsync(video);await Wait(()=>usb.IsRunning&&fake.Active==1);await usb.StopAsync();
                Check(!await pending&&!usb.IsRunning&&usb.CanEdit,"Stopping during first-frame preparation cancels the pending video without restarting it");
                Check(fake.Peak==1,"Static and video replacement never overlaps HID output sessions");
                fake.Hold=false;
                var moved=video+".temporarily-missing";var originalMediaHash=SHA256.HashData(await File.ReadAllBytesAsync(video));File.Move(video,moved);
                await main.Library.UseWallpaperAsync(item);
                Check(!usb.IsRunning&&main.Output.HasRetry&&main.Output.Message.Contains(item.Name),"Missing resource use preserves output state and exposes a named explicit repair retry");
                File.Move(moved,video);await main.Output.RetryAsync();
                Check(usb.IsVideoRunning&&originalMediaHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(video))),"Repairing the original file lets explicit retry use the same resource without modifying its bytes");
                await usb.StopAsync();
                usb.VideoFps=10;usb.AutoReduceVideoFps=true;usb.VideoJpegQuality=80;await usb.VideoPreferencesPending;
                usb.SelectedSerial="second-video-device";Check(usb.VideoFps==30&&!usb.AutoReduceVideoFps&&usb.VideoJpegQuality==90,"A different device receives independent default video preferences");
                usb.VideoFps=24;await usb.VideoPreferencesPending;usb.SelectedSerial=serial;
                Check(usb.VideoFps==10&&usb.AutoReduceVideoFps&&usb.VideoJpegQuality==80,"Returning to a device restores its own FPS, quality and adaptive preference");
                var reloaded=await settings.LoadAsync();
                Check(reloaded.UsbVideoPreferences[serial]==new UsbVideoPreferences(10,true,80)&&reloaded.UsbVideoPreferences["second-video-device"].FramesPerSecond==24&&reloaded.UsbVideoPreferences["second-video-device"].JpegQuality==90&&reloaded.UsbOutput?.SerialNumber==serial,"Video preferences including 24 FPS survive disk reload without overwriting another device or output target");
                var registry=new DeskDisplay.Core.Devices.DeviceRegistry();
                usb.VideoMode="画质优先";await usb.VideoPreferencesPending;
                Check(!usb.IsCustomVideoMode&&usb.GetVideoPreferences(serial).FramesPerSecond==24&&usb.VideoFps==10,"Quality UI preset preserves the device's stored custom FPS");
                Check(await usb.PlayVideoAsync(video)&&Math.Abs(fake.LastInterval.TotalSeconds-1d/24)<.000001,"Standalone quality preset reaches the actual 24 FPS output session");
                await usb.StopAsync();usb.VideoMode="流畅优先";await usb.VideoPreferencesPending;
                Check(await usb.PlayVideoAsync(video)&&Math.Abs(fake.LastInterval.TotalSeconds-1d/30)<.000001,"Standalone smooth preset reaches the actual 30 FPS output session");
                await usb.StopAsync();usb.SelectedSerial="second-video-device";usb.SelectedSerial=serial;
                Check(usb.VideoMode=="流畅优先"&&(await settings.LoadAsync()).UsbVideoPreferences[serial].Mode==VideoPlaybackMode.Smooth,"Playback mode persists per device and survives selection changes");
                usb.VideoMode="自定义";await usb.VideoPreferencesPending;
                Check(usb.VideoFps==10&&usb.VideoJpegQuality==80&&usb.GetVideoPreferences(serial).FramesPerSecond==10,"Returning to custom mode restores unmodified manual settings");
                var descriptor=new DeskDisplay.Core.Devices.DeviceDescriptor("video-restore-target","D92",DeskDisplay.Core.Devices.DeviceTransport.UsbHid,new("test",serial,DeskDisplay.Core.Devices.IdentityConfidence.VerifiedStable),new("test","offline",1),true);
                registry.Replace(DeskDisplay.Core.Devices.DeviceTransport.UsbHid,[descriptor]);
                await using var sessions=new DeskDisplay.Core.Output.OutputSessionManager(registry,new DeskDisplay.Core.Output.InMemoryDeviceLeaseProvider(),(_,_)=>throw new InvalidOperationException("Standalone restore must use its existing USB path."));
                using var managed=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(root,"devices.json")),()=>[],()=>[],_=>Task.CompletedTask,logger){Coordinator=main.Output};
                await managed.InitializeAsync();devices.AttachManaged(managed);var card=managed.Cards.Single();card.Width="1920";card.Height="462";card.Confirmed=true;
                await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new(serial,"Video",video,Paused:true));
                Check(usb.IsVideoRunning&&usb.IsVideoPaused&&usb.VideoFps==10,"Startup video restore reopens the real decoder using saved per-device FPS and pause state");
                await usb.StopAsync();var starts=fake.Starts;
                try{await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new(serial,"Video",Path.Combine(root,"gone.mp4")));throw new Exception("Missing startup video accepted");}catch(InvalidOperationException error){Check(error.Message.Contains("丢失")&&fake.Starts==starts,"Missing startup video is reported before replacing or starting output");}
                registry.Replace(DeskDisplay.Core.Devices.DeviceTransport.UsbHid,[]);await Wait(()=>!card.Connected);
                try{await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new(serial,"Video",video));throw new Exception("Disconnected video target accepted");}catch(InvalidOperationException){Check(fake.Starts==starts,"A disconnected startup target never redirects video to another device");}
                await managed.PrepareForCloseAsync();
            }
        }
        finally { Directory.Delete(root,true); }
    }
    private static Task Idle()=>Dispatcher.CurrentDispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle).Task;
    private sealed record RuntimeSample(double Seconds,long CompletedFrames,double WindowFps,long WorkingSetBytes,long PrivateMemoryBytes,long ManagedHeapBytes,int HandleCount,double CpuSeconds);
    private static async Task Wait(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(8);while(!ready()&&DateTime.UtcNow<until)await Task.Delay(30);if(!ready())throw new TimeoutException("Video state did not settle.");}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private sealed class Target:ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged; public int Starts,Active,Peak;public bool Hold,FailNext;public TimeSpan LastInterval;
        public async Task RunAsync(string serial,Func<CancellationToken,Task<byte[]>> render,TimeSpan interval,CancellationToken token)
        {
            Starts++;Active++;Peak=Math.Max(Peak,Active);LastInterval=interval;long frames=0;
            try{while(!token.IsCancellationRequested){var bytes=await render(token);if(bytes.Length==0)throw new IOException("Empty frame");if(FailNext){FailNext=false;throw new IOException("Simulated first frame failure");}if(Hold)await Task.Delay(Timeout.Infinite,token);StateChanged?.Invoke(new("Streaming",++frames,1));await Task.Delay(interval,token);}}
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            finally{Active--;}
        }
    }
}
