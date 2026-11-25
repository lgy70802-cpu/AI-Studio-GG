using System.Diagnostics;
using System.Buffers;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.Services;

public sealed record VideoSampleTiming(TimeSpan MediaPosition, double DecodeMilliseconds, double EncodeMilliseconds, int JpegBytes)
{ public VideoStageTiming? Stages { get; init; } public int JpegQuality {get;init;}public string? QualityReason {get;init;}public bool ByteBudgetExceeded {get;init;}public int EncodingPasses {get;init;}=1; }
public enum VideoFrameBackend { TimestampSampling, ContinuousFrameServer }

/// <summary>Pull-only video sampling on a private STA. At most one request, no waiting-frame queue.</summary>
public sealed class VideoJpegFrameSource : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Thread _thread;
    private readonly IVideoFrameSource? _source;
    private readonly ILiveVideoFrameSource? _live;
    private readonly Stopwatch _clock = new();
    private readonly object _sync = new();
    private readonly CancellationTokenSource _life = new();
    private Task<byte[]>? _active;
    private bool _disposed;
    private Task? _dispose;
    private readonly int _width, _height, _rotation, _quality;
    private IVideoFrameComposition? _composition;
    private bool _captureDiagnostics;
    // One caller-owned readback buffer per serialized decoder. Never published to UI or a frame result.
    private byte[]? _decodePixels;
    private VideoRotationBuffer? _rotationBuffer;
    private VideoCanvasNormalizer? _canvasNormalizer;
    private byte[]? _canvasPixels;
    private MemoryStream? _jpegStream;
    private VideoQualityController? _qualityController;
    private bool _reuseEncoderBitmaps;
    private WriteableBitmap? _rawBitmap,_normalizedBitmap;
    private RenderTargetBitmap? _fallbackBitmap;
    private byte[]? _previewPixels;
    private long _previewAt;
    private VideoPreviewRequest _previewRequest = VideoPreviewRequest.LegacySnapshot;
    public void SetPreviewRequest(VideoPreviewRequest request) { request.Validate(); Volatile.Write(ref _previewRequest, request); }
    internal int ReadbackBufferCapacity => _decodePixels?.Length??0;
    internal int JpegStreamCapacity => _jpegStream?.Capacity??0;
    public TimeSpan Duration => _source?.Duration ?? _live!.Duration;
    public TimeSpan Position => _live?.Position ?? VideoFrameClock.LoopPosition(_clock.Elapsed, Duration);
    public long CompletedLoops => (_live as WindowsLiveVideoFrameSource)?.CompletedLoops ?? (long)(_clock.Elapsed.TotalSeconds / Duration.TotalSeconds);
    public VideoFrameBackend Backend => _live is null ? VideoFrameBackend.TimestampSampling : VideoFrameBackend.ContinuousFrameServer;
    public VideoSampleTiming? LastTiming { get; private set; }
    public BitmapSource? PreviewFrame { get; private set; }
    private volatile bool _paused;
    private byte[]? _lastJpeg;
    private BitmapSource? _lastRawImage;
    public bool IsPaused => _paused;
    public async Task SetPausedAsync(bool paused)
    {
        lock (_sync) ObjectDisposedException.ThrowIf(_disposed, this);
        await _dispatcher.InvokeAsync(() =>
        {
            if (_live is WindowsLiveVideoFrameSource live) live.SetPaused(paused);
            if (paused) _clock.Stop(); else _clock.Start();
            _paused = paused;
        }).Task.ConfigureAwait(false);
    }

    private VideoJpegFrameSource(Dispatcher dispatcher, Thread thread, IVideoFrameSource? source, ILiveVideoFrameSource? live, int width, int height, int rotation, int quality)
    { _dispatcher = dispatcher; _thread = thread; _source = source; _live = live; _width = width; _height = height; _rotation = rotation; _quality = quality; }

    public static async Task<VideoJpegFrameSource> OpenAsync(string path, int width, int height, int rotation, int quality = 90, CancellationToken token = default, VideoFrameBackend backend = VideoFrameBackend.TimestampSampling, IVideoFrameComposition? composition = null, bool captureDiagnostics = false, UsbVideoPreferences? encodingPreferences = null, bool reuseEncoderBitmaps = false)
    {
        if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 16_777_216 || rotation is not (0 or 90 or 180 or 270) || quality is < 30 or > 95 || !Enum.IsDefined(backend)) throw new ArgumentOutOfRangeException(nameof(width));
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { var dispatcher = Dispatcher.CurrentDispatcher; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher)); ready.SetResult(dispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = "D92 video sample STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var worker = await ready.Task.ConfigureAwait(false);
        try
        {
            if(composition is ThemeVideoComposition measuredComposition)measuredComposition.CaptureDiagnostics=captureDiagnostics;
            if (backend == VideoFrameBackend.ContinuousFrameServer)
            {
                var live = await worker.InvokeAsync(() => WindowsLiveVideoFrameSource.OpenAsync(path, width, height, token)).Task.Unwrap().ConfigureAwait(false);
                live.CaptureDiagnostics = captureDiagnostics;
                return new(worker, thread, null, live, width, height, rotation, quality) { _composition = composition, _captureDiagnostics = captureDiagnostics, _qualityController=new(encodingPreferences??new(JpegQuality:quality)), _reuseEncoderBitmaps=reuseEncoderBitmaps };
            }
            var source = await worker.InvokeAsync(() => WindowsVideoFrameSource.OpenAsync(path, token)).Task.Unwrap().ConfigureAwait(false);
            return new(worker, thread, source, null, width, height, rotation, quality) { _composition = composition, _captureDiagnostics = captureDiagnostics, _qualityController=new(encodingPreferences??new(JpegQuality:quality)), _reuseEncoderBitmaps=reuseEncoderBitmaps };
        }
        catch { worker.BeginInvokeShutdown(DispatcherPriority.Background); await Task.Run(thread.Join).ConfigureAwait(false); throw; }
    }

    public Task<byte[]> RenderAsync(CancellationToken token)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is { IsCompleted: false }) throw new InvalidOperationException("视频取帧仍在进行；不会积压后续帧。");
            return _active = RenderCoreAsync(token);
        }
    }

    private async Task<byte[]> RenderCoreAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _life.Token);
        return await _dispatcher.InvokeAsync(async () =>
        {
            double readMs=0,copyMs=0,createMs=0,composeMs=0,rotateMs=0,encodeMs=0,jpegCopyMs=0,standaloneNormalizeMs=0;
            long mark=0;
            void Mark(){if(_captureDiagnostics)mark=Stopwatch.GetTimestamp();}
            double Elapsed()=>_captureDiagnostics?Stopwatch.GetElapsedTime(mark).TotalMilliseconds:0;
            VideoStageTiming? Profile(BitmapSource? raw,bool reused)=>_captureDiagnostics?new(readMs,copyMs,createMs,composeMs,rotateMs,encodeMs,jpegCopyMs,raw?.PixelWidth??0,raw?.PixelHeight??0,_paused,reused,_paused?null:(_live as WindowsLiveVideoFrameSource)?.LastReadTiming){Composition=(_composition as ThemeVideoComposition)?.LastStageTiming,StandaloneNormalizeMs=standaloneNormalizeMs}:null;
            if (_paused && _composition is null && _lastJpeg is not null)
            {
                linked.Token.ThrowIfCancellationRequested();Mark();var heldBytes=_lastJpeg.ToArray();jpegCopyMs=Elapsed();
                if(_captureDiagnostics)LastTiming=new(Position,0,0,heldBytes.Length){Stages=Profile(_lastRawImage,true),JpegQuality=LastTiming?.JpegQuality??_quality,QualityReason=LastTiming?.QualityReason,ByteBudgetExceeded=LastTiming?.ByteBudgetExceeded??false,EncodingPasses=0};
                return heldBytes;
            }
            if (!_paused && !_clock.IsRunning) _clock.Start();
            var position = Position;
            var watch = Stopwatch.StartNew();
            BitmapSource image; double decode; var samplePosition = LastTiming?.MediaPosition ?? position;
            if (_paused && _lastRawImage is { } held) { image = held; decode = 0; }
            else
            {
                int frameWidth,frameHeight,frameStride;byte[] managedPixels;
                if(_live is IBufferedLiveVideoFrameSource buffered)
                {
                    _decodePixels??=ArrayPool<byte>.Shared.Rent(buffered.BufferLength);
                    Mark();var frame=await buffered.ReadLatestIntoAsync(_decodePixels.AsMemory(0,buffered.BufferLength),linked.Token);readMs=Elapsed();
                    frameWidth=frame.Width;frameHeight=frame.Height;frameStride=frame.Stride;samplePosition=frame.SamplePosition;
                    managedPixels=_decodePixels;
                }
                else
                {
                    Mark();var frame = _live is null ? await _source!.ReadAsync(position, _width, _height, linked.Token) : await _live.ReadLatestAsync(linked.Token);readMs=Elapsed();
                    frameWidth=frame.Width;frameHeight=frame.Height;frameStride=frame.Stride;samplePosition=frame.SamplePosition;
                    Mark();managedPixels=MemoryMarshal.TryGetArray(frame.Pixels,out var segment)&&segment.Offset==0&&segment.Array is not null?segment.Array:frame.Pixels.ToArray();copyMs=Elapsed();
                }
                // BitmapSource.Create snapshots the input synchronously. Frozen snapshots, not the rental,
                // are retained for pause and asynchronous preview; covered by the reuse/immutability tests.
                Mark();
                if(_reuseEncoderBitmaps)
                {
                    if(_rawBitmap is null||_rawBitmap.PixelWidth!=frameWidth||_rawBitmap.PixelHeight!=frameHeight)
                        _rawBitmap=new WriteableBitmap(frameWidth,frameHeight,96,96,PixelFormats.Bgra32,null);
                    _rawBitmap.WritePixels(new Int32Rect(0,0,frameWidth,frameHeight),managedPixels,frameStride,0);image=_rawBitmap;
                }
                else { image = BitmapSource.Create(frameWidth, frameHeight, 96, 96, PixelFormats.Bgra32, null, managedPixels, frameStride); image.Freeze(); }
                createMs=Elapsed();
                _lastRawImage = image; decode = watch.Elapsed.TotalMilliseconds;
            }
            watch.Restart();
            BitmapSource bitmap;
            Mark();
            if (_composition is not null) bitmap = await _composition.ComposeAsync(image, linked.Token);
            else if (image.PixelWidth == _width && image.PixelHeight == _height)
                bitmap = image; // Native frame already fits: avoid a GPU render/readback round trip.
            else if(image.Format==PixelFormats.Bgra32&&Math.Min(_width/(double)image.PixelWidth,_height/(double)image.PixelHeight)>=1)
            {
                _canvasPixels??=ArrayPool<byte>.Shared.Rent(checked(_width*_height*4));
                (_canvasNormalizer??=new()).Copy(image,_canvasPixels,_width,_height,Colors.Black,.5,.5);
                if(_reuseEncoderBitmaps) {
                    _normalizedBitmap??=new WriteableBitmap(_width,_height,96,96,PixelFormats.Bgra32,null);
                    _normalizedBitmap.WritePixels(new Int32Rect(0,0,_width,_height),_canvasPixels,_width*4,0);bitmap=_normalizedBitmap;
                } else {bitmap=BitmapSource.Create(_width,_height,96,96,PixelFormats.Bgra32,null,_canvasPixels,_width*4);bitmap.Freeze();}
                standaloneNormalizeMs=Elapsed();
            }
            else
            {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.Black, null, new Rect(0, 0, _width, _height));
                var scale = Math.Min(_width / (double)image.PixelWidth, _height / (double)image.PixelHeight);
                var width = image.PixelWidth * scale; var height = image.PixelHeight * scale;
                drawing.DrawImage(image, new Rect((_width - width) / 2, (_height - height) / 2, width, height));
            }
            var composed = _reuseEncoderBitmaps ? _fallbackBitmap??=new RenderTargetBitmap(_width,_height,96,96,PixelFormats.Pbgra32) : new RenderTargetBitmap(_width, _height, 96, 96, PixelFormats.Pbgra32);
            if(_reuseEncoderBitmaps)composed.Clear();composed.Render(visual);if(!_reuseEncoderBitmaps)composed.Freeze();
            bitmap = composed;
            }
            composeMs=Elapsed();
            // Materialize the orthogonal rotation once instead of a lazy WIC transform during JPEG encoding.
            if (ReferenceEquals(bitmap, PreviewFrame) && _lastJpeg is not null) { Mark();var cached=_lastJpeg.ToArray();jpegCopyMs=Elapsed();LastTiming = new(samplePosition, decode, 0, _lastJpeg.Length){Stages=Profile(image,true),JpegQuality=LastTiming?.JpegQuality??_quality,QualityReason=LastTiming?.QualityReason,ByteBudgetExceeded=LastTiming?.ByteBudgetExceeded??false}; return cached; }
            Mark();BitmapSource rotated = _rotation==0?bitmap:(_rotationBuffer??=new()).Rotate(bitmap,_rotation,_reuseEncoderBitmaps);rotateMs=Elapsed();
            Mark();
            // 每个串行编码器复用流容量；ToArray 仍为输出帧创建独立快照。
            var stream = _jpegStream??=new MemoryStream();var quality=_paused&&LastTiming?.JpegQuality>0?LastTiming.JpegQuality:_qualityController?.Quality??_quality;
            void Encode(int selected){stream.Position=0;stream.SetLength(0);var encoder=new JpegBitmapEncoder{QualityLevel=selected};encoder.Frames.Add(BitmapFrame.Create(rotated));encoder.Save(stream);}
            Encode(quality);linked.Token.ThrowIfCancellationRequested();
            var retry=_qualityController?.RetryQuality((int)stream.Length)??quality;var passes=1;
            if(retry!=quality&&!_paused){quality=retry;Encode(quality);passes=2;linked.Token.ThrowIfCancellationRequested();}
            _qualityController?.Observe((int)stream.Length,quality,_paused);
            encodeMs=Elapsed();Mark();var bytes = stream.ToArray();jpegCopyMs=Elapsed();
            if(!_reuseEncoderBitmaps) PreviewFrame=bitmap;
            else {
                var previewRequest=Volatile.Read(ref _previewRequest);
                if(PreviewFrame is null || previewRequest.Enabled && Stopwatch.GetElapsedTime(_previewAt)>=TimeSpan.FromSeconds(1d/previewRequest.FramesPerSecond)) {
                var scale=Math.Min(1,Math.Min(previewRequest.MaximumWidth/(double)bitmap.PixelWidth,previewRequest.MaximumHeight/(double)bitmap.PixelHeight));
                BitmapSource previewSource=scale<1?new TransformedBitmap(bitmap,new ScaleTransform(scale,scale)):bitmap;
                var previewWidth=previewSource.PixelWidth;var previewHeight=previewSource.PixelHeight;
                var length=checked(previewWidth*previewHeight*4);
                if(_previewPixels is null||_previewPixels.Length<length)_previewPixels=new byte[length];
                previewSource.CopyPixels(_previewPixels,previewWidth*4,0);
                var preview=BitmapSource.Create(previewWidth,previewHeight,96,96,previewSource.Format,null,_previewPixels,previewWidth*4);preview.Freeze();PreviewFrame=preview;_previewAt=Stopwatch.GetTimestamp();
                }
            }
            _lastJpeg = bytes; LastTiming = new(samplePosition, decode, watch.Elapsed.TotalMilliseconds, bytes.Length){Stages=Profile(image,false),JpegQuality=quality,QualityReason=_qualityController?.Reason,ByteBudgetExceeded=_qualityController?.Adaptive==true&&bytes.Length>VideoQualityController.RetryBytes,EncodingPasses=passes}; return bytes;
        }, DispatcherPriority.Background, linked.Token).Task.Unwrap().ConfigureAwait(false);
    }

    internal static BitmapSource RotatePixels(BitmapSource bitmap, int rotation)
    {
        using var buffer=new VideoRotationBuffer();return buffer.Rotate(bitmap,rotation);
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync) { if (_dispose is not null) return new(_dispose); _disposed = true; _life.Cancel(); return new(_dispose = DisposeCoreAsync(_active)); }
    }
    private async Task DisposeCoreAsync(Task<byte[]>? active)
    {
        try { if (active is not null) await active.ConfigureAwait(false); } catch { /* cleanup must also run after decoder failure */ }
        try { await _dispatcher.InvokeAsync(async () => { try { _composition?.Dispose(); } finally { _composition = null; _lastRawImage = PreviewFrame = null; _lastJpeg = null; if (_live is not null) await _live.DisposeAsync(); else await _source!.DisposeAsync(); } }).Task.Unwrap().ConfigureAwait(false); }
        finally
        {
            _rotationBuffer?.Dispose();_rotationBuffer=null;
            _rawBitmap=_normalizedBitmap=null;_fallbackBitmap=null;_previewPixels=null;
            _jpegStream?.Dispose();_jpegStream=null;
            _canvasNormalizer?.Dispose();_canvasNormalizer=null;
            if(_canvasPixels is {} canvas){_canvasPixels=null;ArrayPool<byte>.Shared.Return(canvas);}
            if(_decodePixels is { } pixels){_decodePixels=null;ArrayPool<byte>.Shared.Return(pixels);}
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); await Task.Run(_thread.Join).ConfigureAwait(false); _life.Dispose();
        }
    }
}
