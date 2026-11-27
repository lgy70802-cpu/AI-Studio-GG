using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiraBox.App;

internal sealed partial class D92StaticOutput
{
    internal volatile bool DisplayStandby;
    internal volatile int DisplayBrightness = 100;
    byte[]? displaySource;
    byte[]? displayResult;
    int displayLevel = -1;

    byte[] DisplayFrame(byte[] source, bool standby)
    {
        var level = standby ? 0 : Math.Clamp(DisplayBrightness, 0, 100);
        if (level == 100) return source;
        if (displayResult != null && displayLevel == level &&
            (level == 0 || ReferenceEquals(displaySource, source))) return displayResult;
        using var input = new MemoryStream(source);
        var image = BitmapFrame.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight);
            dc.DrawImage(image, bounds);
            dc.PushOpacity(1 - level / 100d);
            dc.DrawRectangle(Brushes.Black, null, bounds);
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        displaySource = source;
        displayLevel = level;
        return displayResult = output.ToArray();
    }
}
