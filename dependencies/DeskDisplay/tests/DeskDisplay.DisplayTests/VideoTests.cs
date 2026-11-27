using System.IO;
using System.Windows;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using Serilog;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace DeskDisplay.DisplayTests;

internal static class VideoTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var path = Path.Combine(Path.GetTempPath(), "DeskDisplay-video-test-" + Guid.NewGuid().ToString("N") + ".mp4");
        using var playback = new WindowsVideoPlaybackService(logger);
        await playback.OpenAsync(path, 30);
        Check(!playback.IsPlaying && playback.Status.Contains("不存在"), "Missing video produces a friendly failure");
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path));
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 255, 0, 0), TimeSpan.FromSeconds(0.5)));
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 0, 0, 255), TimeSpan.FromSeconds(0.5)));
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
        profile.Video.Width = 320; profile.Video.Height = 180;
        var result = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
        Check(result == TranscodeFailureReason.None, "Native Windows pipeline generates H.264 test fixture");
        var catalog = new DeskDisplay.Library.WallpaperCatalog(logger, path + ".catalog");
        var asset = await catalog.ImportAsync(path);
        Check(asset.IsVideo && asset.Width == 320 && asset.Height == 180 && asset.DurationSeconds > 0 && File.Exists(catalog.ThumbnailPath(asset.Id)),
            "Library imports MP4 metadata and thumbnail without opening a second player");
        var scene = new VideoScene(path, playback);
        var first = new SceneView { Scene = scene };
        var second = new SceneView { Scene = scene };
        var panel = new System.Windows.Controls.StackPanel();
        first.Height = 160; second.Height = 160;
        panel.Children.Add(first); panel.Children.Add(second);
        var window = new Window { Content = panel, Width = 500, Height = 400, ShowInTaskbar = false };
        try
        {
            window.Show();
            await playback.OpenAsync(path, 30);
            await WaitUntil(() => playback.IsPlaying, playback);
            Check(playback.Player is { IsMuted: true, Volume: 0 } && ReferenceEquals(((VideoScene)first.Scene!).Playback, ((VideoScene)second.Scene!).Playback),
                "Both views share one muted native player and timeline");
            playback.Pause();
            var pausedPosition = playback.Position;
            await Task.Delay(150);
            Check(playback.IsPaused && Math.Abs((playback.Position - pausedPosition).TotalMilliseconds) < 80, "Pause stops shared video timeline");
            playback.Seek(TimeSpan.FromSeconds(0.5));
            Check(Math.Abs(playback.Position.TotalSeconds - 0.5) < 0.1, "Seek changes shared playback position");
            playback.Resume();
            var preparedPath = playback.TemporaryPath!;
            var encoded = await MediaEncodingProfile.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(preparedPath));
            Check(encoded.Video.FrameRate.Numerator / encoded.Video.FrameRate.Denominator == 30, "30 FPS setting changes the encoded playback rate");
            await WaitUntil(() => playback.LoopCount >= 2, playback);
            Check(playback.IsPlaying, "Video automatically loops multiple times");
            await playback.OpenAsync(path, 60);
            await WaitUntil(() => playback.IsPlaying, playback);
            encoded = await MediaEncodingProfile.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(playback.TemporaryPath!));
            Check(encoded.Video.FrameRate.Numerator / encoded.Video.FrameRate.Denominator == 60, "60 FPS setting produces a 60 FPS playback file");
            playback.Stop();
            Check(playback.Player is null && !playback.IsPlaying, "Leaving video closes the native player");
            Check(!File.Exists(preparedPath), "Superseded playback copy was removed");
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(exclusive.Length > 0, "Source video is unlocked after playback stops");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await playback.OpenAsync(path, 30, cancel.Token);
            Check(playback.Player is null, "Cancelled preparation cannot start late playback");
        }
        finally { playback.Stop(); window.Close(); File.Delete(path); Directory.Delete(path + ".catalog", true); }
        Console.WriteLine("All video tests passed.");
    }
    private static async Task WaitUntil(Func<bool> condition, WindowsVideoPlaybackService playback)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(100);
        Check(condition(), "Video state reached: " + playback.Status);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
}
