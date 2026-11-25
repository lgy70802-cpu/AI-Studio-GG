using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Serilog;

namespace DeskDisplay.Library;

public sealed record StudioCategory(string Id, string Name, string? ParentId);
public sealed record StudioTag(string Id, string Name);
public sealed record StudioResource(string Id, string Name, string Description, string Type, string Version,
    string Sha256, long ByteSize, string MimeType, string DownloadUrl, string PreviewUrl, string ThumbnailUrl,
    IReadOnlyList<StudioCategory> Categories, IReadOnlyList<StudioTag> Tags, DateTimeOffset UpdatedAt)
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string AccessPolicy { get; init; } = "FREE";
    public int? PriceMinor { get; init; }
    public string Currency { get; init; } = "CNY";
    public string Changelog { get; init; } = "";
    public string CategoryLabel => Categories.Count == 0 ? "未分类" : string.Join(" / ", Categories.Select(item => item.Name));
    public string TagLabel => Tags.Count == 0 ? "无标签" : string.Join(" · ", Tags.Select(item => item.Name));
    public string VersionLabel => $"版本 {Version} · {DownloadState}";
    public bool IsPortrait => Height > Width;
    public double CardAspectRatio => IsPortrait ? 0.25 : 4d;
    public string ResolutionLabel => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "尺寸未知";
    public bool HasDownloadEntitlement => string.Equals(AccessPolicy, "FREE", StringComparison.OrdinalIgnoreCase) || IsEntitled;
    public string AccessLabel => AccessPolicy switch
    {
        "VIP" => "VIP",
        "PURCHASE" when PriceMinor is int price => $"¥{price / 100d:0.##}",
        "PURCHASE" => "付费",
        "PURCHASE_OR_VIP" => "VIP / 付费",
        _ => "免费"
    };
    public string AccessDescription => HasDownloadEntitlement ? (AccessPolicy == "FREE" ? "免费资源，可直接下载" : "已获得使用权益，可下载并离线使用") : "当前账号尚未取得此资源权益";
    public string? MinimumClientVersion { get; init; }
    public IReadOnlyList<string> SupportedDevices { get; init; } = [];
    public Uri? PreviewUri { get; internal set; }
    public Uri? ThumbnailUri { get; internal set; }
    public bool HasUpdate { get; set; }
    public bool IsDownloaded { get; set; }
    public string DownloadState => HasUpdate ? "有更新" : IsDownloaded ? "已下载" : "";
    public bool IsEntitled { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsCompatible { get; internal set; } = true;
    public string CompatibilityMessage { get; internal set; } = "适用于 D92";
}
public sealed record StudioDownloadProgress(long BytesReceived, long TotalBytes)
{
    public int Percentage => TotalBytes <= 0 ? 0 : (int)Math.Clamp(BytesReceived * 100 / TotalBytes, 0, 100);
}
public sealed record StudioResourcePage(IReadOnlyList<StudioResource> Items, int Total, int Page, int PageSize);
public sealed record StudioCatalogOptions(IReadOnlyList<StudioCategory> Categories, IReadOnlyList<StudioTag> Tags);
public sealed record StudioUserProfile(string Id, string Email, bool IsVip, DateTimeOffset? VipExpiresAt);
public sealed record StudioAuthSession(string Token, DateTimeOffset ExpiresAt, StudioUserProfile Profile);
public sealed record StudioUserLibrary(IReadOnlyList<string> ResourceIds, bool HasVip, IReadOnlyList<string> FavoriteIds);

