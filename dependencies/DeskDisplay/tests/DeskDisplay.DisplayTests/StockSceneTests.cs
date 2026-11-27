using System.IO;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;

namespace DeskDisplay.DisplayTests;

internal static class StockSceneTests
{
    public static Task RunAsync()
    {
        var scene = new StockScene();
        scene.Update([
            new("sh600519", "贵州茅台", 1518.88, 1.26, 1500),
            new("sz300750", "宁德时代", 286.42, -0.83, 288.82),
            new("sh600036", "招商银行", 41.25, 0.34, 41.11),
            new("sh688981", "中芯国际", 112.60, 2.18, 110.20)
        ], DateTimeOffset.Now);
        using var renderer = new SceneJpegRenderer();
        var jpeg = renderer.Render(scene, 0);
        using var stream = new MemoryStream(jpeg);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Check(decoder.Frames[0].PixelWidth == 1920 && decoder.Frames[0].PixelHeight == 462,
            "Stock plugin renders a native 1920 x 462 USB frame");
        Check(jpeg.Length > 20_000, "Stock plugin frame contains rendered quote content");
        Console.WriteLine("All stock scene tests passed.");
        return Task.CompletedTask;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine($"PASS: {description}");
    }
}
