using System.Diagnostics;
using System.IO;
using System.Buffers.Binary;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Usb;
using Serilog;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;

namespace MiraBox.App;

internal static class D92StaticImage
{
    public const int TransportWidth = 462;
    public const int TransportHeight = 1920;

    public static ThemeOutputGeometry Geometry(int rotation, string model = "D92") => rotation switch
    {
        0 or 180 => new(model == "D69" ? 480 : TransportWidth, model == "D69" ? 1280 : TransportHeight, rotation),
        90 or 270 => new(model == "D69" ? 1280 : TransportHeight, model == "D69" ? 480 : TransportWidth, rotation),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation))
    };

    public static byte[] Encode(int rotation, Action<DrawingContext, Size> draw, string model = "D92")
    {
        var geometry = Geometry(rotation, model);
        var canvas = new Size(geometry.Width, geometry.Height);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(canvas));
            draw(context, canvas);
        }

        var bitmap = new RenderTargetBitmap(geometry.Width, geometry.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        BitmapSource output = rotation == 0 ? bitmap : new TransformedBitmap(bitmap, new RotateTransform(rotation));
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static byte[] Encode(int rotation, BitmapSource image, string model = "D92") => Encode(rotation, (context, size) =>
    {
        var scale = Math.Min(size.Width / image.PixelWidth, size.Height / image.PixelHeight);
        var width = image.PixelWidth * scale;
        var height = image.PixelHeight * scale;
        context.DrawImage(image, new Rect((size.Width - width) / 2, (size.Height - height) / 2, width, height));
    }, model);

    public static void RequireTransportRaster(ReadOnlySpan<byte> jpeg, string model = "D92")
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8 || jpeg[^2] != 0xff || jpeg[^1] != 0xd9)
            throw new InvalidOperationException("D92 发送已拦截，JPEG 文件不完整。");

        // Scan JPEG headers only, without decoding or allocating a bitmap on the USB writer.
        var offset = 2;
        while (offset < jpeg.Length && jpeg[offset++] == 0xff)
        {
            while (offset < jpeg.Length && jpeg[offset] == 0xff) offset++;
            if (offset >= jpeg.Length) break;
            var marker = jpeg[offset++];
            if (marker is 0xda or 0xd9) break;
            if (marker == 0x01 || marker is >= 0xd0 and <= 0xd7) continue;
            if (jpeg.Length - offset < 2) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg[offset..]);
            if (length < 2 || length > jpeg.Length - offset) break;
            if (marker is >= 0xc0 and <= 0xcf and not (0xc4 or 0xc8 or 0xcc))
            {
                if (length < 8) break;
                var height = BinaryPrimitives.ReadUInt16BigEndian(jpeg[(offset + 3)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(jpeg[(offset + 5)..]);
                var expectedWidth = model == "D69" ? 480 : TransportWidth;
                var expectedHeight = model == "D69" ? 1280 : TransportHeight;
                if (width == expectedWidth && height == expectedHeight) return;
                throw new InvalidOperationException($"{model} 发送已拦截，JPEG 尺寸 {width} × {height}，需要 {expectedWidth} × {expectedHeight}。");
            }
            offset += length;
        }
        throw new InvalidOperationException("D92 发送已拦截，无法读取 JPEG 尺寸。");
    }
}

internal sealed record HardwareDeviceOptions(string? VideoMode = null, string Group = "", bool D69VideoEnabled = true, int D69VideoFps = 20, string MediaFit = "contain", string? LandscapeContent = null, string? PortraitContent = null, int LandscapeRotation = 90, int PortraitRotation = 0, int DisplayBrightness = 100, int StandbyMinutes = 0);

internal sealed class D92OrientationPreferences(string path)
{
    Dictionary<string, HardwareDeviceOptions>? cachedOptions;
    public const int DefaultRotation = 90;

    public int Get(string serial) => Read().GetValueOrDefault(serial, DefaultRotation);

    public void Save(string serial, int rotation)
    {
        if (rotation is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotation));