public sealed class StudioApiClient : IDisposable
{
    private const long MaximumBytes = 25 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    private readonly StudioSessionStore _sessionStore;
    private readonly SemaphoreSlim _thumbnailGate = new(4, 4);
    public StudioApiClient(ILogger logger, HttpClient? client = null, string? cacheDirectory = null, StudioSessionStore? sessionStore = null)
    {
        _logger = logger; _client = client ?? new HttpClient(); _ownsClient = client is null;
        _client.Timeout = TimeSpan.FromSeconds(30);
        _cacheDirectory = cacheDirectory ?? Path.Combine(DeskDisplay.Core.Storage.StudioDataPaths.Root, "CloudCache");
        _sessionStore = sessionStore ?? new StudioSessionStore(cacheDirectory is null ? null : Path.Combine(cacheDirectory, "studio.session"));
    }
    public bool HasSession(string serverUrl) => _sessionStore.Read(serverUrl) is not null;
    public async Task<StudioAuthSession> LoginAsync(string serverUrl, string email, string password, bool register, CancellationToken cancellationToken = default)
    {
        var server = ValidateServer(serverUrl);
        using var response = await _client.PostAsJsonAsync(new Uri(server, register ? "/api/v1/client/auth/register" : "/api/v1/client/auth/login"), new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<StudioAuthSession>(JsonOptions, cancellationToken) ?? throw new InvalidDataException("登录响应为空");
        _sessionStore.Save(serverUrl, session.Token);
        return session;
    }
    public async Task<StudioUserProfile?> GetProfileAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (_sessionStore.Read(serverUrl) is not { } token) return null;
        using var request = Authorized(HttpMethod.Get, new Uri(ValidateServer(serverUrl), "/api/v1/client/auth/me"), token);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) { _sessionStore.Clear(serverUrl); return null; }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioUserProfile>(JsonOptions, cancellationToken);
    }
    public async Task<StudioUserLibrary?> GetUserLibraryAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (_sessionStore.Read(serverUrl) is not { } token) return null;
        using var request = Authorized(HttpMethod.Get, new Uri(ValidateServer(serverUrl), "/api/v1/client/me/library"), token);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) { _sessionStore.Clear(serverUrl); return null; }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioUserLibrary>(JsonOptions, cancellationToken);
    }
    public async Task SetFavoriteAsync(string serverUrl, string resourceId, bool favorite, CancellationToken cancellationToken = default)
    {
        var token = _sessionStore.Read(serverUrl) ?? throw new InvalidOperationException("请先登录");
        using var request = Authorized(favorite ? HttpMethod.Post : HttpMethod.Delete, new Uri(ValidateServer(serverUrl), $"/api/v1/client/me/favorites/{resourceId}"), token);
        using var response = await _client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
    }
    public async Task LogoutAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var token = _sessionStore.Read(serverUrl);
        try
        {
            if (token is not null)
            {
                using var request = Authorized(HttpMethod.Post, new Uri(ValidateServer(serverUrl), "/api/v1/client/auth/logout"), token);
                using var response = await _client.SendAsync(request, cancellationToken);
            }
        }
        finally { _sessionStore.Clear(serverUrl); }
    }
    private static HttpRequestMessage Authorized(HttpMethod method, Uri uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new("Bearer", token);
        return request;
    }
    public async Task<StudioResourcePage> GetResourcesAsync(string serverUrl, string search = "", string? categoryId = null, string? tagId = null, int pageNumber = 1, CancellationToken cancellationToken = default, string orientation = "landscape", string sort = "recommended", int pageSize = 24, string scope = "all")
    {
        var server = ValidateServer(serverUrl);
        var query = $"/api/v1/client/resources?page={pageNumber}&pageSize={pageSize}&scope={Uri.EscapeDataString(scope)}&search={Uri.EscapeDataString(search)}&orientation={Uri.EscapeDataString(orientation)}&sort={Uri.EscapeDataString(sort)}" + (categoryId is null ? "" : $"&categoryId={Uri.EscapeDataString(categoryId)}") + (tagId is null ? "" : $"&tagId={Uri.EscapeDataString(tagId)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, query));
        if (_sessionStore.Read(serverUrl) is { } token) request.Headers.Authorization = new("Bearer", token);
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<StudioResourcePage>(JsonOptions, cancellationToken) ?? throw new InvalidDataException("云端资源响应为空");
        foreach (var item in page.Items)
        {
            ValidateItem(item);
            SetCompatibility(item);
            item.PreviewUri = new Uri(server, item.PreviewUrl);
            item.ThumbnailUri = new Uri(server, item.ThumbnailUrl);
        }
        await PrepareThumbnailsAsync(server, page.Items, cancellationToken);
        return page;
    }
    private async Task PrepareThumbnailsAsync(Uri server, IReadOnlyList<StudioResource> items, CancellationToken cancellationToken)
    {
        var serverKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server.AbsoluteUri)));
        var directory = Path.Combine(_cacheDirectory, "Thumbnails", serverKey);
        Directory.CreateDirectory(directory);
        await Task.WhenAll(items.Select(item => CacheThumbnailAsync(server, item, directory, cancellationToken)));
    }
    private async Task CacheThumbnailAsync(Uri server, StudioResource item, string directory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, $"{item.Id}-{SanitizeVersion(item.Version)}-{item.UpdatedAt.ToUnixTimeMilliseconds()}.jpg");
        if (File.Exists(path)) { item.ThumbnailUri = new Uri(path); return; }
        await _thumbnailGate.WaitAsync(cancellationToken);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var response = await _client.GetAsync(new Uri(server, item.ThumbnailUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 5 * 1024 * 1024) throw new InvalidDataException("缩略图文件过大");
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024) throw new InvalidDataException("缩略图文件无效");
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, true);
            item.ThumbnailUri = new Uri(path);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            _logger.Debug(exception, "Cannot cache Studio thumbnail {ResourceId}", item.Id);
        }
        finally
        {
            try { File.Delete(temporary); } catch { }
            _thumbnailGate.Release();
        }
    }
    public async Task<StudioCatalogOptions> GetCatalogOptionsAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var server = ValidateServer(serverUrl);
        using var response = await _client.GetAsync(new Uri(server, "/api/v1/client/resources/catalog/options"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioCatalogOptions>(JsonOptions, cancellationToken) ?? new([], []);
    }
    public async Task<string> DownloadAsync(string serverUrl, StudioResource item, CancellationToken cancellationToken = default)
        => await DownloadAsync(serverUrl, item, null, cancellationToken);
    public async Task<string> DownloadAsync(string serverUrl, StudioResource item, IProgress<StudioDownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        ValidateItem(item); SetCompatibility(item); if (!item.IsCompatible) throw new InvalidDataException(item.CompatibilityMessage); var server = ValidateServer(serverUrl);
        Directory.CreateDirectory(_cacheDirectory);
        var extension = item.MimeType == "image/png" ? ".png" : item.MimeType == "image/jpeg" ? ".jpg" : throw new InvalidDataException("云端文件类型不受支持");
        var destination = Path.Combine(_cacheDirectory, $"{item.Id}-{SanitizeVersion(item.Version)}{extension}");
        if (File.Exists(destination) && await HashMatchesAsync(destination, item.Sha256, cancellationToken)) { progress?.Report(new(item.ByteSize, item.ByteSize)); return destination; }
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, item.DownloadUrl));
            if (_sessionStore.Read(serverUrl) is { } token) request.Headers.Authorization = new("Bearer", token);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaximumBytes || response.Content.Headers.ContentLength is long length && length != item.ByteSize)
                throw new InvalidDataException("云端文件大小与清单不一致");
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920]; long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken); if (read == 0) break;
                total += read; if (total > MaximumBytes || total > item.ByteSize) throw new InvalidDataException("云端文件超过清单大小");
                hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken); progress?.Report(new(total, item.ByteSize));
            }
            await output.FlushAsync(cancellationToken);
            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (total != item.ByteSize || !string.Equals(actual, item.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("云端文件校验失败");
            await output.DisposeAsync();
            File.Move(temporary, destination, true); return destination;
        }
        catch { try { File.Delete(temporary); } catch (Exception exception) { _logger.Warning(exception, "Cannot delete incomplete Studio download {Path}", temporary); } throw; }
    }
    private static void SetCompatibility(StudioResource item)
    {
        if (item.SupportedDevices.Count > 0 && !item.SupportedDevices.Any(device => string.Equals(device, "D92", StringComparison.OrdinalIgnoreCase)))
        { item.IsCompatible = false; item.CompatibilityMessage = "此资源未标记支持 D92"; return; }
        if (!string.IsNullOrWhiteSpace(item.MinimumClientVersion) && Version.TryParse(item.MinimumClientVersion, out var minimum)
            && Version.TryParse(typeof(StudioApiClient).Assembly.GetName().Version?.ToString(), out var current) && current < minimum)
        { item.IsCompatible = false; item.CompatibilityMessage = $"需要客户端 {minimum} 或更高版本"; return; }
        item.IsCompatible = true; item.CompatibilityMessage = "适用于 D92";
    }
    private static Uri ValidateServer(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/'), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new InvalidDataException("服务器地址必须使用 HTTPS；本机开发可使用 HTTP localhost");
        return uri;
    }
    private static void ValidateItem(StudioResource item) => StudioResourceContract.Validate(item);
    private static string SanitizeVersion(string version) => string.Concat(version.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')) is { Length: > 0 } safe ? safe : "unknown";
    private static async Task<bool> HashMatchesAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path); var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() { _thumbnailGate.Dispose(); if (_ownsClient) _client.Dispose(); }
}
