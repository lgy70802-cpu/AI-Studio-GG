using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;

namespace DeskDisplay.Display.Usb;

/// <summary>Uses the preview's SceneView and Scene; only the target applies transport rotation.</summary>
public sealed class SceneJpegRenderer : IDisposable
{
    // Matches the WIC quantization tables verified on the physical D92.
    // This is a verified profile, not evidence that other JPEG quality levels are unsupported.
    public const int DefaultQuality = 90;
    private readonly SceneView _view = new();
    private RenderTargetBitmap? _bitmap;
    private bool _disposed;
    private Exception? _renderError;
    private WeakReference<ImageScene>? _cachedScene;
    private (int Width,int Height,int Rotation,int Quality) _cachedGeometry;
    private byte[]? _cachedJpeg;
    public bool LastRenderWasCached {get;private set;}

    public SceneJpegRenderer(bool transparentThemeBackdrop = false) { _view.SkipThemeBackdrop = transparentThemeBackdrop; _view.RenderFailed += OnRenderFailed; }
    private void OnRenderFailed(Exception error) => _renderError = error;

    public byte[] Render(Scene scene, int rotationDegrees, int quality = DefaultQuality)
    {
        _view.Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        LastRenderWasCached=false;
        var geometry=(scene.PixelWidth,scene.PixelHeight,rotationDegrees,quality);
        var cacheable=scene is ImageScene {Image:WpfSceneImage {Bitmap.IsFrozen:true}};
        if(cacheable && _cachedJpeg is not null && _cachedGeometry==geometry && _cachedScene?.TryGetTarget(out var cached)==true && ReferenceEquals(cached,scene))
        {LastRenderWasCached=true;RenderPerformance.RecordCacheHit();return _cachedJpeg.ToArray();}
        _cachedJpeg=null;_cachedScene=null;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var result=RenderCore(scene,rotationDegrees,quality);
        RenderPerformance.Record(watch.Elapsed.TotalMilliseconds,result.Length);
        if(cacheable && result.Length<=8*1024*1024)
        {_cachedScene=new((ImageScene)scene);_cachedGeometry=geometry;_cachedJpeg=result.ToArray();}
        return result;
    }
    private byte[] RenderCore(Scene scene,int rotationDegrees,int quality)
    {
        if (rotationDegrees is not (0 or 90 or 180 or 270)) throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        if (quality is < 30 or > 95) throw new ArgumentOutOfRangeException(nameof(quality));
        var bitmap = RenderBitmap(scene);
        var output = VideoJpegFrameSource.RotatePixels(bitmap, rotationDegrees);
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    public BitmapSource RenderBitmap(Scene scene)
    {
        _view.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (scene is ThemeScene { ContainsVideo: true } theme && theme.Project.ActiveLayout.Layers.Any(l => l.Visible && l.IsVideo && theme.Children.GetValueOrDefault(l.Id) is not ImageScene))
            throw new NotSupportedException("视频图层需要通过视频输出链路解码后再合成。");
        if (scene is not (ClockScene or ImageScene or PerformanceScene or DashboardScene or StockScene or RetroCameraClockScene or ThemeScene))
            throw new NotSupportedException("USB Alpha supports clock, image, performance, dashboard, stock and camera clock scenes");
        if (scene.PixelWidth > 8192 || scene.PixelHeight > 8192 || (long)scene.PixelWidth * scene.PixelHeight > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(scene), "Canvas too large for USB Alpha");
        _renderError = null;
        _view.Scene = scene;
        var size = new Size(scene.PixelWidth, scene.PixelHeight);
        _view.Measure(size);
        _view.Arrange(new Rect(size));
        _view.InvalidateVisual();
        _view.UpdateLayout();
        if (_bitmap is null || _bitmap.PixelWidth != scene.PixelWidth || _bitmap.PixelHeight != scene.PixelHeight)
            _bitmap = new(scene.PixelWidth, scene.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        _bitmap.Clear();
        _bitmap.Render(_view);
        if (_renderError is not null) throw new InvalidOperationException("Scene rendering failed", _renderError);
        var snapshot = _bitmap.Clone(); snapshot.Freeze(); return snapshot;
    }

    public void Dispose()
    {
        _view.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _view.RenderFailed -= OnRenderFailed;
        _view.Scene = null;
        _bitmap = null;
        _cachedJpeg=null;_cachedScene=null;
    }
}