        var options = GetOptions(serial);
        SaveOptions(serial, rotation % 180 == 0
            ? options with { PortraitRotation = rotation }
            : options with { LandscapeRotation = rotation });
        var values = Read();
        values[serial] = rotation;
        SaveJson(path, values);
    }

    public string? GetApplyTarget()
    {
        var targetPath = path + ".target";
        return File.Exists(targetPath) ? JsonSerializer.Deserialize<string>(File.ReadAllText(targetPath)) : null;
    }

    public void SaveApplyTarget(string? serial) => SaveJson(path + ".target", serial);

    public Dictionary<string, string> GetNames()
    {
        var namesPath = path + ".names";
        return File.Exists(namesPath)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(namesPath)) ?? new()
            : new();
    }

    public void SaveName(string serial, string name)
    {
        var names = GetNames();
        names[serial] = name;
        SaveJson(path + ".names", names);
    }

    public Dictionary<string, string> GetModels() => File.Exists(path + ".models")
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path + ".models")) ?? new() : new();

    public void SaveModel(string serial, string model)
    {
        if (model is not ("D92" or "D69")) throw new NotSupportedException("设备型号不支持");
        var models = GetModels();
        models[serial] = model;
        SaveJson(path + ".models", models);
    }

    public HardwareDeviceOptions GetOptions(string serial)
    {
        var options = ReadOptions().GetValueOrDefault(serial, new());
        var current = Get(serial);
        return current % 180 == 0
            ? options with { PortraitRotation = current }
            : options with { LandscapeRotation = current };
    }

    public void SaveOptions(string serial, HardwareDeviceOptions options)
    {
        var values = new Dictionary<string, HardwareDeviceOptions>(ReadOptions());
        values[serial] = options;
        SaveJson(path + ".options", values);
        cachedOptions = values;
    }

    Dictionary<string, HardwareDeviceOptions> ReadOptions() => cachedOptions ??= File.Exists(path + ".options")
        ? JsonSerializer.Deserialize<Dictionary<string, HardwareDeviceOptions>>(File.ReadAllText(path + ".options")) ?? new() : new();

    static void SaveJson<T>(string destination, T value)
    {
        var temporary = destination + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    Dictionary<string, int> Read()
    {
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        var values = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path));
        if (values == null || values.Values.Any(angle => angle is not (0 or 90 or 180 or 270)))
            throw new InvalidDataException("D92 旋转配置无效。");
        return values;
    }
}

internal interface ID92PlaybackTransport : ID92RenderTarget
{
    event Action<D92FrameTiming>? FrameCompleted;
}

internal sealed class D92PlaybackTransport(ILogger logger, bool highRateVideo = false, string model = "D92", string? serial = null,
    bool allowD69Video = false) : ID92PlaybackTransport
{
    readonly D92HidRenderTarget sender = new(logger, experimentalHighFrameRate: highRateVideo,
        confirmedDevice: model == "D69" ? new DeviceDescriptor(OutputDeviceKeys.UsbScreen(model, serial!), model,
            DeviceTransport.UsbHid, new("usb:2100:0027", serial, IdentityConfidence.VerifiedStable),
            new(serial!, "", 0), true) : null, experimentalD69Video: model == "D69" && allowD69Video,
        skipRepeatedStillFrames: model == "D92" && !highRateVideo && D92StaticOutput.StillHeartbeatOnly);
    public event Action<D92OutputState>? StateChanged
    {
        add => sender.StateChanged += value;
        remove => sender.StateChanged -= value;
    }
    public event Action<D92FrameTiming>? FrameCompleted
    {
        add => sender.FrameCompleted += value;
        remove => sender.FrameCompleted -= value;
    }
    public Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render,
        TimeSpan frameInterval, CancellationToken cancellationToken)
    {
        byte[]? validated = null;
        return sender.RunAsync(serialNumber, async token =>
        {
            var bytes = await render(token).ConfigureAwait(false);
            if (!ReferenceEquals(validated, bytes))
            {
                D92StaticImage.RequireTransportRaster(bytes, model);
                validated = bytes;
            }
            return bytes;
        }, frameInterval, cancellationToken);
    }
}

