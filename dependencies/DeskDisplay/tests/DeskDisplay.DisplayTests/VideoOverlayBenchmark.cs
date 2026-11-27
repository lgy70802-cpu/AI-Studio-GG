using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;

namespace DeskDisplay.DisplayTests;
internal static class VideoOverlayBenchmark
{
    public static async Task RunAsync()
    {
        var video=Environment.GetEnvironmentVariable("DESKDISPLAY_OVERLAY_SOURCE")??throw new IOException("Explicit source required.");
        var evidence=Environment.GetEnvironmentVariable("DESKDISPLAY_OVERLAY_EVIDENCE")??throw new IOException("New evidence path required.");if(File.Exists(evidence))throw new IOException("Evidence exists.");
        var cases=new List<object>();
        foreach(var cached in new[]{false,true})
        {
            var project=new ThemeProject{Name="Overlay benchmark"};project.Landscape.Width=1920;project.Landscape.Height=462;
            project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Path=video,Width=1920,Height=462});
            project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Rectangle,X=1100,Y=292,Width=800,Height=150,Color="#172A46",Opacity=.85,ZIndex=1});
            project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,X=1120,Y=302,Width=760,Height=130,FontSize=96,ShowSeconds=true,ZIndex=2});
            using var scene=new ThemeScene(project);
            var composition=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()),cached);
            await using var source=await VideoJpegFrameSource.OpenAsync(video,1920,462,90,backend:VideoFrameBackend.ContinuousFrameServer,composition:composition);
            await source.RenderAsync(default);await using var pipeline=new VideoFramePipeline(source);using var timer=new VideoTimerResolution(true);
            var watch=Stopwatch.StartNew();var frames=0;double encode=0,decode=0;long bytes=0;
            while(watch.Elapsed<TimeSpan.FromSeconds(10))
            {
                var at=watch.Elapsed;var frame=await pipeline.ReadAsync(default);frames++;encode+=frame.Timing.EncodeMilliseconds;decode+=frame.Timing.DecodeMilliseconds;bytes+=frame.Bytes.Length;
                var wait=TimeSpan.FromSeconds(1d/30)-(watch.Elapsed-at);if(wait>TimeSpan.Zero)await Task.Delay(wait);
            }
            cases.Add(new{CachedOverlay=cached,Seconds=watch.Elapsed.TotalSeconds,Frames=frames,Fps=frames/watch.Elapsed.TotalSeconds,AverageEncodeMs=encode/frames,AverageDecodeMs=decode/frames,AverageJpegBytes=bytes/frames,FastPathFrames=composition.FastPathFrames});
        }
        await File.WriteAllTextAsync(evidence,JsonSerializer.Serialize(new{Hardware=false,Cases=cases},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(await File.ReadAllTextAsync(evidence));
    }
}
