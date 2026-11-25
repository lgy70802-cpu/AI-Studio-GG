using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DeskDisplay.App.Views;

/// <summary>Bounded shared preview decode. No file reads or decoding on the dispatcher.</summary>
public static class AsyncPreviewImage
{
    public static readonly DependencyProperty UriProperty = DependencyProperty.RegisterAttached("Uri", typeof(Uri), typeof(AsyncPreviewImage), new PropertyMetadata(null, Changed));
    public static Uri? GetUri(DependencyObject value) => (Uri?)value.GetValue(UriProperty);
    public static void SetUri(DependencyObject value, Uri? uri) => value.SetValue(UriProperty, uri);
    private static readonly SemaphoreSlim Gate = new(4);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, (BitmapSource Image, long Used, long Bytes)> Cache = [];
    private static readonly Dictionary<string, Task<BitmapSource?>> Pending = [];
    private static long _clock;
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not Image image) return;
        image.Loaded -= Loaded; image.Unloaded -= Unloaded;
        image.Loaded += Loaded; image.Unloaded += Unloaded;
        image.Source = null;
        if (image.IsLoaded) Update(image);
    }
    private static void Loaded(object sender, RoutedEventArgs e) => Update((Image)sender);
    private static void Unloaded(object sender, RoutedEventArgs e) => ((Image)sender).Source = null;
    private static async void Update(Image image)
    {
        var uri = GetUri(image); if (uri is null) return;
        try
        {
            // WPF downloads remote public previews asynchronously; local cached previews use our decoder.
            if (!uri.IsFile) { image.Source = new BitmapImage(uri); return; }
            var source = await LoadAsync(uri);
            if (image.IsLoaded && GetUri(image) == uri) image.Source = source;
        }
        catch (Exception exception) { Serilog.Log.Warning(exception, "Cannot load preview {Uri}", uri); }
    }
    public static Task<BitmapSource?> LoadAsync(Uri uri) => Task.Run(async () =>
    {
        if (!uri.IsFile) return null;
        try
        {
            var info = new FileInfo(uri.LocalPath);
            if (!info.Exists) return null;
            var key = info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            Task<BitmapSource?> task;
            lock (Sync)
            {
                if (Cache.TryGetValue(key, out var hit)) { Cache[key] = (hit.Image, ++_clock, hit.Bytes); return hit.Image; }
                if (!Pending.TryGetValue(key, out task!)) Pending[key] = task = DecodeAsync(uri.LocalPath);
            }
            var result = await task;
            lock (Sync)
            {
                Pending.Remove(key);
                if (result is not null)
                {
                    Cache[key] = (result, ++_clock, (long)result.PixelWidth * result.PixelHeight * 4);
                    while (Cache.Count > 72 || Cache.Values.Sum(v => v.Bytes) > 64 * 1024 * 1024)
                        Cache.Remove(Cache.MinBy(pair => pair.Value.Used).Key);
                }
            }
            return result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException) { return null; }
    });
    private static async Task<BitmapSource?> DecodeAsync(string path)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            var width = frame.PixelWidth; var height = frame.PixelHeight; stream.Position = 0;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (height > width) bitmap.DecodePixelHeight = Math.Min(960, height); else bitmap.DecodePixelWidth = Math.Min(960, width);
            bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException) { return null; }
        finally { Gate.Release(); }
    }
}