internal sealed partial class D92StaticOutput : IDisposable
{
    internal static readonly bool StillHeartbeatOnly =
        Environment.GetEnvironmentVariable("MIRABOX_D92_STILL_HEARTBEAT_ONLY") == "1";
    internal static readonly UsbVideoPreferences VideoProfile = (new UsbVideoPreferences { Mode = VideoPlaybackMode.Quality }).Resolve();
    internal static readonly UsbVideoPreferences SmoothVideoProfile = (new UsbVideoPreferences { Mode = VideoPlaybackMode.Smooth }).Resolve();
    readonly CancellationTokenSource stop = new();
    readonly object gate = new();
    CancellationTokenSource? transportCancellation;
    sealed record Submission(Guid Id, string? Name, string Mode, long Started, string? UsageResourceId = null);
    readonly Guid sessionId = Guid.NewGuid();
    readonly Func<ILogger, bool, ID92PlaybackTransport> createTransport;
    readonly Func<string, CancellationToken, Task<D92VideoFrames>>? openVideo;
    byte[] frame;
    Submission submission = new(Guid.NewGuid(), null, "wallpaper", Stopwatch.GetTimestamp());
    Guid requestedSubmissionId;

    public D92StaticOutput(string serial, int rotation, byte[] jpeg, bool identification, string model = "D92", bool allowD69Video = false)
        : this(serial, rotation, jpeg, identification, (logger, video) => new D92PlaybackTransport(logger, video, model, serial, allowD69Video), null,
            model, allowD69Video)
    {
        Model = model;
        if (model == "D69")
        {
            _ = D92StaticImage.Geometry(rotation, model);
            D92StaticImage.RequireTransportRaster(jpeg, model);
        }
    }

    internal string Model { get; private set; } = "D92";
    internal bool D69VideoEnabled { get; }

    internal D92StaticOutput(string serial, int rotation, byte[] jpeg, bool identification,
        Func<ILogger, bool, ID92PlaybackTransport> transport, Func<string, CancellationToken, Task<D92VideoFrames>>? decoder,
        string model = "D92", bool allowD69Video = false)
    {
        Serial = serial;
        Rotation = rotation;
        frame = jpeg;
        Identification = identification;
        createTransport = transport;
        openVideo = decoder;
        Model = model;
        D69VideoEnabled = allowD69Video;
    }

    async Task<D92VideoFrames> OpenVideo(string path, UsbVideoPreferences profile, CancellationToken token)
    {
        var geometry = D92StaticImage.Geometry(Rotation, Model);
        var decode = VideoDecodeSize;
        var composition = CreateVideoComposition?.Invoke();
        var video = await VideoJpegFrameSource.OpenAsync(path, decode?.Width ?? geometry.Width, decode?.Height ?? geometry.Height, composition != null || decode == null ? geometry.Rotation : 0,
            quality: profile.JpegQuality, token: token, backend: VideoFrameBackend.ContinuousFrameServer,
            composition: composition, captureDiagnostics: true, encodingPreferences: profile, reuseEncoderBitmaps: ReuseVideoBitmaps).ConfigureAwait(false);
        var pipeline = new VideoFramePipeline(video);
        VideoSampleTiming? timing = null;
        return new(async token =>
            {
                video.SetPreviewRequest(Volatile.Read(ref previewRequest));
                var frame = await pipeline.ReadAsync(token).ConfigureAwait(false);
                timing = frame.Timing;
                Volatile.Write(ref videoPreview, frame.Preview);
                return frame.Bytes;
            },
            async () =>
            {
                try { await pipeline.DisposeAsync().ConfigureAwait(false); }
                finally { await video.DisposeAsync().ConfigureAwait(false); }
            }, pipeline.SetPausedAsync, () => timing, video.Duration);
    }

    internal Func<byte[], byte[]>? TransformVideoFrame { get; set; }
    internal (int Width, int Height)? VideoDecodeSize { get; set; }
    internal Func<IVideoFrameComposition>? CreateVideoComposition { get; set; }
    internal bool ReuseVideoBitmaps { get; set; } = true;

