using System.IO;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.DataProviders;

public sealed record WpfSceneImage(BitmapSource Bitmap, int OriginalWidth, int OriginalHeight) : ISceneImage;

public sealed class ImageSceneLoader
{
    public const int MaximumEdge = 4096;
    public const long MaximumPixels = 8_000_000;

    public Task<ImageScene> LoadAsync(string path, ImageDisplayMode mode, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var fullPath = System.IO.Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 128L * 1024 * 1024) throw new InvalidDataException("图片文件超过 128 MB，请先缩小图片。");
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder)) throw new InvalidDataException("请选择 JPG 或 PNG 图片。");
        var width = decoder.Frames[0].PixelWidth;
        var height = decoder.Frames[0].PixelHeight;
        if (width <= 0 || height <= 0 || (long)width * height > 200_000_000)
            throw new InvalidDataException("图片尺寸无效或超过 2 亿像素，请先缩小图片。");
        var scale = Math.Min(1, Math.Min((double)MaximumEdge / Math.Max(width, height), Math.Sqrt((double)MaximumPixels / ((long)width * height))));
        stream.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        // StreamSource has no URI cache entry; IgnoreImageCache requires a non-null URI in WPF.
        bitmap.CreateOptions = BitmapCreateOptions.None;
        bitmap.DecodePixelWidth = Math.Max(1, (int)(width * scale));
        bitmap.DecodePixelHeight = Math.Max(1, (int)(height * scale));
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        cancellationToken.ThrowIfCancellationRequested();
        return new ImageScene(fullPath, new WpfSceneImage(bitmap, width, height), mode);
    }, cancellationToken);
}
