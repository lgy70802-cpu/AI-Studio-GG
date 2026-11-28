using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiraBox.Core;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    sealed record VideoUpload(string Path, string Name, string Extension, long Expected, FileStream Stream);
    readonly Dictionary<string, VideoUpload> videoUploads = new();
    readonly HashSet<string> processingVideos = new();
    readonly SemaphoreSlim videoProcessing = new(1);
    async Task<object> ImportVideo(string command, JsonElement message)
    {
        if (command == "video-begin")
        {
            var size = message.GetProperty("size").GetInt64();
            var name = Path.GetFileName(message.GetProperty("name").GetString()!);
            var extension = Path.GetExtension(name).ToLowerInvariant();
            if (size <= 0 || size > VideoImportLimits.MaximumBytes) throw new InvalidDataException("视频大小需在 0 至 500 MiB 之间。");
            if (!new[] { ".mp4", ".m4v", ".mov", ".webm" }.Contains(extension)) throw new InvalidDataException("请选择 MP4、M4V、MOV 或 WebM 视频。");
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(library.MediaRoot, id + ".upload");
            videoUploads[id] = new(path, name, extension, size, new FileStream(path, FileMode.CreateNew, FileAccess.Write));
            return id;
        }
        var uploadId = message.GetProperty("uploadId").GetString()!;
        if (!videoUploads.TryGetValue(uploadId, out var upload)) throw new InvalidOperationException("导入会话已结束，请重新选择文件。");
        if (command == "video-chunk")
        {
            var text = message.GetProperty("data").GetString()!;
            if (text.Length > 2 * 1024 * 1024) throw new InvalidDataException("导入分片过大。");
            var bytes = Convert.FromBase64String(text);
            if (upload.Stream.Length + bytes.Length > upload.Expected) throw new InvalidDataException("导入数据超出声明大小。");
            await upload.Stream.WriteAsync(bytes);
            return true;
        }
        videoUploads.Remove(uploadId);
        upload.Stream.Dispose();
        try
        {
            if (command == "video-cancel") return true;
            if (command != "video-finish" || new FileInfo(upload.Path).Length != upload.Expected)
                throw new InvalidDataException("视频数据不完整，请重新导入。");
            var temporary = upload.Path + upload.Extension;
            var converted = upload.Path + ".converted.mp4";
            var sourcePath = temporary;
            var outputExtension = upload.Extension;
            File.Move(upload.Path, temporary);
            processingVideos.Add(upload.Path);
            try
            {
                await videoProcessing.WaitAsync(shutdown.Token);
                VideoMetadata metadata;
                try
                {
                    var probe = new WindowsVideoProbe();
                    var needsConversion = await VideoCompatibilityConverter.IsHevcAsync(temporary, shutdown.Token);
                    VideoMetadata? nativeMetadata = null;
                    if (!needsConversion)
                    {
                        try { nativeMetadata = await probe.ProbeAsync(temporary); }
                        catch (Exception error) when (error.HResult == unchecked((int)0xC00D109B) ||
                            error is System.Runtime.InteropServices.COMException or InvalidDataException or TimeoutException or NotSupportedException)
                        {
                            Log(new { at = DateTimeOffset.Now, eventType = "video-import-compatibility", code = error.HResult.ToString("X8") });
                        }
                    }
                    if (nativeMetadata == null)
                    {
                        await VideoCompatibilityConverter.ConvertAsync(sourcePath, converted, shutdown.Token);
                        sourcePath = converted;
                        outputExtension = ".mp4";
                    }
                    metadata = nativeMetadata ?? await probe.ProbeAsync(sourcePath);
                }
                finally { videoProcessing.Release(); }
                if (metadata.Duration <= TimeSpan.Zero) throw new InvalidDataException("视频时长无效。");
                string hash;
                using (var source = File.OpenRead(temporary))
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(source)).ToLowerInvariant();
                var media = hash + (sourcePath == converted ? ".compatible" : "") + outputExtension;
                var thumbnail = hash + ".video.jpg";
                if (!File.Exists(Path.Combine(library.MediaRoot, media))) File.Move(sourcePath, Path.Combine(library.MediaRoot, media));
                File.WriteAllBytes(Path.Combine(library.MediaRoot, thumbnail), metadata.Thumbnail);
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return new {
                    id = "video-" + Guid.NewGuid().ToString("N"), name = Path.GetFileNameWithoutExtension(upload.Name),
                    displayName = Path.GetFileNameWithoutExtension(upload.Name), category = "视频", source = "local", inLibrary = true,
                    imageUrl = "https://media.studio.local/" + thumbnail, thumbnailUrl = "https://media.studio.local/" + thumbnail,
                    videoUrl = "https://media.studio.local/" + media, durationSeconds = metadata.Duration.TotalSeconds,
                    sourceWidth = metadata.Dimensions.Width, sourceHeight = metadata.Dimensions.Height,
                    resolution = metadata.Dimensions.Width + " × " + metadata.Dimensions.Height,
                    orientation = metadata.Dimensions.Width >= metadata.Dimensions.Height ? "landscape" : "portrait",
                    contentHash = hash, author = "本地导入", clockStyle = "none", desktopStatic = true,
                    downloads = "本地导入", downloadCount = 0, publishedAt = now, addedAt = now, lastUsedAt = now,
                    releasedLabel = "刚刚导入", addedLabel = "刚刚导入", useCount = 0, durationMinutes = 30,
                    inPlaylist = false, isFavorite = false, isRecent = true, accentHex = "#5E45F8"
                };
            }
            finally
            {
                processingVideos.Remove(upload.Path);
                File.Delete(temporary);
                File.Delete(converted);
            }
        }
        finally { if (File.Exists(upload.Path)) File.Delete(upload.Path); }
    }
    string? PreviewData(SavedDevice device)
    {
        if (themePreviews.TryGetValue(device.Serial, out var themePreview)) return themePreview;
        if (outputs.TryGetValue(device.Serial, out var output) && output.VideoPath != null)
        {
            output.SetVideoPreviewRequest(device.Serial == selected
                ? new DeskDisplay.Core.Models.VideoPreviewRequest(true, 800, 800, 1)
                : DeskDisplay.Core.Models.VideoPreviewRequest.Disabled);
            if (output.VideoPreview is { } frame)
            {
                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 80 };
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
                using var memory = new MemoryStream();
                encoder.Save(memory);
                return "data:image/jpeg;base64," + Convert.ToBase64String(memory.ToArray());
            }
        }
        return device.ImageFile != null && File.Exists(ImagePath(device))
            ? "data:image/jpeg;base64," + Convert.ToBase64String(File.ReadAllBytes(ImagePath(device))) : null;
    }
    string ManagedVideoPath(SavedDevice device)
    {
        var path = Path.GetFullPath(Path.Combine(dataRoot, device.VideoFile ?? throw new InvalidOperationException("没有视频文件。")));
        if (!path.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("视频路径超出工作区。");
        return path;
    }
}
