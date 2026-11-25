using System.Text.Json;
using DeskDisplay.Core.Models;
using Serilog;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace DeskDisplay.Library;

/// <summary>Local metadata and bounded shell thumbnails. Original files are referenced, never modified.</summary>
public sealed class WallpaperCatalog(ILogger logger, string? directory = null)
{
    private readonly string _directory = directory ?? Path.Combine(DeskDisplay.Core.Storage.StudioDataPaths.Root, "Library");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public bool Exists => File.Exists(Path.Combine(_directory, "library.json"));
    public string ThumbnailPath(Guid id) => Path.Combine(_directory, "Thumbnails", id.ToString("N") + ".thumb");
    public void RemoveThumbnail(Guid id)
    {
        try { File.Delete(ThumbnailPath(id)); }
        catch (Exception exception) { logger.Warning(exception, "Cannot delete unused thumbnail {Id}", id); }
    }
    public async Task<WallpaperLibrary> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var path = Path.Combine(_directory, "library.json");
            if (!File.Exists(path)) return new();
            await using var stream = File.OpenRead(path);
            var library = await JsonSerializer.DeserializeAsync<WallpaperLibrary>(stream);
            if (library is null || library.Version != 1 || library.Assets is null || library.Playlist is null) throw new JsonException("Unsupported library document");
            library.Assets = library.Assets.Where(item => item is not null && item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Path))
                .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).DistinctBy(item => item.Id).ToList();
            for (var index = 0; index < library.Assets.Count; index++)
            {
                var item = library.Assets[index];
                item.Name = string.IsNullOrWhiteSpace(item.Name) ? Path.GetFileNameWithoutExtension(item.Path) : item.Name;
                item.Category = string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category;
                item.Subcategory = string.IsNullOrWhiteSpace(item.Subcategory) ? "未分类" : item.Subcategory;
                item.Tags ??= "";
                if (item.AddedAt == default) item.AddedAt = DateTimeOffset.UnixEpoch.AddTicks(index + 1L);
            }
            library.Playlist = library.Playlist.Where(id => library.Assets.Any(item => item.Id == id)).Distinct().ToList();
            library.History ??= [];
            library.History = library.History.Where(record => library.Assets.Any(item => item.Id == record.WallpaperId))
                .OrderByDescending(record => record.UsedAt).DistinctBy(record => record.WallpaperId).Take(20).ToList();
            library.IntervalSeconds = Math.Clamp(library.IntervalSeconds, 10, 86400);
            return library;
        }
        catch (Exception exception) { logger.Error(exception, "Cannot load wallpaper library; preserving source file"); return new(); }
        finally { _gate.Release(); }
    }
    public async Task<bool> SaveAsync(WallpaperLibrary library)
    {
        // Snapshot mutable UI-owned models before yielding.
        var json = JsonSerializer.Serialize(library, JsonOptions);
        await _gate.WaitAsync();
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "library.json");
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, json);
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception exception) { logger.Error(exception, "Cannot save wallpaper library"); return false; }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); } catch (Exception exception) { logger.Warning(exception, "Cannot remove temporary catalog"); }
            _gate.Release();
        }
    }
    public async Task<WallpaperAsset> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".mp4")) throw new InvalidDataException("Only JPG/PNG/MP4 are supported");
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
        var item = new WallpaperAsset { Path = path, Name = Path.GetFileNameWithoutExtension(path), IsVideo = extension == ".mp4", AddedAt = DateTimeOffset.UtcNow };
        if (item.IsVideo)
        {
            var properties = await file.Properties.GetVideoPropertiesAsync().AsTask(cancellationToken);
            item.Width = (int)properties.Width; item.Height = (int)properties.Height; item.DurationSeconds = properties.Duration.TotalSeconds;
        }
        else
        {
            var properties = await file.Properties.GetImagePropertiesAsync().AsTask(cancellationToken);
            item.Width = (int)properties.Width; item.Height = (int)properties.Height;
        }
        if (item.Width <= 0 || item.Height <= 0) throw new InvalidDataException("Media dimensions are unavailable");
        try
        {
            using var thumbnail = await file.GetThumbnailAsync(item.IsVideo ? ThumbnailMode.VideosView : ThumbnailMode.PicturesView, 640, ThumbnailOptions.ResizeThumbnail).AsTask(cancellationToken);
            if (thumbnail is not null && thumbnail.Size > 0)
            {
                var destination = ThumbnailPath(item.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var source = thumbnail.AsStreamForRead();
                await using var output = File.Create(destination);
                await source.CopyToAsync(output, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { RemoveThumbnail(item.Id); throw; }
        catch (Exception exception) { logger.Warning(exception, "Wallpaper thumbnail unavailable for {Path}", path); }
        logger.Information("Imported wallpaper {Path}, {Width}x{Height}, video={IsVideo}", path, item.Width, item.Height, item.IsVideo);
        return item;
    }
}
