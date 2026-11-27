using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;

namespace DeskDisplay.DisplayTests;

internal static class RetroCameraClockTests
{
    public static Task RunAsync()
    {
        var scene = new RetroCameraClockScene { Animate = true, DurationMs = 440, CropY = 145 };
        var timestamp = Stopwatch.GetTimestamp();
        scene.Tick(new DateTimeOffset(2026, 10, 9, 8, 42, 36, TimeSpan.FromHours(8)), timestamp);
        scene.Tick(scene.CurrentTime, timestamp + Stopwatch.Frequency);
        using var renderer = new SceneJpegRenderer();
        var jpeg = renderer.Render(scene, 0);
        using var stream = new MemoryStream(jpeg);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Check(decoder.Frames[0].PixelWidth == 1920 && decoder.Frames[0].PixelHeight == 462,
            "Retro camera clock renders a native 1920 x 462 USB frame");
        Check(jpeg.Length > 100_000, "Retro camera clock includes the photographic source asset");

        scene.Demonstrate(timestamp + Stopwatch.Frequency * 2);
        var flipStart = renderer.Render(scene, 0);
        scene.Tick(scene.CurrentTime, timestamp + Stopwatch.Frequency * 2 + Stopwatch.Frequency / 5);
        var flipMiddle = renderer.Render(scene, 0);
        Check(!SHA256.HashData(flipStart).SequenceEqual(SHA256.HashData(flipMiddle)),
            "Retro camera flip animation produces distinct intermediate frames");

        var path = Path.Combine(AppContext.BaseDirectory, "retro-camera-clock.jpg");
        File.WriteAllBytes(path, jpeg);
        Console.WriteLine($"RENDER IMAGE: {path}");
        Console.WriteLine("All retro camera clock tests passed.");
        return Task.CompletedTask;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine($"PASS: {description}");
    }
}