    public event Action<D92Diagnostic>? Diagnostic;
    public event Action<D92VideoPerformance>? VideoPerformance;
    public Guid SubmissionId { get { lock (gate) return submission.Id; } }
    public Guid RequestedSubmissionId { get { lock (gate) return requestedSubmissionId; } }
    string? videoPath;
    long frames;
    int connections;
    VideoThroughput? videoRate;
    D92VideoProgress? videoProgress;
    bool videoPaused;
    bool videoHasWritten;
    BitmapSource? videoPreview;
    public BitmapSource? VideoPreview => Volatile.Read(ref videoPreview);
    VideoPreviewRequest previewRequest = VideoPreviewRequest.Disabled;
    public void SetVideoPreviewRequest(VideoPreviewRequest request) { request.Validate(); Volatile.Write(ref previewRequest, request); }
    UsbVideoPreferences activeVideoProfile = VideoProfile;

    public string? VideoPath => Volatile.Read(ref videoPath);
    public byte[] CurrentStill => Volatile.Read(ref frame);
    public string Serial { get; }
    public int Rotation { get; }
    public bool Identification { get; }
    public bool IsStopping => stop.IsCancellationRequested;
    public long Frames => Interlocked.Read(ref frames);
    public int Connections => Volatile.Read(ref connections);
    public VideoThroughput? VideoRate => Volatile.Read(ref videoRate);
    public D92VideoProgress? VideoProgress => Volatile.Read(ref videoProgress);
    public bool VideoPaused => Volatile.Read(ref videoPaused);
    public bool VideoHasWritten => Volatile.Read(ref videoHasWritten);
    public UsbVideoPreferences ActiveVideoProfile => Volatile.Read(ref activeVideoProfile);

    public void ReplaceFrame(byte[] bytes) => SubmitStill(bytes, null, "wallpaper");
    internal int LiveFramesPerSecond { get; set; } = 1;

    public bool RefreshStill(Guid id, byte[] jpeg)
    {
        lock (gate)
        {
            if (IsStopping || videoPath != null || submission.Id != id || submission.Mode != "clock") return false;
            Volatile.Write(ref frame, jpeg);
            return true;
        }
    }

    public void SubmitStill(byte[] bytes, string? name, string mode, string? usageResourceId = null)
    {
        lock (gate)
        {
            Volatile.Write(ref frame, StillHeartbeatOnly && Model == "D92" ? (byte[])bytes.Clone() : bytes);
            if (mode != "preview") restoreStill = null;
            PublishSubmission(name, mode, bytes.Length, usageResourceId);
            requestedSubmissionId = submission.Id;
            SetModeLocked(null);
        }
    }

    public void SetVideo(string path) => SubmitVideo(path, null);

    public bool SetVideoPaused(bool paused)
    {
        lock (gate)
        {
            if (IsStopping || videoPath == null || !VideoHasWritten) return false;
            Volatile.Write(ref videoPaused, paused);
            Volatile.Write(ref videoRate, null);
            return true;
        }
    }

    public void SubmitVideo(string path, string? name, byte[]? fallback = null, string? fallbackName = null,
        UsbVideoPreferences? profile = null, string? usageResourceId = null)
    {
        if (Model == "D69" && !D69VideoEnabled) throw new NotSupportedException("D69 视频输出尚未完成实机验证。");
        if (Model == "D69")
        {
            var resolved = (profile ?? new UsbVideoPreferences(10, true, 80)).Resolve();
            profile = resolved with { Mode = VideoPlaybackMode.Custom,
                FramesPerSecond = Math.Min(24, resolved.FramesPerSecond) };
        }
        lock (gate)
        {
            if (fallback != null) Volatile.Write(ref frame, fallback);
            restoreStill = fallbackName;
            Volatile.Write(ref activeVideoProfile, profile ?? VideoProfile);
            PublishSubmission(name, "video", null, usageResourceId);
            requestedSubmissionId = submission.Id;
            Volatile.Write(ref videoPath, path);
            transportCancellation?.Cancel();
        }
    }

    string? restoreStill;

