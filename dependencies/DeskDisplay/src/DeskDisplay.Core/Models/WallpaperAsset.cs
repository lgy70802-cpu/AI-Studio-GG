namespace DeskDisplay.Core.Models;

public sealed class WallpaperAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsVideo { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double DurationSeconds { get; set; }
    public string Category { get; set; } = "未分类";
    public string Subcategory { get; set; } = "未分类";
    public string Tags { get; set; } = "";
    public bool Favorite { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public string CloudResourceId { get; set; } = "";
    public string CloudVersion { get; set; } = "";
}

public sealed class WallpaperLibrary
{
    public List<CarouselPreset> CarouselPresets { get; set; } = [];
    public Guid? ActiveCarouselPresetId { get; set; }
    public int Version { get; set; } = 1;
    public List<WallpaperAsset> Assets { get; set; } = [];
    public List<Guid> Playlist { get; set; } = [];
    public int IntervalSeconds { get; set; } = 180;
    public bool Shuffle { get; set; }
    public bool RotateToUsb { get; set; }
    public Guid? LastPlayedId { get; set; }
    public List<WallpaperUsageRecord> History { get; set; } = [];
}

public sealed class WallpaperUsageRecord
{
    public Guid WallpaperId { get; set; }
    public DateTimeOffset UsedAt { get; set; }
    public string Target { get; set; } = "";
}

public sealed class CarouselPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "默认方案";
    public List<Guid> Playlist { get; set; } = [];
    public int IntervalSeconds { get; set; } = 180;
    public bool Shuffle { get; set; }
    public bool RotateToUsb { get; set; }
}