using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Core.Models;

namespace DeskDisplay.VideoPrototype;

internal static partial class Program
{
    private static ThemeScene CreateClockFixture(Plan plan)
    {
        var project=new ThemeProject{Name="P0 diagnostic video and live clock"};project.Landscape.Width=plan.Width;project.Landscape.Height=plan.Height;
        project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Path=plan.VideoPath,Width=plan.Width,Height=plan.Height});
        project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Rectangle,X=plan.Width*.55,Y=plan.Height*.6,Width=plan.Width*.43,Height=plan.Height*.35,Color="#172A46",Opacity=.85,ZIndex=1});
        project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,X=plan.Width*.56,Y=plan.Height*.61,Width=plan.Width*.42,Height=plan.Height*.32,FontSize=plan.Height*.2,ShowSeconds=true,ZIndex=2});
        return new(project);
    }
    private static object SummarizeStages(IEnumerable<Frame> frames)
    {
        var stages=frames.Select(f=>f.Sample.Stages).OfType<VideoStageTiming>().ToArray();
        return new {
            NativeRead=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.NativeReadMs)),
            NativeWait=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Native is not null).Select(s=>s.Native!.WaitMs)),
            SurfaceCopy=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Native is not null).Select(s=>s.Native!.SurfaceCopyMs)),
            Readback=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Native is not null).Select(s=>s.Native!.ReadbackMs)),
            NativePixelCopy=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Native is not null).Select(s=>s.Native!.PixelCopyMs)),
            ManagedCopy=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.ManagedCopyMs)),
            BitmapCreate=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.BitmapCreateMs)),
            Composition=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.CompositionMs)),
            StandaloneNormalize=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.StandaloneNormalizeMs)),
            FastPathFrames=stages.Count(s=>s.Composition?.FastPath==true),
            OverlayRedrawFrames=stages.Count(s=>s.Composition?.RedrawCause>OverlayRedrawCause.None),
            BackgroundNormalize=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>s.Composition!.BackgroundNormalizeMs)),
            OverlayRenderAndCopy=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>s.Composition!.OverlayRenderAndCopyMs)),
            CompositionInputCopy=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>s.Composition!.InputCopyMs)),
            PixelBlend=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>s.Composition!.BlendMs)),
            BlendVisitedPixels=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>(double)s.Composition!.BlendVisitedPixels)),
            CompositionBitmapCreate=VideoMeasurementStatistics.Summarize(stages.Where(s=>s.Composition?.FastPath==true).Select(s=>s.Composition!.BitmapCreateMs)),
            Rotation=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.RotationMs)),
            JpegEncode=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.JpegEncodeMs)),
            JpegCopy=VideoMeasurementStatistics.Summarize(stages.Select(s=>s.JpegCopyMs))
        };
    }

    private static async Task<int> MakeFixturesAsync(Plan plan,string output)
    {
        if(plan.Width is <1 or >8192||plan.Height is <1 or >8192||(long)plan.Width*plan.Height>16_777_216||plan.Rotation is not(0 or 90 or 180 or 270))throw new InvalidDataException("Invalid fixture geometry");
        var directory=Path.GetFullPath(output);if(Directory.Exists(directory))throw new IOException("New fixture directory required");
        Directory.CreateDirectory(directory);
        var width=plan.Rotation is 90 or 270?plan.Height:plan.Width;var height=plan.Rotation is 90 or 270?plan.Width:plan.Height;
        var pixels=new byte[checked(width*height*4)];var random=new Random(9230);
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var i=(y*width+x)*4;var noise=random.Next(-16,17);
            pixels[i]=(byte)Math.Clamp(80+x*120/width+noise,0,255);
            pixels[i+1]=(byte)Math.Clamp(60+y*140/height+noise,0,255);
            pixels[i+2]=(byte)Math.Clamp(160-x*100/width+noise,0,255);pixels[i+3]=255;
        }
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);bitmap.Freeze();
        var candidates=new List<(int Quality,byte[] Bytes)>();
        for(var quality=30;quality<=95;quality++)
        {
            var encoder=new JpegBitmapEncoder{QualityLevel=quality};encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=new MemoryStream();encoder.Save(stream);candidates.Add((quality,stream.ToArray()));
        }
        var summaries=new List<object>();
        foreach(var target in new[]{60_000,80_000,100_000,120_000})
        {
            var selected=candidates.MinBy(c=>Math.Abs(c.Bytes.Length-target));
            var path=Path.Combine(directory,$"jpeg-target-{target}.jpg");await File.WriteAllBytesAsync(path,selected.Bytes);
            var fixturePlan=plan with{VideoPath="",BaselineJpegs=[path,path],RequestedFps=[30],ExperimentalHighFrameRate=true,CaptureDiagnostics=true};
            await File.WriteAllTextAsync(Path.Combine(directory,$"plan-{target}.json"),JsonSerializer.Serialize(fixturePlan,Json));
            summaries.Add(new{TargetBytes=target,ActualBytes=selected.Bytes.Length,selected.Quality,Width=width,Height=height,Path=path,PaddingAdded=false});
        }
        await File.WriteAllTextAsync(Path.Combine(directory,"fixtures.json"),JsonSerializer.Serialize(summaries,Json));
        Console.WriteLine(JsonSerializer.Serialize(summaries,Json));return 0;
    }

    private static async Task<int> AnalyzeHidAsync(string input,string output)
    {
        var directory=Path.GetFullPath(output);if(Directory.Exists(directory))throw new IOException("New analysis directory required");
        var samples=new List<(int Reports,double WriteMs)>();var cases=new List<object>();
        foreach(var path in Directory.GetFiles(input,"result.json",SearchOption.AllDirectories).Order())
        {
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(path));var root=json.RootElement;
            if(!root.GetProperty("RealDevice").GetBoolean()||!root.GetProperty("Decoder").GetString()!.StartsWith("Pre-encoded JPEG"))throw new InvalidDataException("Only real-device pre-encoded baselines may be combined");
            var capacity=root.GetProperty("ReportLength").GetInt32()-1;if(capacity<1)throw new InvalidDataException("Invalid report length");
            foreach(var stage in root.GetProperty("Stages").EnumerateArray())
            {
                if(stage.GetProperty("Failed").GetBoolean())throw new InvalidDataException("Failed stage retained separately: "+path);
                var frames=stage.GetProperty("Frames").EnumerateArray().ToArray();var writes=new List<double>();var sizes=new List<int>();
                foreach(var frame in frames)
                {
                    var bytes=frame.GetProperty("Sample").GetProperty("JpegBytes").GetInt32();var ms=frame.GetProperty("Transport").GetProperty("WriteMilliseconds").GetDouble();
                    samples.Add(((bytes+D92FrameEncoder.HeaderLength+capacity-1)/capacity,ms));writes.Add(ms);sizes.Add(bytes);
                }
                cases.Add(new{Source=path,Count=frames.Length,JpegBytes=VideoMeasurementStatistics.Summarize(sizes.Select(n=>(double)n)),WriteMs=VideoMeasurementStatistics.Summarize(writes),HostCompletionFps=stage.GetProperty("CompletionFps").GetDouble()});
            }
        }
        if(cases.Count<2)throw new InvalidDataException("At least two distinct baseline cases required");
        Directory.CreateDirectory(directory);
        var result=new{Meaning="Host full-frame write completion, capped at requested FPS; neither endpoint polling proof nor panel presentation FPS",Regression=VideoMeasurementStatistics.FitReports(samples),Cases=cases};
        await File.WriteAllTextAsync(Path.Combine(directory,"curve.json"),JsonSerializer.Serialize(result,Json));Console.WriteLine(JsonSerializer.Serialize(result,Json));return 0;
    }
}
