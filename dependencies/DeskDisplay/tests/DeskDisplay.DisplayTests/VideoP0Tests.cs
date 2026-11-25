using System.IO;
using DeskDisplay.Display.Services;

namespace DeskDisplay.DisplayTests;

internal static class VideoP0Tests
{
    public static async Task RunAsync()
    {
        var stats=VideoMeasurementStatistics.Summarize(Enumerable.Range(1,100).Select(x=>(double)x));
        Check(stats.Count==100&&stats.P50==50&&stats.P95==95&&stats.P99==99,"P0 percentiles use nearest rank across all retained measurements");
        Check(VideoMeasurementStatistics.Summarize([double.NaN,double.PositiveInfinity]).Count==0,"P0 empty distributions are unavailable instead of fabricated zero latency");
        var fit=VideoMeasurementStatistics.FitReports(new[]{(60,17d),(80,22d),(100,27d),(120,32d)});
        Check(fit.InterceptMs==2&&fit.MsPerReport==.25&&fit.RSquared==1,"P0 report regression separates fixed cost from per-report slope");
        Check(VideoMeasurementStatistics.FitReports(new[]{(100,20d),(100,30d)}).MsPerReport is null,"A fixed-size baseline cannot independently identify a per-report slope");
        var root=Path.Combine(Path.GetTempPath(),"D92-P0-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var video=await VideoExperienceTests.MakeVideoAsync(root);
            await using(var source=await VideoJpegFrameSource.OpenAsync(video,640,154,90,backend:VideoFrameBackend.ContinuousFrameServer,captureDiagnostics:true))
            {
                await source.RenderAsync(default);var timing=source.LastTiming!;var stage=timing.Stages!;
                Check(stage.Native is not null&&stage.SourceWidth>0&&stage.SourceHeight>0&&!stage.Paused&&!stage.ReusedJpeg,"Opt-in P0 records actual native readback and encoded frame geometry");
                Check(new[]{stage.NativeReadMs,stage.ManagedCopyMs,stage.BitmapCreateMs,stage.CompositionMs,stage.RotationMs,stage.JpegEncodeMs,stage.JpegCopyMs}.All(x=>double.IsFinite(x)&&x>=0),"P0 stage timings are finite and nonnegative");
                await source.SetPausedAsync(true);await source.RenderAsync(default);
                Check(source.LastTiming!.Stages is {Paused:true,ReusedJpeg:true,Native:null}&&timing.Stages==stage,"Paused JPEG reuse is labeled separately and previous frame measurements remain immutable");
            }
            await using(var plain=await VideoJpegFrameSource.OpenAsync(video,640,154,0))
            {await plain.RenderAsync(default);Check(plain.LastTiming!.Stages is null,"Normal playback does not enable detailed P0 diagnostics by default");}
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
}
