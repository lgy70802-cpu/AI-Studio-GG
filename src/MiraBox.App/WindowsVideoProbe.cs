using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

internal interface IVideoProbe
{
    Task<VideoMetadata> ProbeAsync(string path);
}

internal sealed class VideoLimitException(string message = "视频超过大小或时长限制") : Exception(message);

internal sealed class WindowsVideoProbe : IVideoProbe
{
    public async Task<VideoMetadata> ProbeAsync(string path)
    {
        var player = new MediaPlayer { Volume = 0, ScrubbingEnabled = true };
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? error = null;
        var ended = false;
        player.MediaOpened += (_, _) => opened.TrySetResult();
        player.MediaEnded += (_, _) => ended = true;
        player.MediaFailed += (_, args) =>
        {
            error = args.ErrorException ?? new InvalidDataException("系统解码失败。");
            opened.TrySetException(error);
        };
        try
        {
            player.Open(new Uri(Path.GetFullPath(path)));
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!player.HasVideo || !player.NaturalDuration.HasTimeSpan ||
                player.NaturalVideoWidth <= 0 || player.NaturalVideoHeight <= 0)
                throw new InvalidDataException("无法读取视频尺寸或时长。");
            var duration = player.NaturalDuration.TimeSpan;
            if (VideoImportLimits.Exceeded(0, duration)) throw new VideoLimitException();
            var size = new MiraBox.Core.Size(player.NaturalVideoWidth, player.NaturalVideoHeight);
            var sample = TimeSpan.FromTicks(Math.Min(TimeSpan.TicksPerSecond, duration.Ticks / 2));
            player.Position = sample;
            player.Play();
            // Let the media pipeline deliver a frame before rendering the visual.
            var ready = System.Diagnostics.Stopwatch.StartNew();
            while (!ended && player.Position <= sample && error == null && ready.Elapsed < TimeSpan.FromSeconds(4))
                await Task.Delay(40);
            if (error != null) throw error;
            if (!ended && player.Position <= sample) throw new TimeoutException("系统解码器未返回视频帧。");
            player.Pause();
            await Dispatcher.Yield(DispatcherPriority.Render);

            var scale = Math.Min(1, 480d / Math.Max(size.Width, size.Height));
            var width = Math.Max(1, (int)Math.Round(size.Width * scale));
            var height = Math.Max(1, (int)Math.Round(size.Height * scale));
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) context.DrawVideo(player, new Rect(0, 0, width, height));
            var frame = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            frame.Render(visual);
            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(frame));
            using var bytes = new MemoryStream();
            encoder.Save(bytes);
            return new(size, duration, bytes.ToArray());
        }
        finally { player.Close(); }
    }
}
