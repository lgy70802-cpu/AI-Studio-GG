using System.IO;
using System.Text;
using System.Text.Json;

namespace MiraBox.App;

internal sealed record D92VideoPerformance(DateTimeOffset At, string Serial, string? Content, int RequestedFps,
    int EffectiveFps, double ActualFps, int Samples, double DecodeMs, double CompositionMs, double RotationMs,
    double JpegMs, double UsbWriteMs, double PacingMs, double AverageJpegBytes);

internal sealed record D92Diagnostic(DateTimeOffset At, Guid SessionId, Guid? SubmissionId,
    string Event, string Serial, int Rotation, string? Content, string? Mode, int? Width, int? Height,
    int? Bytes, long? FirstWriteMilliseconds, long Frames, int Connections, string? Detail,
    string? UsageResourceId = null);

internal sealed class D92Diagnostics(string folder)
{
    const long MaximumBytes = 1024 * 1024;
    readonly object gate = new();
    public string Path => System.IO.Path.Combine(folder, "d92-output.jsonl");

    public void RecordVideoPerformance(D92VideoPerformance entry)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var path = System.IO.Path.Combine(folder, "d92-video-performance.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length >= MaximumBytes) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { System.Diagnostics.Trace.TraceWarning("D92 video performance write failed: {0}", ex.Message); }
        }
    }

    public void Record(D92Diagnostic entry)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                if (File.Exists(Path) && new FileInfo(Path).Length >= MaximumBytes)
                    File.Move(Path, Path + ".previous", overwrite: true);
                File.AppendAllText(Path, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning("D92 diagnostic write failed: {0}", ex.Message);
            }
        }
    }

    public string? LastWrittenContent(string serial)
    {
        lock (gate)
        {
            string? name = null;
            try
            {
                foreach (var file in new[] { Path + ".previous", Path }.Where(File.Exists))
                foreach (var line in File.ReadLines(file))
                {
                    D92Diagnostic? item;
                    try { item = JsonSerializer.Deserialize<D92Diagnostic>(line); }
                    catch (JsonException) { continue; }
                    if (item is { Event: "usb-first-write", Content: not null, Mode: "wallpaper" or "clock" or "video" } &&
                        item.Serial == serial) name = item.Content;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning("D92 diagnostic read failed: {0}", ex.Message);
            }
            return name;
        }
    }
}
