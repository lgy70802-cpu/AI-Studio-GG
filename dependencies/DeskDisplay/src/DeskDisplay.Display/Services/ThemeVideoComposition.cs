using System.Diagnostics;
using System.Buffers;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Usb;

namespace DeskDisplay.Display.Services;

public interface IVideoFrameComposition : IDisposable
{
    Task<BitmapSource> ComposeAsync(BitmapSource video, CancellationToken token);
}

/// <summary>Created on the owner thread; all renderer and composition mutations occur on video STA.</summary>
public sealed class ThemeVideoComposition : IVideoFrameComposition
{
    private readonly ThemeProject _project;
    private readonly DeskDisplay.Core.Adaptation.ResolvedLayout? _resolved;
    private readonly Guid _videoId;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<Guid, Scene>>> _capture;
    private ThemeScene? _scene;
    private SceneJpegRenderer? _renderer;
    private long _captured;
    private ThemeScene? _overlayScene;
    private SceneJpegRenderer? _overlayRenderer;
    private byte[]? _overlayPixels;
    private byte[]? _blendPixels;
    private PixelAlphaBounds _overlayBounds;
    private VideoCanvasNormalizer? _normalizer;
    private long _overlaySecond = long.MinValue;
    private BitmapSource? _lastVideo, _lastResult;
    private bool _overlayDirty = true;
    private readonly bool _enableCachedOverlay;
    private readonly TimeProvider _time;
    private readonly bool _hasTimeLayers;
    private readonly bool _pollChildren;
    private bool _hasCaptured;
    public long OverlayRenderCount { get; private set; }
    public long FastPathFrames { get; private set; }
    public bool CaptureDiagnostics { get; set; }
    public VideoCompositionTiming? LastStageTiming { get; private set; }
    public ThemeVideoComposition(ThemeScene source, Func<CancellationToken, Task<IReadOnlyDictionary<Guid, Scene>>> capture, bool enableCachedOverlay = true, TimeProvider? timeProvider = null)
    {
        var videos = source.Project.ActiveLayout.Layers.Where(l => l.Visible && l.IsVideo).ToArray();
        if (videos.Length != 1) throw new NotSupportedException("每个布局目前支持一个可见视频图层。");
        _project = source.Project.Clone(); _resolved = source.ResolvedLayout; _videoId = videos[0].Id; _capture = capture;
        _enableCachedOverlay = enableCachedOverlay;
        _time=timeProvider??TimeProvider.System;
        _hasTimeLayers=_project.ActiveLayout.Layers.Any(l=>l.Visible&&l.Kind is ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday or ThemeElementKind.CameraClock or ThemeElementKind.StockPlugin);
        _pollChildren=_project.ActiveLayout.Layers.Any(l=>l.Visible&&(l.Kind is ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock||!l.IsVideo&&l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper));
    }
    public async Task<BitmapSource> ComposeAsync(BitmapSource video, CancellationToken token)
    {
        LastStageTiming=null;
        _scene ??= _resolved is null ? new(_project) : new(_project, _resolved);
        if (!_hasCaptured || _pollChildren&&_time.GetElapsedTime(_captured) >= TimeSpan.FromSeconds(1))
        {
            var children = await _capture(token);
            foreach (var child in children)
            {
                if(_scene.Children.TryGetValue(child.Key,out var previous)&&SameSnapshot(previous,child.Value))continue;
                _scene.SetChild(child.Key, child.Value); _overlayScene?.SetChild(child.Key, child.Value);_overlayDirty=true;
            }
            _captured = _time.GetTimestamp();_hasCaptured=true;
        }
        _scene.SetChild(_videoId, new ImageScene("decoded-video-frame", new WpfSceneImage(video, video.PixelWidth, video.PixelHeight), ImageDisplayMode.Uniform));
        var now = _time.GetLocalNow(); _scene.Tick(now);
        foreach (var camera in _scene.Children.Values.OfType<RetroCameraClockScene>()) camera.Tick(now, Stopwatch.GetTimestamp());
        if (CanBlend(video)) return BlendOverlay(video, now);
        _renderer ??= new(); var fallback=_renderer.RenderBitmap(_scene);
        if(CaptureDiagnostics)LastStageTiming=new(false,0,0,0,0);
        return fallback;
    }
    private static bool SameSnapshot(Scene previous,Scene next)=>ReferenceEquals(previous,next)||previous is StockScene a&&next is StockScene b&&a.Status==b.Status&&a.UpdatedAt==b.UpdatedAt&&a.UpdatedAt?.Offset==b.UpdatedAt?.Offset&&a.Quotes.SequenceEqual(b.Quotes);
    private bool CanBlend(BitmapSource video)
    {
        if (!_enableCachedOverlay || video.Format != PixelFormats.Bgra32) return false;
        var layer = _project.ActiveLayout.Layers.Single(l => l.Id == _videoId);
        var width = _resolved?.SurfaceSize.Width ?? _project.ActiveLayout.Width;
        var height = _resolved?.SurfaceSize.Height ?? _project.ActiveLayout.Height;
        if (layer.Rotation != 0 || layer.Opacity != 1 || layer.Brightness != 1 || layer.ImageZoom != 1||width!=Math.Truncate(width)||height!=Math.Truncate(height)) return false;
        if(video.PixelWidth!=width||video.PixelHeight!=height)
        {
            if(layer.Fill!=ImageDisplayMode.Uniform||Math.Min(width/video.PixelWidth,height/video.PixelHeight)<1)return false;
            try{if(((Color)ColorConverter.ConvertFromString(_project.ActiveLayout.Background)).A!=255)return false;}
            catch(Exception error)when(error is FormatException or NotSupportedException or ArgumentException){return false;}
        }
        if (_project.ActiveLayout.Layers.Any(l => l.Visible && l.Id != _videoId && l.ZIndex <= layer.ZIndex)) return false;
        var placed = _resolved?.Layers.FirstOrDefault(l => l.Source.Id == _videoId);
        return placed is null ? layer.X == 0 && layer.Y == 0 && layer.Width == width && layer.Height == height
            : placed.ContentScale == 1 && placed.Bounds.X == 0 && placed.Bounds.Y == 0 && placed.Bounds.Width == width && placed.Bounds.Height == height;
    }
    private BitmapSource BlendOverlay(BitmapSource video, DateTimeOffset now)
    {
        var width=_resolved?.SurfaceSize.Width??(int)_project.ActiveLayout.Width;
        var height=_resolved?.SurfaceSize.Height??(int)_project.ActiveLayout.Height;
        var normalize=video.PixelWidth!=width||video.PixelHeight!=height;
        if (_overlayScene is null)
        {
            var project = _project.Clone(); project.ActiveLayout.Background = "#00000000";
            project.ActiveLayout.Layers.Single(l => l.Id == _videoId).Visible = false;
            _overlayScene = _resolved is null ? new(project) : new(project, _resolved);
            foreach (var child in _scene!.Children.Where(c => c.Key != _videoId)) _overlayScene.SetChild(child.Key, child.Value);
            _overlayRenderer = new(true);
        }
        var second = now.ToUnixTimeSeconds();
        var animated = _scene!.Children.Values.OfType<RetroCameraClockScene>().Any(c => c.Animate);
        var cause=(_overlayDirty?OverlayRedrawCause.ContentChanged:OverlayRedrawCause.None)
            |(_hasTimeLayers&&_overlaySecond!=second?OverlayRedrawCause.Clock:OverlayRedrawCause.None)
            |(animated?OverlayRedrawCause.Animation:OverlayRedrawCause.None);
        var changed = cause!=OverlayRedrawCause.None;
        var stamp=CaptureDiagnostics?Stopwatch.GetTimestamp():0;double overlayMs=0,inputMs=0,blendMs=0;
        if (changed)
        {
            _overlayScene.Tick(now);
            var overlay = _overlayRenderer!.RenderBitmap(_overlayScene);
            _overlayPixels ??= new byte[checked(width * height * 4)];
            overlay.CopyPixels(_overlayPixels, width * 4, 0);
            _overlayBounds=VideoOverlayBlender.FindBounds(_overlayPixels,width,height);
            _overlaySecond = second; _overlayDirty = false;
            OverlayRenderCount++;
        }
        if(CaptureDiagnostics)overlayMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
        if (!changed && ReferenceEquals(video, _lastVideo) && _lastResult is not null)
        {if(CaptureDiagnostics)LastStageTiming=new(true,overlayMs,0,0,0);return _lastResult;}
        if(CaptureDiagnostics)stamp=Stopwatch.GetTimestamp();
        if(_overlayBounds.Area==0&&!normalize)
        {
            if(CaptureDiagnostics)LastStageTiming=new(true,overlayMs,0,0,0){BlendVisitedPixels=0,RedrawCause=cause};
            FastPathFrames++;_lastVideo=video;return _lastResult=video;
        }
        var pixels = _blendPixels??=ArrayPool<byte>.Shared.Rent(checked(width * height * 4));
        if(normalize)
        {
            var layer=_project.ActiveLayout.Layers.Single(l=>l.Id==_videoId);
            (_normalizer??=new()).Copy(video,pixels,width,height,(Color)ColorConverter.ConvertFromString(_project.ActiveLayout.Background),layer.CropX,layer.CropY);
        }
        else video.CopyPixels(pixels,width*4,0);
        if(CaptureDiagnostics){inputMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;stamp=Stopwatch.GetTimestamp();}
        VideoOverlayBlender.Blend(pixels,_overlayPixels!,width,height,_overlayBounds);
        if(CaptureDiagnostics){blendMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;stamp=Stopwatch.GetTimestamp();}
        var result = BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4); result.Freeze();
        if(CaptureDiagnostics)LastStageTiming=new(true,overlayMs,inputMs,blendMs,Stopwatch.GetElapsedTime(stamp).TotalMilliseconds){BlendVisitedPixels=_overlayBounds.Area,RedrawCause=cause,BackgroundNormalizeMs=normalize?inputMs:0};
        FastPathFrames++; _lastVideo = video; return _lastResult = result;
    }
    public static IReadOnlyDictionary<Guid, Scene> CaptureChildren(ThemeScene source)
    {
        var result = new Dictionary<Guid, Scene>();
        foreach (var child in source.Children)
        {
            if (child.Value is ImageScene image) result[child.Key] = image;
            else if (child.Value is StockScene stock)
            {
                var copy = new StockScene(); copy.Update(stock.Quotes.ToArray(), stock.UpdatedAt ?? DateTimeOffset.Now); copy.SetStatus(stock.Status); result[child.Key] = copy;
            }
            else if (child.Value is RetroCameraClockScene camera)
                result[child.Key] = new RetroCameraClockScene { Animate = camera.Animate, Hour12 = camera.Hour12, DurationMs = camera.DurationMs, CropY = camera.CropY };
        }
        return result;
    }
    public void Dispose() { _renderer?.Dispose(); _renderer = null; _scene?.Dispose(); _scene = null; _overlayRenderer?.Dispose(); _overlayScene?.Dispose(); _overlayRenderer = null; _overlayScene = null; _overlayPixels = null; _lastResult = _lastVideo = null; _normalizer?.Dispose();_normalizer=null;if(_blendPixels is {} pixels){_blendPixels=null;ArrayPool<byte>.Shared.Return(pixels);} }
}
