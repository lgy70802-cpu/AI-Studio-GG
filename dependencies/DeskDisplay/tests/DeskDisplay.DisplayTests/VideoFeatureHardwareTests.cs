using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.DisplayTests;
/// <summary>Explicit opt-in only. Real HID, no writes to the user's settings/library.</summary>
internal static class VideoFeatureHardwareTests
{
    public static async Task RunAsync()
    {
        var serial=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_SERIAL")??throw new IOException("Explicit serial required.");
        var video=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_SOURCE")??throw new IOException("Explicit MP4 source required.");
        var directory=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_EVIDENCE")??throw new IOException("New evidence directory required.");
        if(Directory.Exists(directory))throw new IOException("Evidence directory already exists.");
        foreach(var name in new[]{"DeskDisplay.App","MiraBox Craft","CRAFT"})if(Process.GetProcessesByName(name).Length>0)throw new IOException("Close competing application: "+name);
        var physical=D92HidDevice.Enumerate().Where(d=>d.SerialNumber==serial).ToArray();if(physical.Length!=1)throw new IOException("Unique connected target required.");
        var settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DeskDisplay","settings.json");
        var saved=JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(settingsPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})?.UsbOutput;
        if(saved is null||saved.SerialNumber!=serial)throw new IOException("Use the saved and verified matching USB geometry.");
        Directory.CreateDirectory(directory);using var logger=new LoggerConfiguration().WriteTo.Sink(new PerformanceSink()).CreateLogger();using var displays=new WorkspaceTests.Manager();
        var device=WindowsDeviceDiscovery.DescribeUsb(physical).Single();var registry=new DeviceRegistry();registry.Replace(DeviceTransport.UsbHid,[device]);
        var jpegQuality=int.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_QUALITY"),out var requestedQuality)?requestedQuality:90;
        if(jpegQuality is <30 or >95)throw new IOException("JPEG quality must be 30..95.");
        var requestedFps=int.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_FPS"),out var selectedFps)?selectedFps:30;
        if(!UsbVideoPreferences.IsValidRate(requestedFps))throw new IOException("Invalid requested video FPS.");
        var autoReduce=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_AUTO_REDUCE")!="0";
        var modeText=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_MODE");
        var mode=VideoPlaybackMode.Custom;
        if(modeText is not null&&(!Enum.TryParse(modeText,true,out mode)||!Enum.IsDefined(mode)))throw new IOException("Unknown video mode.");
        var videoPreferences=new UsbVideoPreferences(requestedFps,autoReduce,jpegQuality){Mode=mode}.Resolve();
        requestedFps=videoPreferences.FramesPerSecond;autoReduce=videoPreferences.AutoReduceFrameRate;jpegQuality=videoPreferences.JpegQuality;
        var states=new List<D92OutputState>();var measurements=new Dictionary<Guid,Totals>();var rates=new List<VideoThroughput>();var gate=new object();var stages=new List<object>();
        var completionClock=Stopwatch.StartNew();Guid? measuredTheme=null;double? lastCompletionMs=null;var intervals=new List<double>();
        var diagnosticFrames=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_DIAGNOSTICS")=="1";VideoSlowFrameRecorder? slowRecorder=null;
        ThemeScene? previewScene=null;
        var factory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger,usbTargetFactory:context=>
        {
            var target=new D92HidRenderTarget(logger,context.Ownership,confirmedDevice:context.Device,experimentalHighFrameRate:true);
            target.StateChanged+=state=>{lock(gate)states.Add(state);};target.FrameCompleted+=frame=>{lock(gate){if(!measurements.TryGetValue(context.Theme.ThemeId,out var totals))measurements[context.Theme.ThemeId]=totals=new();totals.Frames++;totals.Bytes+=frame.JpegBytes;totals.WriteMs+=frame.WriteMilliseconds;
                if(measuredTheme==context.Theme.ThemeId){var now=completionClock.Elapsed.TotalMilliseconds;if(lastCompletionMs is {} prior&&intervals.Count<60000)intervals.Add(now-prior);lastCompletionMs=now;}
            }};return target;
        },videoOptions:_=>videoPreferences,videoMetrics:(_,rate)=>{lock(gate)rates.Add(rate);},videoPreview:(_,_,frame)=>previewScene?.SetOutputFrame(new DeskDisplay.Display.DataProviders.WpfSceneImage(frame,frame.PixelWidth,frame.PixelHeight)),videoFrameDiagnostics:diagnosticFrames?frame=>{lock(gate){if(measuredTheme==frame.ThemeId)slowRecorder?.Observe(completionClock.Elapsed.TotalMilliseconds,frame.Sample,frame.Transport,GC.GetTotalPauseDuration().TotalMilliseconds,GC.CollectionCount(2),frame.PreparedLookAhead,frame.LookAheadPending);}}:null);
        await using var sessions=new OutputSessionManager(registry,WindowsDeviceLeaseProvider.Shared,factory.CreateAsync);
        var profile=new DisplayProfile("saved-video-feature-hardware",new(saved.Width,saved.Height),saved.RotationDegrees is 90 or 270?new(saved.Height,saved.Width):new(saved.Width,saved.Height),saved.RotationDegrees,new(CapabilityEvidence.Declared,CapabilityEvidence.Declared,CapabilityEvidence.Declared));
        var still=new ThemeProject{Name="静态切换验收",Orientation=saved.Width>=saved.Height?ThemeOrientation.Landscape:ThemeOrientation.Portrait};still.ActiveLayout.Width=saved.Width;still.ActiveLayout.Height=saved.Height;still.ActiveLayout.Background="#245AA0";
        still.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.Text,Text="D92 STATIC",X=60,Y=80,Width=saved.Width-120,Height=180,FontSize=90,Color="#FFFFFF"});
        var composed=new ThemeProject{Name="视频背景与实时时钟",Orientation=saved.Width>=saved.Height?ThemeOrientation.Landscape:ThemeOrientation.Portrait};composed.ActiveLayout.Width=saved.Width;composed.ActiveLayout.Height=saved.Height;
        composed.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Path=video,Width=saved.Width,Height=saved.Height});
        composed.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.Rectangle,X=saved.Width-820,Y=saved.Height-170,Width=800,Height=150,Color="#172A46",Opacity=.85,ZIndex=1});
        composed.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,Name="真实系统时钟",Format="HH:mm",ShowSeconds=true,X=saved.Width-800,Y=saved.Height-160,Width=760,Height=130,FontSize=96,Color="#FFFFFF",ZIndex=2});
        composed.ActiveLayout.Layers.Add(new(){Kind=ThemeElementKind.Text,Text="VIDEO + LIVE CLOCK",X=36,Y=saved.Height-70,Width=700,Height=60,FontSize=38,Color="#FFFFFF",ZIndex=3});
        var success=false;string? error=null;
        using var acceptanceStop=new CancellationTokenSource();
        System.Windows.Window? previewWindow=null;DeskDisplay.Display.Views.SceneView? previewView=null;
        var activatePreview=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_ACTIVATE")=="1";
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_WINDOW")=="1")
        {
            previewView=new DeskDisplay.Display.Views.SceneView();
            previewWindow=new(){Title="D92 视频主题长测（隔离验收，关闭取消测试）",Width=1100,Height=350,ShowActivated=activatePreview,Content=previewView};
            previewWindow.Closed+=(_,_)=>acceptanceStop.Cancel();previewWindow.Show();
        }
        var videoSeconds=int.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_SECONDS"),out var duration)?Math.Clamp(duration,10,1800):30;
        var cycles=int.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_CYCLES"),out var cycleCount)?Math.Clamp(cycleCount,1,6):1;
        var currentCycle=0;
        var testPause=Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_FEATURE_PAUSE")=="1";
        var pulseWatch=Stopwatch.StartNew();var lastPulse=pulseWatch.Elapsed.TotalMilliseconds;var delays=new List<double>();
        var pulse=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};pulse.Tick+=(_,_)=>{var now=pulseWatch.Elapsed.TotalMilliseconds;if(delays.Count<10000)delays.Add(Math.Max(0,now-lastPulse-250));lastPulse=now;};pulse.Start();
        Totals Snapshot(Guid id){lock(gate){var value=measurements.GetValueOrDefault(id);return value is null?new():new(){Frames=value.Frames,Bytes=value.Bytes,WriteMs=value.WriteMs};}}
        async Task Stage(ThemeProject source,int seconds)
        {
            if(previewView is not null){previewScene?.Dispose();previewScene=new(source.Clone());previewView.Scene=previewScene;}
            var before=Snapshot(source.Id);var result=await sessions.StartAsync(device.DeviceId,profile,LegacyThemeAdapter.Read(source),cancellationToken:acceptanceStop.Token);if(!result.Started)throw new IOException(result.Code);
            var ready=DateTime.UtcNow.AddSeconds(12);
            while(sessions.Snapshot.Single().Snapshot.ActualState!=OutputActualState.Streaming){if(DateTime.UtcNow>ready)throw new IOException("No first complete frame.");if(sessions.Snapshot.Single().Snapshot.ActualState is OutputActualState.Failed or OutputActualState.Blocked)throw new IOException("Hardware session failed.");await Task.Delay(25);}
            var watch=Stopwatch.StartNew();var startCount=Snapshot(source.Id).Frames;var samples=new List<object>();using var process=Process.GetCurrentProcess();string? stageError=null;
            lock(gate){measuredTheme=source.Id;lastCompletionMs=null;intervals.Clear();slowRecorder=diagnosticFrames?new():null;}
            double pausedSeconds=0;long pausedFrames=0,pauseStartFrames=0;double? pauseStart=null;
            try
            {
                for(var i=0;i<seconds;i++)
                {
                    await Task.Delay(1000,acceptanceStop.Token);
                    if (File.Exists(Path.Combine(directory,"cancel.request"))) acceptanceStop.Cancel();
                    acceptanceStop.Token.ThrowIfCancellationRequested();
                    if(source.ActiveLayout.Layers.Any(l=>l.IsVideo)&&testPause&&i is 15 or 20)
                    {
                        var control=sessions.GetVideoControl(device.DeviceId)??throw new IOException("Video background control missing.");await control.SetBackgroundPausedAsync(i==15);
                        if(i==15){pauseStart=watch.Elapsed.TotalSeconds;pauseStartFrames=Snapshot(source.Id).Frames;}
                        else if(pauseStart is {} started){pausedSeconds+=watch.Elapsed.TotalSeconds-started;pausedFrames+=Snapshot(source.Id).Frames-pauseStartFrames;pauseStart=null;}
                        Console.WriteLine("BACKGROUND_PAUSED: "+control.BackgroundPaused);
                    }
                    if(sessions.Snapshot.Single().Snapshot.ActualState is OutputActualState.Failed or OutputActualState.Blocked or OutputActualState.Stopped)throw new IOException("Output stopped during runtime validation.");
                    if((i+1)%10==0)
                    {
                        process.Refresh();var count=Snapshot(source.Id).Frames-startCount;
                        samples.Add(new{Seconds=watch.Elapsed.TotalSeconds,Frames=count,Fps=count/watch.Elapsed.TotalSeconds,BackgroundPaused=sessions.GetVideoControl(device.DeviceId)?.BackgroundPaused==true,PrivateBytes=process.PrivateMemorySize64,WorkingSetBytes=process.WorkingSet64,ManagedBytes=GC.GetTotalMemory(false),Handles=process.HandleCount,CpuSeconds=process.TotalProcessorTime.TotalSeconds});
                        await File.WriteAllTextAsync(Path.Combine(directory,"progress.json"),JsonSerializer.Serialize(new{Cycle=currentCycle,Cycles=cycles,CompletedStages=stages.Count,Stage=source.Name,RequestedSeconds=seconds,Seconds=watch.Elapsed.TotalSeconds,Frames=count,Fps=count/watch.Elapsed.TotalSeconds,Samples=samples}));
                        Console.WriteLine($"VIDEO_THEME_RUNTIME: cycle={currentCycle}/{cycles}; {i+1}/{seconds}s; frames={count}; fps={count/watch.Elapsed.TotalSeconds:F2}");
                    }
                }
            }
            catch(Exception failure){stageError=failure.ToString();throw;}
            finally
            {
                watch.Stop();var after=Snapshot(source.Id);var frames=after.Frames-before.Frames;
                double[] intervalSamples;VideoSlowSummary? slowFrames;lock(gate){measuredTheme=null;intervalSamples=intervals.ToArray();slowFrames=slowRecorder?.Snapshot();}
                if(pauseStart is {} started){pausedSeconds+=watch.Elapsed.TotalSeconds-started;pausedFrames+=after.Frames-pauseStartFrames;}
                stages.Add(new{Name=source.Name,ContainsVideo=source.ActiveLayout.Layers.Any(l=>l.IsVideo),MeasuredSeconds=watch.Elapsed.TotalSeconds,CompletedAfterFirstFrame=after.Frames-startCount,CompletionFps=(after.Frames-startCount)/watch.Elapsed.TotalSeconds,PausedSeconds=pausedSeconds,PausedFrames=pausedFrames,ActiveFps=(after.Frames-startCount-pausedFrames)/Math.Max(.001,watch.Elapsed.TotalSeconds-pausedSeconds),FrameIntervalMs=VideoMeasurementStatistics.Summarize(intervalSamples),SlowFrames=slowFrames,IntervalsIncludeBackgroundPause=testPause&&source.ActiveLayout.Layers.Any(l=>l.IsVideo),MeanJpegBytes=frames==0?0:(after.Bytes-before.Bytes)/(double)frames,MeanWriteMs=frames==0?0:(after.WriteMs-before.WriteMs)/frames,Error=stageError,Samples=samples});
                await sessions.StopAsync(device.DeviceId);
            }
        }
        try
        {
            for(var cycle=0;cycle<cycles;cycle++){currentCycle=cycle+1;await Stage(still,4);await Stage(composed,videoSeconds);}await Stage(still,4);
            lock(gate)if(states.Any(s=>s.Status is "Failed" or "BlockedByCraft"))throw new IOException("A transport failure was recorded.");
            success=true;
        }
        catch(Exception failure){error=failure.ToString();throw;}
        finally
        {
            await sessions.StopAsync(device.DeviceId);
            pulse.Stop();
            previewWindow?.Close();previewScene?.Dispose();
            object[] status;VideoThroughput[] rateSnapshot;lock(gate){status=states.Cast<object>().ToArray();rateSnapshot=rates.ToArray();}
            await File.WriteAllTextAsync(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{Serial=serial,Width=saved.Width,Height=saved.Height,Rotation=saved.RotationDegrees,Mode=mode.ToString(),RequestedFps=requestedFps,JpegQuality=jpegQuality,AutoReduce=autoReduce,Cycles=cycles,PauseChecks=testPause,VisibleScenePreview=previewWindow is not null,PreviewAutoActivated=previewWindow is not null&&activatePreview,DiagnosticsEnabled=diagnosticFrames,DispatcherMaxDelayMs=delays.Count==0?0:delays.Max(),DispatcherP95DelayMs=delays.Count==0?0:delays.Order().ElementAt((int)(delays.Count*.95)),Success=success,Error=error,PhysicalDisplayConfirmed=false,Stages=stages,Rates=rateSnapshot,States=status},new JsonSerializerOptions{WriteIndented=true}));
        }
        Console.WriteLine("PASS: Physical D92 static/video-theme/static transitions complete without conflicting output sessions");
    }
    private sealed class Totals{public long Frames,Bytes;public double WriteMs;}
    private sealed class PerformanceSink : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent entry)
        {
            if (entry.MessageTemplate.Text.StartsWith("Video output performance:", StringComparison.Ordinal))
                Console.WriteLine(entry.RenderMessage(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
