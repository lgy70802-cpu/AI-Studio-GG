using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using MiraBox.Core;

namespace MiraBox.App;

internal sealed class OnlineResourceSource(HttpClient client)
{
    public const string ConfigurationKey = "MIRABOX_RESOURCE_CATALOG_URL";
    const int CatalogBytes = 2 * 1024 * 1024;
    const long ImageBytes = 50L * 1024 * 1024;
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
    static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mov", ".webm", ".mkv" };

    sealed record Catalog(int Version, CatalogItem[] Items);
    sealed record CatalogItem(string Id, string Name, string Type, string? Category, string? Subcategory,
        int Width, int Height, string Url, int Hot, DateTimeOffset Added);

    public static Uri? ConfiguredCatalog()
    {
        var value = Environment.GetEnvironmentVariable(ConfigurationKey);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !Allowed(uri))
            throw new InvalidDataException("在线资源目录需要 HTTPS 地址。");
        return uri;
    }

    static bool Allowed(Uri uri) => uri.IsAbsoluteUri && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

    public async Task<LibraryEntry[]> ReadCatalog(Uri address, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var data = new MemoryStream();
        await Transfer(address, data, CatalogBytes, null, timeout.Token).ConfigureAwait(false);
        data.Position = 0;
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(data, Json, timeout.Token).ConfigureAwait(false);
        if (catalog is not { Version: 1, Items.Length: <= 3000 })
            throw new InvalidDataException("在线资源目录版本或结构无效。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<LibraryEntry>();
        foreach (var item in catalog.Items)
        {
            if (item == null || item.Id is not { Length: > 0 and <= 80 } ||
                !item.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') || !ids.Add(item.Id) ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 || item.Width is < 1 or > 16384 ||
                item.Height is < 1 or > 16384 || item.Type is not ("壁纸" or "视频") ||
                string.IsNullOrWhiteSpace(item.Url) || !Uri.TryCreate(address, item.Url, out var media) || !Allowed(media) ||
                !Extensions(item.Type).Contains(Path.GetExtension(media.AbsolutePath)))
                throw new InvalidDataException("在线资源目录包含无效或重复的资源。");
            entries.Add(new(new("online:" + item.Id, item.Name.Trim(), [new(item.Width, item.Height)], Local: false),
                item.Type, item.Category ?? "", item.Subcategory ?? "", Added: item.Added,
                Online: true, Hot: item.Hot, MediaUrl: media.AbsoluteUri));
        }
        return entries.ToArray();
    }

    public static string Extension(LibraryEntry entry) => Path.GetExtension(new Uri(entry.MediaUrl!).AbsolutePath);

    static HashSet<string> Extensions(string type) => type == "视频" ? VideoExtensions : ImageExtensions;

    public async Task Download(LibraryEntry entry, string path, IProgress<(long Bytes, long? Total)>? progress, CancellationToken token)
    {
        var address = new Uri(entry.MediaUrl!);
        if (!Allowed(address) || !Extensions(entry.Type).Contains(Extension(entry)))
            throw new InvalidDataException("资源下载地址或格式无效。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await Transfer(address, file, entry.Type == "视频" ? VideoImportLimits.MaximumBytes : ImageBytes,
            progress, timeout.Token).ConfigureAwait(false);
    }

    async Task Transfer(Uri address, Stream destination, long limit, IProgress<(long Bytes, long? Total)>? progress,
        CancellationToken token)
    {
        using var response = await Send(address, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri is not { } final || !Allowed(final))
            throw new InvalidDataException("资源重定向地址无效。");
        var total = response.Content.Headers.ContentLength;
        if (total > limit) throw new InvalidDataException("文件超出大小限制。");
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var buffer = new byte[65536];
        var update = Stopwatch.StartNew();
        long bytes = 0;
        int size;
        while ((size = await source.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            bytes += size;
            if (bytes > limit) throw new InvalidDataException("文件超出大小限制。");
            await destination.WriteAsync(buffer.AsMemory(0, size), token).ConfigureAwait(false);
            if (update.ElapsedMilliseconds >= 100) { progress?.Report((bytes, total)); update.Restart(); }
        }
        if (bytes == 0 || total.HasValue && total != bytes)
            throw new InvalidDataException("服务器返回的文件不完整。");
        progress?.Report((bytes, total));
    }

    async Task<HttpResponseMessage> Send(Uri address, CancellationToken token)
    {
        if (!Allowed(address)) throw new InvalidDataException("资源地址无效。");
        for (var redirects = 0; redirects < 6; redirects++)
        {
            var response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)) return response;
            using (response)
            {
                if (response.Headers.Location is not { } location || !Uri.TryCreate(address, location, out var next) ||
                    !Allowed(next) || address.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidDataException("资源重定向地址无效。");
                address = next;
            }
        }
        throw new InvalidDataException("资源重定向次数过多。");
    }
}
