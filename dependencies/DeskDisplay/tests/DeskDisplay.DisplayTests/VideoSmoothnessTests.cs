using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;

namespace DeskDisplay.DisplayTests;

internal static class VideoSmoothnessTests
{
    public static async Task RunAsync()
    {
        var clock=new ManualClock();clock.Advance(.2);var project=Project(64,16,true);var source=Image(64,16);var captures=0;
        using(var scene=new ThemeScene(project))
        using(var composition=new ThemeVideoComposition(scene,_=>{captures++;return Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>());},timeProvider:clock){CaptureDiagnostics=true})
        {
            await composition.ComposeAsync(source,default);var first=composition.OverlayRenderCount;
            clock.Advance(.6);await composition.ComposeAsync(source,default);
            Check(composition.OverlayRenderCount==first,"The same clock second does not redraw an unchanged foreground");
            clock.Advance(.3);await composition.ComposeAsync(source,default);var next=composition.OverlayRenderCount;
            Check(next==first+1,"A real clock second redraws exactly once despite a periodic empty snapshot");
            clock.Advance(.2);await composition.ComposeAsync(source,default);
            Check(composition.OverlayRenderCount==next,"Snapshot cadence cannot introduce another redraw inside the same second");
            Check(captures==1,"A video/clock/shape-only theme avoids repeated empty UI-thread child capture");
        }
        foreach(var (sw,sh) in new[]{(16,9),(32,8),(8,16),(37,16),(38,16)})
        {
            var raw=Image(sw,sh);using var scene=new ThemeScene(Project(64,16,false));
            using var fast=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));
            using var reference=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()),false);
            var actual=await fast.ComposeAsync(raw,default);var expected=await reference.ComposeAsync(raw,default);
            var a=Pixels(actual);var b=Pixels(expected);var error=a.Zip(b).Average(p=>Math.Abs(p.First-p.Second));var max=a.Zip(b).Max(p=>Math.Abs(p.First-p.Second));
            Console.WriteLine($"NORMALIZE_DIFF: {sw}x{sh}; mean={error}; max={max}");
            Check(fast.FastPathFrames==1&&actual.PixelWidth==64&&actual.PixelHeight==16&&error<1&&max<=8,$"Uniform {sw}x{sh} video uses the canvas fast path and matches reference geometry/colors");
        }
        var stockClock=new ManualClock();stockClock.Advance(.2);var stockProject=Project(64,16,false);var stockId=Guid.NewGuid();stockProject.Landscape.Layers.Add(new(){Id=stockId,Kind=ThemeElementKind.StockPlugin,Width=64,Height=16,ZIndex=1});var price=10d;
        using(var scene=new ThemeScene(stockProject))
        using(var stocks=new ThemeVideoComposition(scene,_=>{var stock=new StockScene();stock.Update([new("TEST","test",price,0)],DateTimeOffset.UnixEpoch);return Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>{{stockId,stock}});},timeProvider:stockClock))
        {
            await stocks.ComposeAsync(source,default);stockClock.Advance(.9);await stocks.ComposeAsync(source,default);
            Check(stocks.OverlayRenderCount==2,"The stock plugin's own visible clock still redraws on a new second");
            stockClock.Advance(.2);await stocks.ComposeAsync(source,default);
            Check(stocks.OverlayRenderCount==2,"Identical stock snapshots do not add a second redraw inside the same second");
            price=11;stockClock.AdvanceTimestamp(1.1);await stocks.ComposeAsync(source,default);
            Check(stocks.OverlayRenderCount==3,"A real stock value change invalidates the foreground cache independently of the clock");
        }
        foreach(var crop in new[]{0d,.3,1d})
        {
            var p=Project(64,16,false);p.Landscape.Layers[0].CropX=crop;using var scene=new ThemeScene(p);
            using var fast=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));using var reference=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()),false);
            var a=Pixels(await fast.ComposeAsync(Image(16,9),default));var b=Pixels(await reference.ComposeAsync(Image(16,9),default));
            Check(a.Zip(b).Average(v=>Math.Abs(v.First-v.Second))<1,$"Uniform crop alignment {crop} preserves full-image placement against the reference renderer");
        }
        using(var scene=new ThemeScene(Project(64,16,false)))
        using(var fast=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>())))
        using(var reference=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()),false))
        {
            var checker=Image(16,9,true);var a=Pixels(await fast.ComposeAsync(checker,default));var b=Pixels(await reference.ComposeAsync(checker,default));var mean=a.Zip(b).Average(p=>Math.Abs(p.First-p.Second));var max=a.Zip(b).Max(p=>Math.Abs(p.First-p.Second));Console.WriteLine($"NORMALIZE_CHECKER_DIFF: mean={mean}; max={max}");
            Check(mean<1&&max<=12,"High-contrast 16:9 content preserves reference sampling within bounded edge interpolation error");
        }
        var cropped=Project(64,16,false);cropped.Landscape.Layers[0].Fill=ImageDisplayMode.Fill;
        using(var scene=new ThemeScene(cropped))using(var fallback=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>())))
        {await fallback.ComposeAsync(Image(16,9),default);Check(fallback.FastPathFrames==0,"Unsupported non-native crop mode keeps its original renderer rather than silently changing to contain");}
        var directory=Path.Combine(Path.GetTempPath(),"D92-aspect-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var video=await VideoExperienceTests.MakeVideoAsync(directory,640,360);using var scene=new ThemeScene(Project(1920,462,true));
            using var composition=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));
            await using var jpeg=await VideoJpegFrameSource.OpenAsync(video,1920,462,90,backend:VideoFrameBackend.ContinuousFrameServer,composition:composition,captureDiagnostics:true);
            var bytes=await jpeg.RenderAsync(default);using var stream=new MemoryStream(bytes);var decoded=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
            Check(composition.FastPathFrames>0&&decoded.PixelWidth==462&&decoded.PixelHeight==1920&&jpeg.PreviewFrame!.PixelWidth==1920,"A real 16:9 MP4 normalizes into the device canvas fast path before a single transport rotation");
            await using var standalone=await VideoJpegFrameSource.OpenAsync(video,1920,462,90,backend:VideoFrameBackend.ContinuousFrameServer,captureDiagnostics:true);
            var direct=await standalone.RenderAsync(default);var retained=Pixels(standalone.PreviewFrame!);await standalone.SetPausedAsync(true);
            Check(standalone.LastTiming!.Stages!.StandaloneNormalizeMs>0&&direct.SequenceEqual(await standalone.RenderAsync(default))&&retained.SequenceEqual(Pixels(standalone.PreviewFrame!)),"Standalone 16:9 playback uses the same contain normalization and preserves a paused preview snapshot");
        }
        finally{Directory.Delete(directory,true);}
        var recorder=new VideoSlowFrameRecorder(2);var normal=new VideoSampleTiming(TimeSpan.Zero,1,1,100);
        var redraw=normal with{Stages=new(1,0,0,1,0,1,0,1,1,false,false,new(0,0,12,0)){Composition=new(true,2,0,0,0){RedrawCause=OverlayRedrawCause.Clock}}};
        recorder.Observe(0,normal,new(1,100,0,1),0,0);
        for(var i=1;i<=4;i++)recorder.Observe(i*60,redraw,new(i+1,100,2,3),i, i);
        var stats=recorder.Snapshot();Check(stats.SlowFrames==4&&stats.Retained==2&&stats.DroppedDetails==2&&stats.WithRedraw==4&&stats.WithReadback==4&&stats.WithGcPause==4,"Slow-frame correlation retains bounded details and counts overlapping annotations without claiming causation");
        var ahead=new VideoSlowFrameRecorder();ahead.Observe(0,normal,new(1,100,0,1),0,0);
        ahead.Observe(60,normal,new(2,100,0,1),0,0,redraw,true);var aheadStats=ahead.Snapshot();
        Check(aheadStats.WithRedraw==1&&aheadStats.WithReadback==1&&aheadStats.WithPendingLookAhead==1&&aheadStats.Events[0].PreparedLookAhead==redraw,"Correlation includes completed look-ahead work and explicitly marks an unfinished producer");
    }
    private static ThemeProject Project(int w,int h,bool time)=>new(){Landscape=new(){Width=w,Height=h,Background="#152239",Layers=time?[new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Width=w,Height=h},new(){Kind=ThemeElementKind.DigitalClock,Width=w,Height=h,FontSize=8,ZIndex=1}]:[new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Width=w,Height=h}]}};
    private static BitmapSource Image(int w,int h,bool checker=false)
    {var p=new byte[w*h*4];for(var y=0;y<h;y++)for(var x=0;x<w;x++){var i=(y*w+x)*4;p[i]=checker?(byte)((x+y)%2*255):(byte)(20+x*120/w);p[i+1]=checker?(byte)((x+y+1)%2*255):(byte)(30+y*120/h);p[i+2]=checker?(byte)((x+y)%2*255):(byte)180;p[i+3]=255;}var b=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,p,w*4);b.Freeze();return b;}
    private static byte[] Pixels(BitmapSource b){var p=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(p,b.PixelWidth*4,0);return p;}
    private static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
    private sealed class ManualClock:TimeProvider
    {
        private double _seconds,_ticks;public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(2026,10,29,0,0,0,TimeSpan.Zero).AddSeconds(_seconds);
        public override TimeZoneInfo LocalTimeZone=>TimeZoneInfo.Utc;public override long GetTimestamp()=> (long)(_ticks*Stopwatch.Frequency);public void Advance(double seconds){_seconds+=seconds;_ticks+=seconds;}public void AdvanceTimestamp(double seconds)=>_ticks+=seconds;
    }
}
