using System.IO;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;

namespace DeskDisplay.Display.Services;

/// <summary>Only active sessions retain frozen decoded images. One decode at a time, bounded retained bytes.</summary>
public sealed class SessionImagePool
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _state = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private long _bytes;
    public const long MaximumRetainedBytes = 256L * 1024 * 1024;
    public async Task<ImageReference> AcquireAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await Task.Run(() =>
            {
                var info = new FileInfo(Path.GetFullPath(path));
                return (Path: info.FullName, Key: $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
            }, ct).ConfigureAwait(false);
            var key = file.Key;
            lock (_state) if (_entries.TryGetValue(key, out var cached)) { cached.Users++; return new(this, key, cached.Image); }
            var image = await new ImageSceneLoader().LoadAsync(file.Path, ImageDisplayMode.Uniform, ct).ConfigureAwait(false);
            var bitmap = ((WpfSceneImage)image.Image).Bitmap;
            var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * Math.Max(4, bitmap.Format.BitsPerPixel / 8);
            lock (_state)
            {
                if (_bytes + bytes > MaximumRetainedBytes) throw new InvalidOperationException("多设备素材内存预算已满，请减少图片或停止部分输出。");
                _entries.Add(key, new(image, bytes)); _bytes += bytes;
            }
            return new(this, key, image);
        }
        finally { _gate.Release(); }
    }
    private void Release(string key)
    {
        lock (_state) if (_entries.TryGetValue(key, out var entry) && --entry.Users == 0) { _entries.Remove(key); _bytes -= entry.Bytes; }
    }
    public sealed class ImageReference(SessionImagePool owner, string key, ImageScene image) : IDisposable
    {
        private int _released;
        public ImageScene Image { get; } = image;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) owner.Release(key); }
    }
    private sealed class Entry(ImageScene image, long bytes)
    {
        public ImageScene Image { get; } = image;
        public long Bytes { get; } = bytes;
        public int Users = 1;
    }
}