    void PublishSubmission(string? name, string mode, int? length, string? usageResourceId = null)
    {
        Volatile.Write(ref videoPreview, null);
        Volatile.Write(ref videoRate, null);
        Volatile.Write(ref videoProgress, null);
        Volatile.Write(ref videoPaused, false);
        Volatile.Write(ref videoHasWritten, false);
        submission = new(Guid.NewGuid(), name, mode, Stopwatch.GetTimestamp(), usageResourceId);
        Log("submitted", submission, length, frameGeometry: mode != "video");
    }

    void SetModeLocked(string? path)
    {
        if (videoPath == path) return;
        Volatile.Write(ref videoPath, path);
        transportCancellation?.Cancel();
    }

    void Log(string name, Submission current, int? bytes = null, string? detail = null,
        bool frameGeometry = false, long? firstWrite = null)
    {
        Diagnostic?.Invoke(new(DateTimeOffset.Now, sessionId, current.Id, name, Serial, Rotation, current.Name, current.Mode,
            frameGeometry ? Model == "D69" ? 480 : D92StaticImage.TransportWidth : null,
            frameGeometry ? Model == "D69" ? 1280 : D92StaticImage.TransportHeight : null,
            bytes, firstWrite, Frames, Connections, detail, firstWrite.HasValue ? current.UsageResourceId : null));
    }

    public void Stop() => stop.Cancel();
    public void Dispose() => stop.Dispose();

