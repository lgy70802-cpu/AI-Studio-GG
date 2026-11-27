using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;

namespace DeskDisplay.DisplayTests;

internal static class VideoPixelOptimizationTests
{
    public static async Task RunAsync()
    {
        RunRotationChecks();
        const int width=65,height=37;var random=new Random(9231);
        var video=new byte[width*height*4];random.NextBytes(video);for(var i=3;i<video.Length;i+=4)video[i]=255;
        foreach(var full in new[]{false,true})
        {
            var overlay=new byte[video.Length];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                if(!full&&(x<17||x>29||y<6||y>12))continue;
                var i=(y*width+x)*4;var alpha=(byte)random.Next(256);overlay[i+3]=alpha;
                for(var c=0;c<3;c++)overlay[i+c]=(byte)random.Next(alpha+1);
            }
            var expected=video.ToArray();
            for(var i=0;i<expected.Length;i+=4)
            {
                var alpha=overlay[i+3];if(alpha==0)continue;
                for(var c=0;c<3;c++)expected[i+c]=(byte)Math.Min(255,overlay[i+c]+(expected[i+c]*(255-alpha)+127)/255);
                expected[i+3]=255;
            }
            var actual=video.ToArray();var bounds=VideoOverlayBlender.FindBounds(overlay,width,height);
            VideoOverlayBlender.Blend(actual,overlay,width,height,bounds);
            Check(actual.SequenceEqual(expected),"Region-limited premultiplied blending exactly matches the full scan for "+(full?"dense":"sparse")+" alpha");
            if(!full)Check(bounds.X>=17&&bounds.Y>=6&&bounds.Area<=13*7,"Sparse overlay visits only its actual nontransparent bounding region");
        }
        var empty=new byte[video.Length];Check(VideoOverlayBlender.FindBounds(empty,width,height).Area==0,"A transparent foreground produces an empty blend region");
        empty[3]=1;empty[^1]=1;Check(VideoOverlayBlender.FindBounds(empty,width,height)==new PixelAlphaBounds(0,0,width,height),"Even one-alpha corner pixels remain inside blend bounds");
        var project=new ThemeProject{Landscape=new(){Width=64,Height=16,Layers=[new(){Kind=ThemeElementKind.Wallpaper,IsVideo=true,Width=64,Height=16},new(){Kind=ThemeElementKind.Rectangle,X=8,Y=4,Width=12,Height=6,Opacity=.5,Color="#3388EE",ZIndex=1}]}};
        BitmapSource retained;byte[] retainedHash;
        using(var scene=new ThemeScene(project))
        using(var composition=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>())){CaptureDiagnostics=true})
        {
            var first=Solid(64,16,40);var original=Hash(first);retained=await composition.ComposeAsync(first,default);retainedHash=Hash(retained);
            await composition.ComposeAsync(Solid(64,16,180),default);
            Check(original.SequenceEqual(Hash(first))&&retainedHash.SequenceEqual(Hash(retained)),"Reusable blend storage never modifies clean video or previously published frozen previews");
            Check(composition.LastStageTiming!.BlendVisitedPixels<64*16,"Real WPF foreground bounds reduce diagnostic blend visits");
        }
        using(var scene=new ThemeScene(project))
        using(var next=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>())))
        {await next.ComposeAsync(Solid(64,16,220),default);Check(retainedHash.SequenceEqual(Hash(retained)),"Returning and renting blend storage leaves old composition snapshots valid");}
        project.Landscape.Layers.RemoveAt(1);
        using(var scene=new ThemeScene(project))
        using(var passthrough=new ThemeVideoComposition(scene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>())))
        {var input=Solid(64,16,70);Check(ReferenceEquals(input,await passthrough.ComposeAsync(input,default)),"A fully transparent foreground passes through the original immutable video frame");}
    }
    private static void RunRotationChecks()
    {
        using var buffer=new VideoRotationBuffer();var random=new Random(9232);
        foreach(var (width,height) in new[]{(1,33),(37,1),(65,37),(1920,462),(462,1920)})
        {
            var bytes=new byte[width*height*4];random.NextBytes(bytes);
            var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);bitmap.Freeze();
            foreach(var angle in new[]{0,90,180,270})
            {
                var actual=buffer.Rotate(bitmap,angle);var expectedWidth=angle is 90 or 270?height:width;
                var expectedHeight=angle is 90 or 270?width:height;var expected=new byte[bytes.Length];
                for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                {
                    var dx=angle==90?height-1-y:angle==180?width-1-x:angle==270?y:x;
                    var dy=angle==90?x:angle==180?height-1-y:angle==270?width-1-x:y;
                    Array.Copy(bytes,(y*width+x)*4,expected,(dy*expectedWidth+dx)*4,4);
                }
                var pixels=new byte[bytes.Length];actual.CopyPixels(pixels,expectedWidth*4,0);
                Check(actual.PixelWidth==expectedWidth&&actual.PixelHeight==expectedHeight&&actual.IsFrozen&&pixels.SequenceEqual(expected),$"Tiled {angle} degree rotation preserves every channel for {width}x{height}, including partial edge tiles");
            }
        }
        var retained=buffer.Rotate(Solid(65,37,40),90);var hash=Hash(retained);
        buffer.Rotate(Solid(65,37,210),270);buffer.Dispose();buffer.Dispose();
        Check(buffer.RetainedCapacity==0&&hash.SequenceEqual(Hash(retained)),"Reusing and disposing rotation scratch arrays preserves previously published immutable images");
        try{buffer.Rotate(retained,90);throw new Exception("Expected disposed rejection");}catch(ObjectDisposedException){Check(true,"A disposed rotation workspace cannot reuse arrays returned to the pool");}
    }
    private static BitmapSource Solid(int width,int height,byte value)
    {var bytes=Enumerable.Range(0,width*height).SelectMany(_=>new byte[]{value,20,90,255}).ToArray();var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);image.Freeze();return image;}
    private static byte[] Hash(BitmapSource bitmap)
    {var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return SHA256.HashData(bytes);}
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
}
