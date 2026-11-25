using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Services;

namespace DeskDisplay.DisplayTests;

internal static class VideoBufferOwnershipTests
{
    public static async Task RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"D92-buffer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var video=await VideoExperienceTests.MakeVideoAsync(root);
            await using(var native=await WindowsLiveVideoFrameSource.OpenAsync(video,640,154,default))
            {
                var destination=Enumerable.Repeat((byte)0xa7,native.BufferLength+8).ToArray();
                var metadata=await native.ReadLatestIntoAsync(destination.AsMemory(4,native.BufferLength),default);
                Check(metadata.ByteLength==native.BufferLength&&destination.Take(4).Concat(destination.TakeLast(4)).All(b=>b==0xa7),"Buffered native readback writes only the caller's requested slice and returns exact geometry");
                var center=4+(metadata.Height/2*metadata.Width+metadata.Width/2)*4;
                Check(destination[center+3]==255&&destination[center+1]<30&&Math.Max(destination[center],destination[center+2])>200&&Math.Min(destination[center],destination[center+2])<30,"Native readback preserves opaque BGRA red or blue fixture colors without a channel swap");
                try{await native.ReadLatestIntoAsync(new byte[1],default);throw new Exception("Expected short-buffer rejection");}catch(ArgumentException){Check(true,"An undersized destination is rejected before sampling or writing");}
                var immutable=await native.ReadLatestAsync(default);var hash=SHA256.HashData(immutable.Pixels.Span);
                await native.ReadLatestIntoAsync(destination.AsMemory(4,native.BufferLength),default);
                Check(hash.SequenceEqual(SHA256.HashData(immutable.Pixels.Span)),"Legacy immutable pixel frames remain owned snapshots after buffered reads");
            }
            var source=await VideoJpegFrameSource.OpenAsync(video,640,154,90,backend:VideoFrameBackend.ContinuousFrameServer,captureDiagnostics:true);
            BitmapSource retained;
            byte[] retainedHash;
            try
            {
                var firstJpeg=await source.RenderAsync(default);var firstJpegHash=SHA256.HashData(firstJpeg);retained=source.PreviewFrame!;retainedHash=Hash(retained);var capacity=source.ReadbackBufferCapacity;
                for(var i=0;i<8;i++)await source.RenderAsync(default);
                Check(source.JpegStreamCapacity>=firstJpeg.Length&&firstJpegHash.SequenceEqual(SHA256.HashData(firstJpeg)),"Reused JPEG stream capacity never aliases an already returned frame");
                Check(capacity>=640*154*4&&source.ReadbackBufferCapacity==capacity&&source.LastTiming!.Stages!.ManagedCopyMs==0,"A decoder reuses one rented readback buffer without an intermediate managed pixel copy");
                Check(retained.IsFrozen&&retainedHash.SequenceEqual(Hash(retained)),"Asynchronous preview snapshots remain unchanged while the raw readback rental is reused");
                await source.SetPausedAsync(true);var paused=await source.RenderAsync(default);await Task.Delay(60);
                Check(paused.SequenceEqual(await source.RenderAsync(default)),"Paused standalone output keeps the original JPEG while readback storage is reusable");
                await source.SetPausedAsync(false);
                try{await source.RenderAsync(new CancellationToken(true));throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true,"Canceled rendering does not publish partially overwritten readback storage");}
                await source.RenderAsync(default);
            }
            finally{await source.DisposeAsync();await source.DisposeAsync();}
            Check(source.ReadbackBufferCapacity==0&&source.JpegStreamCapacity==0&&retainedHash.SequenceEqual(Hash(retained)),"Decoder disposal releases readback and JPEG storage exactly once while retained preview snapshots remain valid");
            await using(var next=await VideoJpegFrameSource.OpenAsync(video,640,154,90,backend:VideoFrameBackend.ContinuousFrameServer))
            {for(var i=0;i<4;i++)await next.RenderAsync(default);Check(retainedHash.SequenceEqual(Hash(retained)),"A new decoder can reuse pooled storage without corrupting a previous decoder's preview");}
            var closing=await VideoJpegFrameSource.OpenAsync(video,640,154,90,backend:VideoFrameBackend.ContinuousFrameServer);
            await closing.RenderAsync(default);var inFlight=closing.RenderAsync(default);var close=closing.DisposeAsync().AsTask();
            try{await inFlight;}catch(OperationCanceledException){}
            await close;
            Check(closing.ReadbackBufferCapacity==0,"Closing during a frame waits for canceled work before releasing reusable readback storage");
        }
        finally{Directory.Delete(root,true);}
    }
    private static byte[] Hash(BitmapSource bitmap)
    {var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return SHA256.HashData(bytes);}
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
}