    public Task RunAsync(Action<D92OutputState> report) => Task.Run(async () =>
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        if (Identification) stop.CancelAfter(TimeSpan.FromSeconds(8));

        Guid? lastWritten = null;
        while (!IsStopping)
        {
            using var session = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            string? path;
            UsbVideoPreferences profile;
            Guid requestedId;
            lock (gate)
            {
                path = videoPath;
                profile = activeVideoProfile;
                requestedId = submission.Id;
                transportCancellation = session;
            }

            D92VideoFrames? video = null;
            Submission? rendered = null;
            var sender = createTransport(logger, path != null || LiveFramesPerSecond > 1);
            var rate = path == null ? null : new VideoRateController(profile.FramesPerSecond, profile.AutoReduceFrameRate);
            var rateClock = Stopwatch.StartNew();
            long lastDelivery = 0;
            Task connectionChange = Task.CompletedTask;
            bool disconnected = false;
            bool decoderPaused = false;
            byte[]? lastVideoFrame = null;
            byte[]? renderedVideoFrame = null;
            VideoSampleTiming? lastVideoTiming = null;
            VideoSampleTiming? deliveredTiming = null;
            double pacingMs = 0, decodeTotal = 0, compositionTotal = 0, rotationTotal = 0, jpegTotal = 0,
                usbTotal = 0, pacingTotal = 0, byteTotal = 0;
            int performanceSamples = 0;
            bool transportBlocked = false;
            var previousFrames = Frames;
            var previousConnections = Connections;
            sender.FrameCompleted += result =>
            {
                Interlocked.Exchange(ref frames, previousFrames + result.Frame);
                if (rendered == null) return;
                if (rendered.Id != lastWritten)
                {
                    lastWritten = rendered.Id;
                    Log("usb-first-write", rendered, result.JpegBytes, frameGeometry: true,
                        firstWrite: (long)Stopwatch.GetElapsedTime(rendered.Started).TotalMilliseconds);
                }
                if (rate == null) return;
                lock (gate)
                {
                    if (rendered.Id != submission.Id) return;
                    lastVideoFrame = renderedVideoFrame;
                    lastVideoTiming = deliveredTiming;
                    Volatile.Write(ref videoHasWritten, true);
                    if (deliveredTiming is { } delivered)
                        Volatile.Write(ref videoProgress, new(delivered.MediaPosition, video?.Duration));
                    if (VideoPaused || decoderPaused) {
                        rate.Reset(); performanceSamples = 0;
                        decodeTotal = compositionTotal = rotationTotal = jpegTotal = usbTotal = pacingTotal = byteTotal = 0;
                        return;
                    }
                    var stages = deliveredTiming?.Stages;
                    performanceSamples++;
                    decodeTotal += deliveredTiming?.DecodeMilliseconds ?? 0;
                    compositionTotal += stages?.CompositionMs ?? 0;
                    rotationTotal += stages?.RotationMs ?? 0;
                    jpegTotal += stages?.JpegEncodeMs ?? deliveredTiming?.EncodeMilliseconds ?? 0;
                    usbTotal += result.WriteMilliseconds; pacingTotal += pacingMs; byteTotal += result.JpegBytes;
                    var preparation = deliveredTiming == null ? 0 :
                        Math.Max(0, deliveredTiming.DecodeMilliseconds - (stages?.Native?.WaitMs ?? 0)) + deliveredTiming.EncodeMilliseconds;
                    var work = Math.Max(Math.Max(0, result.RenderMilliseconds - pacingMs), preparation) + result.WriteMilliseconds;
                    if (rate.Observe(rateClock.Elapsed.TotalSeconds, workMilliseconds: work) is { } measured)
                    {
                        Volatile.Write(ref videoRate, measured with
                        {
                            JpegBytes = result.JpegBytes, JpegQuality = deliveredTiming?.JpegQuality ?? profile.JpegQuality,
                            QualityReason = deliveredTiming?.QualityReason
                        });
                        VideoPerformance?.Invoke(new(DateTimeOffset.Now, Serial, rendered.Name, profile.FramesPerSecond,
                            measured.EffectiveFps, measured.ActualFps, performanceSamples, decodeTotal / performanceSamples,
                            compositionTotal / performanceSamples, rotationTotal / performanceSamples, jpegTotal / performanceSamples,
                            usbTotal / performanceSamples, pacingTotal / performanceSamples, byteTotal / performanceSamples));
                        performanceSamples = 0;
                        decodeTotal = compositionTotal = rotationTotal = jpegTotal = usbTotal = pacingTotal = byteTotal = 0;
                    }
                }
            };
            sender.StateChanged += result =>
            {
                Volatile.Write(ref connections, previousConnections + result.Connections);
                if (session.IsCancellationRequested && !IsStopping) return;
                if (result.Status == "BlockedByCraft") transportBlocked = true;
                if (rate != null && (result.Status is "WaitingForDevice" or "Connected"))
                {
                    performanceSamples = 0;
                    decodeTotal = compositionTotal = rotationTotal = jpegTotal = usbTotal = pacingTotal = byteTotal = 0;
                    rate.Reset();
                    Volatile.Write(ref videoRate, null);
                    lastDelivery = 0;
                    disconnected = result.Status == "WaitingForDevice";
                    SyncDecoderPause();
                }
                if (Identification && result is { Status: "Streaming", Frames: 1 })
                    stop.CancelAfter(TimeSpan.FromSeconds(2));
                if (result.Status != "Stopped" || IsStopping)
                {
                    Submission current;
                    lock (gate) current = submission;
                    if (result.Status != "Streaming")
                        Log(result.Status, current, detail: result.Error);
                    report(result with { Frames = previousFrames + result.Frames, Connections = previousConnections + result.Connections });
                }
            };

            try
            {
                using var precision = new VideoTimerResolution(path != null);
                if (path != null)
                {
                    try
                    {
                        video = await (openVideo?.Invoke(path, session.Token) ?? OpenVideo(path, profile, session.Token)).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!session.IsCancellationRequested)
                    { throw new VideoPlaybackFailure("无法打开视频。", ex); }
                }

                await sender.RunAsync(Serial, RenderDisplayFrame,
                    path == null ? TimeSpan.FromSeconds(1d / Math.Clamp(LiveFramesPerSecond, 1, 12)) : TimeSpan.FromSeconds(1d / profile.FramesPerSecond),
                    session.Token).ConfigureAwait(false);
                if (transportBlocked) break;
                if (!session.IsCancellationRequested) break;
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested) { }
            catch (VideoPlaybackFailure ex) when (!IsStopping)
            {
                lock (gate)
                {
                    if (videoPath == path && submission.Id == requestedId)
                    {
                        Log("video-failed", submission, detail: ex.InnerException?.Message ?? ex.Message);
                        Volatile.Write(ref videoPath, null);
                        PublishSubmission(restoreStill, "wallpaper", frame.Length);
                    }
                }
            }
            finally
            {
                lock (gate) transportCancellation = null;
                if (video != null)
                {
                    try
                    {
                        try { await connectionChange.ConfigureAwait(false); }
                        finally { await video.Dispose().ConfigureAwait(false); }
                    }
                    catch (Exception ex) { logger.Warning(ex, "D92 video pipeline cleanup"); }
                }
            }

            async Task<byte[]> RenderDisplayFrame(CancellationToken token)
            {
                SyncDecoderPause();
                if (DisplayStandby)
                {
                    lock (gate) rendered = submission;
                    await Task.Delay(1000, token).ConfigureAwait(false);
                    return DisplayFrame(CurrentStill, true);
                }
                return DisplayFrame(await RenderFrame(token).ConfigureAwait(false), false);
            }

            async Task<byte[]> RenderFrame(CancellationToken token)
            {
                pacingMs = 0;
                lock (gate)
                {
                    token.ThrowIfCancellationRequested();
                    if (session.IsCancellationRequested) throw new OperationCanceledException(session.Token);
                    rendered = submission;
                    if (video == null) return frame;
                }
                try
                {
                    SyncDecoderPause();
                    await connectionChange.WaitAsync(token).ConfigureAwait(false);
                    if (decoderPaused && lastVideoFrame != null)
                    {
                        // Hold the last written picture at a lower cadence and check for resume promptly.
                        while (VideoPaused && Stopwatch.GetElapsedTime(lastDelivery) < TimeSpan.FromSeconds(1))
                            await Task.Delay(50, token).ConfigureAwait(false);
                        SyncDecoderPause();
                        await connectionChange.WaitAsync(token).ConfigureAwait(false);
                        if (decoderPaused)
                        {
                            lastDelivery = Stopwatch.GetTimestamp();
                            deliveredTiming = lastVideoTiming;
                            renderedVideoFrame = lastVideoFrame;
                            return lastVideoFrame;
                        }
                    }
                    if (rate != null && lastDelivery != 0 && rate.EffectiveFps < profile.FramesPerSecond)
                    {
                        var wait = TimeSpan.FromSeconds(1d / rate.EffectiveFps) - Stopwatch.GetElapsedTime(lastDelivery);
                        if (wait > TimeSpan.Zero)
                        {
                            var pacingStart = Stopwatch.GetTimestamp();
                            await Task.Delay(wait, token).ConfigureAwait(false);
                            pacingMs = Stopwatch.GetElapsedTime(pacingStart).TotalMilliseconds;
                        }
                    }
                    var bytes = await video.Render(token).ConfigureAwait(false);
                    if (TransformVideoFrame is { } transform) bytes = transform(bytes);
                    deliveredTiming = video.Timing?.Invoke();
                    renderedVideoFrame = bytes;
                    lastDelivery = Stopwatch.GetTimestamp();
                    return bytes;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { throw new VideoPlaybackFailure("视频解码失败。", ex); }
            }

            void SyncDecoderPause()
            {
                var desired = disconnected || VideoPaused || DisplayStandby;
                if (desired == decoderPaused) return;
                decoderPaused = desired;
                rate?.Reset();
                if (!desired) lastDelivery = 0;
                if (video?.SetPaused is { } pause)
                    connectionChange = ChangeConnection(connectionChange, pause, desired);
            }
        }
    });

    static async Task ChangeConnection(Task previous, Func<bool, Task> pause, bool offline)
    {
        await previous.ConfigureAwait(false);
        await pause(offline).ConfigureAwait(false);
    }

    sealed class VideoPlaybackFailure(string message, Exception cause) : Exception(message, cause);
}

internal sealed record D92VideoFrames(Func<CancellationToken, Task<byte[]>> Render, Func<ValueTask> Dispose,
    Func<bool, Task>? SetPaused = null, Func<VideoSampleTiming?>? Timing = null, TimeSpan? Duration = null);

internal sealed record D92VideoProgress(TimeSpan Position, TimeSpan? Duration);
