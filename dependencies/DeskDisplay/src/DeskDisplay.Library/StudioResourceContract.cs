using System.Text.RegularExpressions;

namespace DeskDisplay.Library;

/// <summary>Version 1 of the actually deployed static-wallpaper API, not a plugin installer.</summary>
public static class StudioResourceContract
{
    public const int Version = 1;
    public const long MaximumBytes = 25L * 1024 * 1024;
    public static void Validate(StudioResource item)
    {
        if (!Guid.TryParse(item.Id, out var id) || id == Guid.Empty || item.Type != "STATIC_WALLPAPER" || item.ByteSize is <= 0 or > MaximumBytes || !Regex.IsMatch(item.Sha256 ?? "", "^[0-9a-fA-F]{64}$") || item.MimeType is not ("image/png" or "image/jpeg") || string.IsNullOrWhiteSpace(item.Version) || item.Categories is null || item.Tags is null || item.SupportedDevices is null || item.AccessPolicy is not ("FREE" or "VIP" or "PURCHASE" or "PURCHASE_OR_VIP") || item.PriceMinor is < 0)
            throw new InvalidDataException("云端资源清单不符合静态壁纸契约 V1");
        foreach (var (path, name) in new[] { (item.DownloadUrl, "download"), (item.PreviewUrl, "preview"), (item.ThumbnailUrl, "thumbnail") })
            if (path != $"/api/v1/client/resources/{item.Id}/{name}") throw new InvalidDataException("云端资源路径必须是同源、同资源的已约定接口");
        if (!string.IsNullOrWhiteSpace(item.MinimumClientVersion) && !System.Version.TryParse(item.MinimumClientVersion, out _)) throw new InvalidDataException("最低客户端版本格式无效");
    }
}
