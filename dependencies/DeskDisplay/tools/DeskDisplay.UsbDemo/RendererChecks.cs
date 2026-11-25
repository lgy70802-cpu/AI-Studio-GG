using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;

namespace DeskDisplay.UsbDemo;

internal static class RendererChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; }
        var scene = new ClockScene();
        scene.SetCanvasSize(640, 160);
        scene.UpdateTime(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        using var renderer = new SceneJpegRenderer();
        var first = renderer.Render(scene, 90);
        using (var stream = new MemoryStream(first))
        {
            var decoded = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(decoded.PixelWidth == 160 && decoded.PixelHeight == 640, "Transport rotation");
        }
        Check(scene.PixelWidth == 640 && scene.PixelHeight == 160, "Transport must not rotate the shared scene");
        scene.UpdateTime(new DateTimeOffset(2026, 1, 2, 13, 14, 15, TimeSpan.Zero));
        var second = renderer.Render(scene, 90);
        Check(!first.SequenceEqual(second), "Changed scene must produce changed pixels without a loaded visual tree");
        scene.SetCanvasSize(320, 240);
        using (var stream = new MemoryStream(renderer.Render(scene, 0)))
        {
            var decoded = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(decoded.PixelWidth == 320 && decoded.PixelHeight == 240, "Arbitrary resized canvas");
        }
        try { renderer.Render(scene, 45); throw new InvalidOperationException("Invalid rotation accepted"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        renderer.Dispose();
        try { renderer.Render(scene, 0); throw new InvalidOperationException("Disposed renderer accepted work"); }
        catch (ObjectDisposedException) { checks++; }
        Console.WriteLine(JsonSerializer.Serialize(new { result = "pass", rendererChecks = checks, hardwareAccess = false }));
        return 0;
    }
}
