using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Interfaces;
using Windows.Media.Editing;
using Windows.Storage;

namespace DeskDisplay.Display.Services;

/// <summary>
/// Timestamp sampling prototype using the native media editor. It decodes a real frame per request;
/// it is not a continuous hardware frame-server and makes no high-FPS guarantee.
/// Serialized by its owner; all frame buffers and image streams have bounded lifetimes.
/// </summary>
public sealed class WindowsVideoFrameSource : IVideoFrameSource
{
    private MediaComposition? _composition;
    private readonly uint _width, _height;
    public TimeSpan Duration { get; }
    private WindowsVideoFrameSource(MediaComposition composition, uint width, uint height, TimeSpan duration)
    { _composition = composition; _width = width; _height = height; Duration = duration; }

    public static async Task<WindowsVideoFrameSource> OpenAsync(string path, CancellationToken token = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("视频不存在。", path);
        if (!Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("原型仅支持 MP4 视频。");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(token);
        var properties = await file.Properties.GetVideoPropertiesAsync().AsTask(token);
        if (properties.Width == 0 || properties.Height == 0 || properties.Duration <= TimeSpan.Zero) throw new InvalidDataException("视频没有有效尺寸或时长。");
        var clip = await MediaClip.CreateFromFileAsync(file).AsTask(token);
        var composition = new MediaComposition(); composition.Clips.Add(clip);
        return new(composition, properties.Width, properties.Height, composition.Duration);
    }

    public async Task<VideoPixelFrame> ReadAsync(TimeSpan position, int maxWidth, int maxHeight, CancellationToken cancellationToken)
    {
        var composition = _composition ?? throw new ObjectDisposedException(nameof(WindowsVideoFrameSource));
        if (position < TimeSpan.Zero || position >= Duration) throw new ArgumentOutOfRangeException(nameof(position));
        if (maxWidth is < 1 or > 8192 || maxHeight is < 1 or > 8192 || (long)maxWidth * maxHeight > 16_777_216) throw new ArgumentOutOfRangeException(nameof(maxWidth));
        var scale = Math.Min(1d, Math.Min(maxWidth / (double)_width, maxHeight / (double)_height));
        var width = Math.Max(1, (int)Math.Floor(_width * scale));
        // Passing only width preserves the source aspect ratio in the native API.
        using var image = await composition.GetThumbnailAsync(position, width, 0, VideoFramePrecision.NearestFrame).AsTask(cancellationToken);
        using var stream = image.AsStreamForRead();
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        if (bitmap.PixelWidth > maxWidth + 1 || bitmap.PixelHeight > maxHeight + 1 || (long)bitmap.PixelWidth * bitmap.PixelHeight > 16_777_216) throw new InvalidDataException("解码帧超过尺寸限制。");
        var stride = checked(bitmap.PixelWidth * 4); var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0); cancellationToken.ThrowIfCancellationRequested();
        return new(bitmap.PixelWidth, bitmap.PixelHeight, stride, position, pixels);
    }
    public ValueTask DisposeAsync() { _composition?.Clips.Clear(); _composition = null; return ValueTask.CompletedTask; }
}
