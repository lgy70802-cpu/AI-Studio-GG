using System.Text.Json;
namespace MiraBox.Core;

public sealed record LibraryEntry(Resource Resource, string Type = "壁纸", string Category = "", string Subcategory = "",
    bool Favorite = false, bool Downloaded = false, bool Created = false, string? MediaFile = null,
    long Bytes = 0, DateTimeOffset Added = default, int Uses = 0, bool Online=false, int Hot=0,
    TimeSpan? Duration = null, string? ThumbnailFile = null, string? MediaUrl = null, string? CaptureKind = null);
public sealed record VideoMetadata(Size Dimensions, TimeSpan Duration, byte[] Thumbnail);
public static class VideoImportLimits
{
    public const long MaximumBytes = 500L * 1024 * 1024;
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(10);
    public static bool Exceeded(long bytes, TimeSpan? duration = null) =>
        bytes > MaximumBytes || duration > MaximumDuration;
}
public sealed record LibraryFilter(string Query = "", string Collection = "全部资源", string Type = "全部",
    string Category = "全部", string Subcategory = "全部", string Sort = "最近添加");
public sealed class LibraryStore
{
    readonly string root;
    readonly string index;
    public List<LibraryEntry> Entries { get; private set; } = [];
    public void Replace(IEnumerable<LibraryEntry> entries) => Entries=entries.ToList();
    public LibraryStore(string directory,bool loadExisting=true)
    {
        root = Path.GetFullPath(directory); Directory.CreateDirectory(root);
        index = Path.Combine(root, "library.json");
        if (loadExisting && File.Exists(index)) Entries = JsonSerializer.Deserialize<List<LibraryEntry>>(File.ReadAllText(index))
            ?? throw new InvalidDataException("资源索引无效；未覆盖原文件。");
    }
    public void Save()
    {
        var temporary = index + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, Entries); stream.Flush(true); }
        File.Move(temporary, index, true);
    }
    public void Seed(IEnumerable<LibraryEntry> entries)
    { foreach (var entry in entries) if (!Entries.Any(x => x.Resource.Id == entry.Resource.Id)) Entries.Add(entry); Save(); }
    public string? MediaPath(LibraryEntry entry) => ManagedPath(entry.MediaFile);
    public string? ThumbnailPath(LibraryEntry entry) => ManagedPath(entry.ThumbnailFile);

    string? ManagedPath(string? file)
    {
        if (file is null) return null;
        var full = Path.GetFullPath(Path.Combine(root, file));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("媒体路径超出资源目录。");
        return full;
    }
    public LibraryEntry? Import(string source, Size dimensions)
        => ImportMedia(source, dimensions, null);

    public bool IsDuplicate(FileInfo info) => Entries.Any(entry => entry.MediaFile != null &&
        string.Equals(entry.Resource.Name, info.Name, StringComparison.OrdinalIgnoreCase) && entry.Bytes == info.Length);

    public LibraryEntry? ImportVideo(string source, VideoMetadata video)
    {
        if (video.Duration <= TimeSpan.Zero || video.Thumbnail.Length == 0)
            throw new InvalidDataException("视频时长或缩略图无效。");
        return ImportMedia(source, video.Dimensions, video);
    }

    LibraryEntry? ImportMedia(string source, Size dimensions, VideoMetadata? video)
    {
        if (dimensions.Width <= 0 || dimensions.Height <= 0) throw new InvalidDataException("素材尺寸无效。");
        var info = new FileInfo(source);
        if (IsDuplicate(info)) return null;
        if (video != null && VideoImportLimits.Exceeded(info.Length, video.Duration))
            throw new InvalidDataException("视频超出文件大小或时长限制。");
        var id = Guid.NewGuid().ToString("N"); var file = id + info.Extension.ToLowerInvariant();
        var destination = Path.Combine(root, file);
        var thumbnail = video == null ? null : id + ".preview.jpg";
        var entry = new LibraryEntry(new(id, info.Name, [dimensions]), Type: video == null ? "壁纸" : "视频",
            MediaFile: file, Bytes: info.Length, Added: DateTimeOffset.UtcNow, Duration: video?.Duration, ThumbnailFile: thumbnail);
        CommitMedia(source, entry, video);
        return entry;
    }

    public void RefreshOnlineEntries(IEnumerable<LibraryEntry> catalog)
    {
        var previous = Entries;
        var available = catalog.ToDictionary(entry => entry.Resource.Id);
        var next = new List<LibraryEntry>();
        foreach (var entry in previous)
        {
            if (available.Remove(entry.Resource.Id, out var remote))
                next.Add(entry.Resource.Local ? entry with { MediaUrl = remote.MediaUrl, Online = true, Hot = remote.Hot } :
                    remote with { Favorite = entry.Favorite });
            else if (entry.MediaUrl == null || entry.Resource.Local)
                next.Add(entry with { Online = entry.MediaUrl == null && entry.Online });
        }
        next.AddRange(available.Values);
        Entries = next;
        try { Save(); }
        catch { Entries = previous; throw; }
    }

    public LibraryEntry CompleteOnlineDownload(string id, string source, Size dimensions, VideoMetadata? video = null)
    {
        var index = Entries.FindIndex(entry => entry.Resource.Id == id);
        if (index < 0) throw new KeyNotFoundException(id);
        var previous = Entries[index];
        if (previous.MediaUrl == null || previous.Resource.Local)
            throw new InvalidOperationException("该资源不需要下载。");
        var info = new FileInfo(source);
        if (dimensions.Width <= 0 || dimensions.Height <= 0 || info.Length == 0 ||
            (previous.Type == "视频") != (video != null) ||
            video != null && (video.Dimensions != dimensions || video.Duration <= TimeSpan.Zero ||
                video.Thumbnail.Length == 0 || VideoImportLimits.Exceeded(info.Length, video.Duration)))
            throw new InvalidDataException("下载文件的格式、尺寸或时长无效。");
        var fileId = Guid.NewGuid().ToString("N");
        var entry = previous with
        {
            Resource = previous.Resource with { Local = true, Layouts = [dimensions] },
            Downloaded = true,
            Online = true,
            MediaFile = fileId + info.Extension.ToLowerInvariant(),
            Bytes = info.Length,
            Added = DateTimeOffset.UtcNow,
            Duration = video?.Duration,
            ThumbnailFile = video == null ? null : fileId + ".preview.jpg"
        };
        CommitMedia(source, entry, video, index);
        return entry;
    }

    void CommitMedia(string source, LibraryEntry entry, VideoMetadata? video, int? existingIndex = null)
    {
        var previous = existingIndex is { } index ? Entries[index] : null;
        var destination = MediaPath(entry)!;
        var thumbnail = ThumbnailPath(entry);
        try
        {
            File.Copy(source, destination, false);
            if (video != null) File.WriteAllBytes(thumbnail!, video.Thumbnail);
            if (existingIndex is { } target) Entries[target] = entry;
            else Entries.Add(entry);
            Save();
        }
        catch
        {
            if (existingIndex is { } target) Entries[target] = previous!;
            else Entries.Remove(entry);
            File.Delete(destination);
            if (thumbnail != null) File.Delete(thumbnail);
            throw;
        }
    }
    public void Update(string id, Func<LibraryEntry, LibraryEntry> change)
    {
        var i = Entries.FindIndex(x => x.Resource.Id == id); if (i < 0) throw new KeyNotFoundException(id);
        var previous = Entries[i]; Entries[i] = change(previous);
        try { Save(); } catch { Entries[i] = previous; throw; }
    }

    internal void RecordUse(string id)
    {
        var index = Entries.FindIndex(entry => entry.Resource.Id == id);
        if (index < 0) return;
        var entry = Entries[index];
        if (entry.Uses < int.MaxValue) Entries[index] = entry with { Uses = entry.Uses + 1 };
    }
    public IEnumerable<LibraryEntry> Query(LibraryFilter f, bool online = false)
    {
        var rows = Entries.Where(x => (online ? x.Online || !x.Resource.Local : x.Resource.Local)
            && x.Resource.Name.Contains(f.Query, StringComparison.OrdinalIgnoreCase)
            && (f.Type == "全部" || x.Type == f.Type)
            && (f.Category == "全部" || x.Category == f.Category)
            && (f.Subcategory == "全部" || x.Subcategory == f.Subcategory)
            && (online || f.Collection switch { "收藏" => x.Favorite, "已下载" => x.Downloaded, "我的创作" => x.Created, _ => true }));
        return f.Sort switch { "热门" => rows.OrderByDescending(x=>x.Hot), "名称" => rows.OrderBy(x => x.Resource.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), false)),
            "最常用" => rows.OrderByDescending(x => x.Uses).ThenByDescending(x => x.Added), _ => rows.OrderByDescending(x => x.Added) };
    }
}
