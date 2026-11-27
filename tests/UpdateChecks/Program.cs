using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiraBox.App;

internal static class Program
{
    static int count;
    static void Check(bool valid, string name) { if (!valid) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
    static async Task Reject(Func<Task> action, string name)
    {
        try { await action(); } catch (Exception e) when (e is InvalidDataException or InvalidOperationException or OperationCanceledException or HttpRequestException) { Check(true, name); return; }
        throw new Exception("Unexpected acceptance: " + name);
    }
    static async Task Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MiraBoxUpdateChecks-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("MZ fixture installer content");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var version = (StudioUpdateService.CurrentVersion.Major + 1) + ".0.0";
        var manifest = new StudioUpdateService.Manifest(version, "release notes", "https://updates.invalid/setup.exe", hash, bytes.Length);
        var payload = bytes;
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var status = HttpStatusCode.OK;
        using var client = new HttpClient(new Handler(request => new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = request.RequestUri!.AbsolutePath == "/update.json" ? new ByteArrayContent(json) : new StreamContent(new BytewiseStream(payload))
        }));
        var updates = new StudioUpdateService(client, directory);
        await updates.Check("https://updates.invalid/update.json", CancellationToken.None);
        long progress = 0;
        var file = await updates.Download((done, _) => progress = done, CancellationToken.None);
        Check(File.ReadAllBytes(file).SequenceEqual(bytes) && progress == bytes.Length, "fragmented download verifies hash, size and progress");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Version = StudioUpdateService.CurrentVersion.ToString(3) });
        Check(await updates.Check("https://updates.invalid/update.json", CancellationToken.None) == null, "equal version has no update");
        await Reject(async () => { await updates.Download((_, _) => { }, CancellationToken.None); }, "checking latest clears previous update offer");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Version = "0.0.0" });
        Check(await updates.Check("https://updates.invalid/update.json", CancellationToken.None) == null, "older version is not offered");
        await Reject(async () => { await updates.Check("http://updates.invalid/update.json", CancellationToken.None); }, "insecure production manifest rejected");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Url = "http://updates.invalid/setup.exe" });
        await Reject(async () => { await updates.Check("https://updates.invalid/update.json", CancellationToken.None); }, "insecure installer URL rejected");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Sha256 = "invalid" });
        await Reject(async () => { await updates.Check("https://updates.invalid/update.json", CancellationToken.None); }, "malformed digest rejected");
        json = new byte[270 * 1024];
        await Reject(async () => { await updates.Check("https://updates.invalid/update.json", CancellationToken.None); }, "oversized manifest rejected");
        json = Encoding.UTF8.GetBytes("not-json");
        await Reject(async () => { await updates.Check("https://updates.invalid/update.json", CancellationToken.None); }, "invalid manifest has a readable failure");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Sha256 = new string('0', 64) });
        await updates.Check("https://updates.invalid/update.json", CancellationToken.None);
        await Reject(async () => { await updates.Download((_, _) => { }, CancellationToken.None); }, "checksum mismatch never replaces cached installer");
        Check(File.ReadAllBytes(file).SequenceEqual(bytes) && !Directory.GetFiles(directory, "*.part").Any(), "failed download preserves verified cache and cleans partial file");
        json = JsonSerializer.SerializeToUtf8Bytes(manifest);
        await updates.Check("https://updates.invalid/update.json", CancellationToken.None);
        payload = bytes[..^1];
        await Reject(async () => { await updates.Download((_, _) => { }, CancellationToken.None); }, "truncated download rejected");
        payload = bytes.Concat(new byte[] { 1 }).ToArray();
        await Reject(async () => { await updates.Download((_, _) => { }, CancellationToken.None); }, "oversized installer rejected");
        payload = Encoding.UTF8.GetBytes("not a Windows installer");
        await Reject(async () => { await updates.Download((_, _) => { }, CancellationToken.None); }, "non executable payload rejected");
        payload = bytes;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Reject(async () => { await updates.Download((_, _) => { }, cancelled.Token); }, "cancelled download stops without an installer launch");
        using var midDownload = new CancellationTokenSource();
        await Reject(async () => { await updates.Download((done, _) => { if (done >= 4) midDownload.Cancel(); }, midDownload.Token); }, "cancel during streaming removes the partial download");
        Check(!Directory.GetFiles(directory, "*.part").Any(), "cancel leaves no partial installer");
        status = HttpStatusCode.NotFound;
        await Reject(async () => { await updates.Check("https://updates.invalid/update.json", CancellationToken.None); }, "server errors do not report latest version");
        Console.WriteLine(count + " update checks passed");
    }
    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(reply(request)); }
    }
    sealed class BytewiseStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], token);
    }
}
