using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Display.Services;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace DeskDisplay.DisplayTests;

internal static class VideoFramePrototypeTests
{
    public static async Task RunAsync()
    {
        var pixels = new byte[3 * 2 * 4];
        for (var i = 0; i < 6; i++) { pixels[i * 4] = (byte)(i + 1); pixels[i * 4 + 3] = 255; }
        var original = BitmapSource.Create(3, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 12); original.Freeze();
        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            var rotated = VideoJpegFrameSource.RotatePixels(original, rotation);
            var expected = rotation switch { 0 => new byte[] { 1, 2, 3, 4, 5, 6 }, 90 => new byte[] { 4, 1, 5, 2, 6, 3 }, 180 => new byte[] { 6, 5, 4, 3, 2, 1 }, _ => new byte[] { 3, 6, 2, 5, 1, 4 } };
            var actual = new byte[pixels.Length]; rotated.CopyPixels(actual, rotated.PixelWidth * 4, 0);
            Check(rotated.IsFrozen && Enumerable.Range(0, 6).All(i => actual[i * 4] == expected[i]) && rotated.PixelWidth == (rotation is 90 or 270 ? 2 : 3), $"Materialized {rotation} degree rotation preserves asymmetric pixel positions and dimensions");
        }
        using (var log = new Serilog.LoggerConfiguration().CreateLogger())
        {
            var normal = new DeskDisplay.Display.Usb.D92HidRenderTarget(log);
            await Throws<ArgumentOutOfRangeException>(() => normal.RunAsync("test-only", _ => Task.FromResult(Array.Empty<byte>()), TimeSpan.FromSeconds(1d / 30), new CancellationToken(true)), "Normal HID callers retain the 10 FPS ceiling");
            var diagnostic = new DeskDisplay.Display.Usb.D92HidRenderTarget(log, experimentalHighFrameRate: true);
            await Throws<ArgumentOutOfRangeException>(() => diagnostic.RunAsync("test-only", _ => Task.FromResult(Array.Empty<byte>()), TimeSpan.FromSeconds(1d / 31), new CancellationToken(true)), "Explicit diagnostic mode still rejects rates above 30 FPS");
        }
        var evidence = Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_EVIDENCE");
        var root = evidence ?? Path.Combine(Path.GetTempPath(), "D92-video-frames-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(root)) throw new IOException("Video test evidence must use a new directory.");
        Directory.CreateDirectory(root);
        try
        {
            var file = await (await StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(root))).CreateFileAsync("native-color-cycle.mp4");
            var composition = new MediaComposition();
            composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 240, 20, 20), TimeSpan.FromSeconds(1)));
            composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 20, 20, 240), TimeSpan.FromSeconds(1)));
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga); profile.Video.Width = 384; profile.Video.Height = 92; profile.Audio = null;
            Check(await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile) == TranscodeFailureReason.None, "Native encoder creates a real two-second H.264 video fixture");
            composition.Clips.Clear();
            await using (var source = await WindowsVideoFrameSource.OpenAsync(file.Path))
            {
                var red = await source.ReadAsync(TimeSpan.FromMilliseconds(250), 1920, 462, default);
                var blue = await source.ReadAsync(TimeSpan.FromMilliseconds(1250), 1920, 462, default);
                Check(red.Pixels.Span[2] > 180 && red.Pixels.Span[0] < 70 && blue.Pixels.Span[0] > 180 && blue.Pixels.Span[2] < 70, "Native timestamp reads return different decoded red and blue frames rather than one poster image");
                Check(Math.Abs(red.Width / (double)red.Height - 384d / 92) < .03, "Native frame sampling preserves the original video aspect ratio");
                await Throws<OperationCanceledException>(() => source.ReadAsync(TimeSpan.Zero, 1920, 462, new CancellationToken(true)), "Cancelled frame requests do not return successful pixels");
                await Throws<ArgumentOutOfRangeException>(() => source.ReadAsync(source.Duration, 1920, 462, default), "Out-of-range media positions fail before decoding");
            }
            Check(VideoFrameClock.LoopPosition(TimeSpan.FromSeconds(5.25), TimeSpan.FromSeconds(2)) == TimeSpan.FromSeconds(1.25), "Video loop positions follow elapsed wall time and skip obsolete frames");
            await using (var source = await VideoJpegFrameSource.OpenAsync(file.Path, 1920, 462, 90))
            {
                var first = source.RenderAsync(default);
                await Throws<InvalidOperationException>(() => source.RenderAsync(default), "Parallel frame requests are rejected instead of creating an output backlog");
                var jpeg = await first;
                var initial = source.LastTiming!;
                using (var stream = new MemoryStream(jpeg))
                {
                    var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    Check(bitmap.PixelWidth == 462 && bitmap.PixelHeight == 1920, "Video frame JPEG uses the target resolution and applies transport rotation exactly once");
                }
                // Await a known content phase, even when the first native decode is cold/slow.
                var until = DateTime.UtcNow.AddSeconds(8);
                while ((source.Position.TotalSeconds < 1.15 || source.Position.TotalSeconds > 1.45) && DateTime.UtcNow < until) await Task.Delay(30);
                var second = await source.RenderAsync(default);
                Check(!SHA256.HashData(jpeg).SequenceEqual(SHA256.HashData(second)), "Live JPEG output advances through actual video content");
                Check((source.LastTiming!.MediaPosition - initial.MediaPosition).TotalMilliseconds > 900, "Slow sending advances to current media time without slowing video playback");
                await File.WriteAllBytesAsync(Path.Combine(root, "sample-red.jpg"), jpeg); await File.WriteAllBytesAsync(Path.Combine(root, "sample-blue.jpg"), second);
                await File.WriteAllTextAsync(Path.Combine(root, "sample-metrics.json"), JsonSerializer.Serialize(new { Video = file.Path, source.Duration, Initial = initial, Final = source.LastTiming, Backend = "Windows MediaComposition timestamp sampling; no HID" }, new JsonSerializerOptions { WriteIndented = true }));
            }
            var released = await VideoJpegFrameSource.OpenAsync(file.Path, 1920, 462, 0);
            var pending = released.RenderAsync(default); await released.DisposeAsync();
            try { await pending; } catch (OperationCanceledException) { }
            await Throws<ObjectDisposedException>(() => released.RenderAsync(default), "Disposal cancels pending work and rejects new video frames");
            await released.DisposeAsync(); Check(true, "Video source disposal is safe to repeat");
            await Throws<FileNotFoundException>(() => VideoJpegFrameSource.OpenAsync(Path.Combine(root, "missing.mp4"), 1920, 462, 0), "Missing video fails while releasing the private rendering thread");
            await using (var live = await VideoJpegFrameSource.OpenAsync(file.Path, 1920, 462, 0, backend: VideoFrameBackend.ContinuousFrameServer))
            {
                await WaitPhase(live, .15, .55);
                var red = await live.RenderAsync(default); var redTiming = live.LastTiming!;
                await WaitPhase(live, 1.15, 1.55);
                var blue = await live.RenderAsync(default); var blueTiming = live.LastTiming!;
                Check(Center(red).R > 170 && Center(red).B < 70 && Center(blue).B > 170 && Center(blue).R < 70 && !SHA256.HashData(red).SequenceEqual(SHA256.HashData(blue)), "Continuous native frame server returns real changing video pixels without timestamp seeking");
                Check(redTiming.MediaPosition.TotalSeconds < 1 && blueTiming.MediaPosition.TotalSeconds > 1, "Continuous readback records the player's media position with each sample");
                await WaitPhase(live, .15, .55);
                var loopJpeg=await live.RenderAsync(default);var loopColor=Center(loopJpeg);
                Check(loopColor.R > 170 && loopColor.B < 70, $"Continuous playback loops to the first real video segment (R={loopColor.R}, B={loopColor.B}, media={live.LastTiming!.MediaPosition})");
                await Throws<OperationCanceledException>(() => live.RenderAsync(new CancellationToken(true)), "Continuous frame requests honor cancellation");
                await File.WriteAllTextAsync(Path.Combine(root, "live-metrics.json"), JsonSerializer.Serialize(new { Backend = live.Backend.ToString(), Red = redTiming, Blue = blueTiming, Hid = false }, new JsonSerializerOptions { WriteIndented = true }));
            }
            await using (var source = await VideoJpegFrameSource.OpenAsync(file.Path, 384, 92, 90, backend: VideoFrameBackend.ContinuousFrameServer))
            {
                var pipeline = new VideoFramePipeline(source);
                await WaitPhase(source, .15, .55);
                var first = await pipeline.ReadAsync(default);
                await WaitPhase(source, 1.15, 1.55);
                var current = await pipeline.ReadAsync(default);
                Check(Center(first.Bytes).R > 170 && Center(current.Bytes).B > 170 && current.Timing.MediaPosition > TimeSpan.FromSeconds(1), "One-frame pipeline drops expired prepared frames and resumes at the current video phase");
                Check(current.Preview.IsFrozen && current.Preview.PixelWidth == 384 && current.Preview.PixelHeight == 92, "Pipelined preview retains unrotated native geometry independently of the sent JPEG");
                await pipeline.SetPausedAsync(true);
                var pausedFrame = await pipeline.ReadAsync(default); await Task.Delay(180);
                var pausedAgain = await pipeline.ReadAsync(default);
                Check(pausedFrame.Bytes.SequenceEqual(pausedAgain.Bytes) && ReferenceEquals(pausedFrame.Preview, pausedAgain.Preview), "Pipelined pause freezes the exact JPEG and preview without preparing a backlog");
                await pipeline.SetPausedAsync(false); await pipeline.ReadAsync(default);
                await pipeline.DisposeAsync(); await pipeline.DisposeAsync();
                await Throws<ObjectDisposedException>(() => pipeline.ReadAsync(default), "Disposing an active one-frame pipeline is repeatable and rejects later reads");
            }
            var motion = await MakeMotionAsync(root);
            await using (var sampledMotion = await WindowsVideoFrameSource.OpenAsync(motion))
            {
                var hashes = new HashSet<string>();
                foreach (var time in new[] { .1, .6, 1.1, 1.6 }) { var frame = await sampledMotion.ReadAsync(TimeSpan.FromSeconds(time), 1920, 462, default); hashes.Add(Convert.ToHexString(SHA256.HashData(frame.Pixels.Span))); }
                Check(hashes.Count == 4, "Full-resolution moving-pattern video has four distinct decoded content samples");
            }
        }
        finally { if (evidence is null) Directory.Delete(root, true); }
    }
    private static async Task Throws<T>(Func<Task> action, string message) where T : Exception
    { try { await action(); } catch (T) { Check(true, message); return; } throw new InvalidOperationException(message); }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private static async Task WaitPhase(VideoJpegFrameSource source, double low, double high)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while ((source.Position.TotalSeconds < low || source.Position.TotalSeconds > high) && DateTime.UtcNow < until) await Task.Delay(20);
        if (DateTime.UtcNow >= until) throw new TimeoutException("Native player did not advance to the expected media phase.");
    }
    private static (byte R, byte B) Center(byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg); var bitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap(BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var bytes = new byte[4]; bitmap.CopyPixels(new System.Windows.Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), bytes, 4, 0); return (bytes[2], bytes[0]);
    }
    private static async Task<string> MakeMotionAsync(string root)
    {
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(root));
        var composition = new MediaComposition();
        var changesPerSecond = Environment.GetEnvironmentVariable("DESKDISPLAY_VIDEO_MOTION_FPS") == "30" ? 30 : 4;
        for (var index = 0; index < changesPerSecond * 3; index++)
        {
            var visual = new System.Windows.Media.DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(System.Windows.Media.Brushes.MidnightBlue, null, new System.Windows.Rect(0, 0, 1920, 462));
                for (var cell = 0; cell < 220; cell++)
                {
                    var color = System.Windows.Media.Color.FromRgb((byte)((cell * 37 + index * 17) % 256), (byte)((cell * 71 + index * 23) % 256), (byte)((cell * 19 + index * 31) % 256));
                    drawing.DrawRectangle(new System.Windows.Media.SolidColorBrush(color), null, new System.Windows.Rect((cell * 83 + index * 43) % 1900, 100 + cell % 9 * 36, 20 + cell % 13 * 3, 24));
                }
                var text = new System.Windows.Media.FormattedText($"D92 VIDEO  FRAME {index:D2}  {index / (double)changesPerSecond:F2}s", System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, new System.Windows.Media.Typeface("Consolas"), 54, System.Windows.Media.Brushes.White, 1);
                drawing.DrawText(text, new System.Windows.Point(24, 20));
            }
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1920, 462, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(visual);
            var image = Path.Combine(root, $"motion-{index:D2}.png"); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(image)) encoder.Save(stream);
            composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await StorageFile.GetFileFromPathAsync(image), TimeSpan.FromTicks(TimeSpan.TicksPerSecond / changesPerSecond)));
        }
        var video = await folder.CreateFileAsync("native-motion.mp4"); var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p); profile.Video.Width = 1920; profile.Video.Height = 462; profile.Audio = null;
        profile.Video.FrameRate.Numerator = 30; profile.Video.FrameRate.Denominator = 1;
        Check(await composition.RenderToFileAsync(video, MediaTrimmingPreference.Precise, profile) == TranscodeFailureReason.None, "Native encoder creates full-resolution moving patterns with visible frame numbers");
        await File.WriteAllTextAsync(Path.Combine(root, "motion-fixture.json"), JsonSerializer.Serialize(new { Width = 1920, Height = 462, EncodedFps = 30, ContentChangesPerSecond = changesPerSecond, FrameCount = changesPerSecond * 3 }, new JsonSerializerOptions { WriteIndented = true }));
        composition.Clips.Clear(); return video.Path;
    }
}
