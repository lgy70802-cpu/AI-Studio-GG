using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiraBox.App;

internal sealed class StudioUpdateService(HttpClient client, string cache, bool allowLoopback = false)
{
    internal sealed record Manifest(string Version, string Notes, string Url, string Sha256, long Bytes);
    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
    Manifest? available;

    internal static Uri ValidateUrl(string value, bool allowLoopback)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.Scheme != "https" && !(allowLoopback && uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidDataException("更新地址需要使用 HTTPS。");
        return uri;
    }

    internal async Task<HttpResponseMessage> Open(Uri uri, CancellationToken token)
    {
        var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        try
        {
            response.EnsureSuccessStatusCode();
            ValidateUrl(response.RequestMessage!.RequestUri!.AbsoluteUri, allowLoopback);
            return response;
        }
        catch { response.Dispose(); throw; }
    }

    internal async Task<Manifest?> Check(string source, CancellationToken token)
    {
        available = null;
        using var response = await Open(ValidateUrl(source, allowLoopback), token);
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + count > 256 * 1024) throw new InvalidDataException("更新清单过大。");
            memory.Write(buffer, 0, count);
        }
        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(memory.ToArray(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("更新清单为空。");
        }
        catch (JsonException ex) { throw new InvalidDataException("更新清单格式无效。", ex); }
        if (!Version.TryParse(manifest.Version, out var version) || version.Revision > 0 ||
            string.IsNullOrEmpty(manifest.Sha256) || !Regex.IsMatch(manifest.Sha256, "^[a-fA-F0-9]{64}$") ||
            manifest.Bytes is < 2 or > 2L * 1024 * 1024 * 1024 || manifest.Notes?.Length > 32000)
            throw new InvalidDataException("更新清单中的版本、长度或校验值无效。");
        ValidateUrl(manifest.Url, allowLoopback);
        if (version <= CurrentVersion) return null;
        available = manifest;
        return manifest;
    }

    internal async Task<string> Download(Action<long, long> progress, CancellationToken token)
    {
        var manifest = available ?? throw new InvalidOperationException("请先检查更新。");
        Directory.CreateDirectory(cache);
        var target = Path.Combine(cache, "MiraBoxStudio-" + Version.Parse(manifest.Version) + "-Setup.exe");
        var temporary = target + ".part";
        try
        {
            using var response = await Open(ValidateUrl(manifest.Url, allowLoopback), token);
            using var source = await response.Content.ReadAsStreamAsync(token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            var headerBytes = 0;
            var buffer = new byte[128 * 1024];
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                int count;
                while ((count = await source.ReadAsync(buffer, token)) > 0)
                {
                    for (var index = 0; index < count && headerBytes < 2; index++, headerBytes++)
                        if (buffer[index] != (headerBytes == 0 ? 'M' : 'Z'))
                            throw new InvalidDataException("下载文件不是 Windows 安装程序。");
                    total += count;
                    if (total > manifest.Bytes) throw new InvalidDataException("安装包大小超过更新清单声明。");
                    await file.WriteAsync(buffer.AsMemory(0, count), token);
                    hash.AppendData(buffer, 0, count);
                    progress(total, manifest.Bytes);
                }
                await file.FlushAsync(token);
                file.Flush(true);
            }
            if (total != manifest.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包校验失败，请重新检查更新后下载。");
            File.Move(temporary, target, true);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

}
