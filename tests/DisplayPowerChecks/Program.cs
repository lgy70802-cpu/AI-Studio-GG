using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiraBox.App;
internal static class Program
{
    [STAThread] static void Main()
    {
        int count = 0;
        void Check(bool valid, string text) { if (!valid) throw new Exception(text); Console.WriteLine("PASS " + text); count++; }
        byte Red(byte[] jpeg)
        {
            using var memory = new MemoryStream(jpeg);
            var frame = BitmapFrame.Create(memory, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var image = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4]; image.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0); return pixel[2];
        }
        foreach (var model in new[] { "D69", "D92" })
        {
            var source = D92StaticImage.Encode(90, (dc, size) => dc.DrawRectangle(Brushes.White, null, new Rect(size)), model);
            using var output = new D92StaticOutput("PIXELS-" + model, 90, source, false, model, model == "D69");
            var method = typeof(D92StaticOutput).GetMethod("DisplayFrame", BindingFlags.NonPublic | BindingFlags.Instance)!;
            byte[] Render(bool standby) => (byte[])method.Invoke(output, [source, standby])!;
            Check(ReferenceEquals(source, Render(false)), model + " full brightness avoids recompression");
            output.DisplayBrightness = 50; var dimmed = Render(false);
            Check(Red(dimmed) is >= 120 and <= 135, model + " software brightness produces actual half-luminance pixels");
            D92StaticImage.RequireTransportRaster(dimmed, model); Check(true, model + " dimming preserves protocol raster");
            var black = Render(true); Check(Red(black) < 5, model + " standby produces a real black raster");
            Check(ReferenceEquals(black, Render(true)), model + " repeated black frames reuse encoded bytes");
            output.DisplayBrightness = 100; Check(Red(Render(false)) > 245, model + " wake restores original bright pixels");
        }
        Console.WriteLine(count + " checks passed");
    }
}
