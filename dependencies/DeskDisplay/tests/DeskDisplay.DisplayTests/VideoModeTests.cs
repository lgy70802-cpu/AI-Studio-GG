using System.Text.Json;
using DeskDisplay.Core.Models;
namespace DeskDisplay.DisplayTests;

internal static class VideoModeTests
{
    public static void Run()
    {
        var legacy=JsonSerializer.Deserialize<UsbVideoPreferences>("{\"FramesPerSecond\":24,\"AutoReduceFrameRate\":true,\"JpegQuality\":70}")!;
        Check(legacy.Mode==VideoPlaybackMode.Custom&&legacy.Resolve()==legacy,"Old video settings retain custom FPS and quality without a migration overwrite");
        var source=new UsbVideoPreferences(10,true,70){Mode=VideoPlaybackMode.Quality};var resolved=source.Resolve();
        Check(resolved.FramesPerSecond==24&&resolved.JpegQuality==90&&!resolved.AutoReduceFrameRate&&source.FramesPerSecond==10,"Quality preset resolves independently without destroying stored custom values");
        var smooth=new UsbVideoPreferences{Mode=VideoPlaybackMode.Smooth}.Resolve();
        Check(smooth.FramesPerSecond==30&&smooth.JpegQuality==80&&smooth.AutoReduceFrameRate,"Smooth preset opts into bounded quality adaptation and sustained overload fallback");
        Check(new UsbVideoPreferences{Mode=(VideoPlaybackMode)999}.Normalize().Mode==VideoPlaybackMode.Custom,"Unknown persisted video modes safely retain custom behavior");
        var quality=new VideoQualityController(source);quality.Observe(300000,90);Check(quality.Quality==90&&quality.RetryQuality(300000)==90,"Quality preset never silently lowers JPEG quality");
        var controller=new VideoQualityController(smooth);Check(controller.RetryQuality(120000)==76,"Oversized smooth frames request at most one bounded retry quality");
        controller.Observe(120000,76);Check(controller.Quality==74,"Persistent oversized frames reduce subsequent quality in bounded steps");
        for(var i=0;i<100;i++)controller.Observe(200000,controller.Quality);
        Check(controller.Quality==60&&controller.RetryQuality(200000)==60,"Quality floor does not falsely promise that every frame fits the byte budget");
        var paused=controller.Quality;for(var i=0;i<30;i++)controller.Observe(10000,controller.Quality,true);
        Check(controller.Quality==paused,"Paused output does not train the quality controller");
        for(var i=0;i<24;i++)controller.Observe(10000,controller.Quality);
        Check(controller.Quality==61,"Quality recovers only after sustained spare byte budget");
        for(var i=0;i<2400;i++)controller.Observe(10000,controller.Quality);
        Check(controller.Quality==90,"Recovered smooth quality cannot exceed its ceiling");
        var custom=new VideoQualityController(new(24,false,70));for(var i=0;i<20;i++)custom.Observe(500000,70);
        Check(custom.Quality==70&&custom.RetryQuality(500000)==70,"Custom quality remains fixed regardless of JPEG size");
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
}
