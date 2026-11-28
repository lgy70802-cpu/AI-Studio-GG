using System.Diagnostics;
using System.IO;
using MiraBox.Core;

namespace MiraBox.App;

internal static class VideoCompatibilityConverter
{
    static string Tool(string name) => Path.Combine(AppContext.BaseDirectory, "VideoConverter", name + ".exe");

    public static async Task<bool> IsHevcAsync(string source, CancellationToken cancellationToken)
    {
        var probe = Tool("ffprobe");
        if (!File.Exists(probe)) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var (status, output, _) = await Run(probe,
                ["-v", "error", "-protocol_whitelist", "file", "-select_streams", "v:0",
                 "-show_entries", "stream=codec_name", "-of", "default=noprint_wrappers=1:nokey=1", source], timeout.Token);
            return status == 0 && output.Trim() == "hevc";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public static async Task ConvertAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var tool = Tool("ffmpeg");
        if (!File.Exists(tool))
            throw new InvalidDataException("系统缺少此视频的解码器。请使用带视频转换组件的版本，或将视频导出为 H.264 MP4。");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var (encoderStatus, encoders, _) = await Run(tool, ["-hide_banner", "-encoders"], timeout.Token);
        var encoder = encoderStatus == 0
            ? new[] { "h264_mf", "libopenh264", "libx264" }.FirstOrDefault(candidate =>
                encoders.Split('\n').Any(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() == candidate))
            : null;
        if (encoder == null) throw new InvalidDataException("视频转换组件无法使用，请重新安装软件或使用 H.264 MP4。");

        try
        {
            var (status, _, detail) = await Run(tool,
                ["-hide_banner", "-nostdin", "-v", "error", "-protocol_whitelist", "file", "-i", source,
                 "-map", "0:v:0", "-map", "0:a:0?", "-map_metadata", "-1", "-map_chapters", "-1",
                 "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-pix_fmt", encoder == "h264_mf" ? "nv12" : "yuv420p",
                 "-c:v", encoder, "-b:v", "8M", "-c:a", "aac", "-b:a", "160k",
                 "-movflags", "+faststart", destination], timeout.Token);
            if (status != 0 || !File.Exists(destination) || new FileInfo(destination).Length == 0)
                throw new InvalidDataException("兼容转换失败，请检查文件或导出为 H.264 MP4。", new IOException(detail.Trim()));
            if (new FileInfo(destination).Length >= VideoImportLimits.MaximumBytes)
                throw new VideoLimitException("转换后的视频达到 500 MiB 限制，请降低源视频码率或缩短时长。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("视频兼容转换超时，请先导出为 H.264 MP4。");
        }
    }

    static async Task<(int Status, string Output, string Error)> Run(string tool, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(tool) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("无法启动视频转换组件。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        return (process.ExitCode, await output, await error);
    }
}
