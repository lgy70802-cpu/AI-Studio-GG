using System.IO;
using DeskDisplay.Library;

namespace DeskDisplay.DisplayTests;

internal static class ContentContractTests
{
    public static void Run()
    {
        var id = Guid.NewGuid().ToString();
        var valid = new StudioResource(id, "contract fixture", "", "STATIC_WALLPAPER", "1.0.0", new string('a', 64), 32, "image/png", $"/api/v1/client/resources/{id}/download", $"/api/v1/client/resources/{id}/preview", $"/api/v1/client/resources/{id}/thumbnail", [], [], DateTimeOffset.UtcNow);
        StudioResourceContract.Validate(valid); Console.WriteLine("PASS: Current server resource contract remains accepted");
        var cases = new[] { valid with { DownloadUrl = "//external.example/file" }, valid with { PreviewUrl = "https://external.example/file" }, valid with { ThumbnailUrl = "/api/v1/../secret" }, valid with { DownloadUrl = $"/api/v1/client/resources/{Guid.NewGuid()}/download" }, valid with { Type = "PLUGIN" }, valid with { MimeType = "application/zip" }, valid with { AccessPolicy = "UNKNOWN" }, valid with { MinimumClientVersion = "invalid" }, valid with { Id = Guid.Empty.ToString() }, valid with { ByteSize = 0 }, valid with { Sha256 = "bad" }, valid with { PriceMinor = -1 } };
        foreach (var item in cases)
        {
            try { StudioResourceContract.Validate(item); throw new InvalidOperationException("Unsupported content accepted"); }
            catch (InvalidDataException) { Console.WriteLine("PASS: Contract rejects unsupported or unsafe manifest variant"); }
        }
    }
}
