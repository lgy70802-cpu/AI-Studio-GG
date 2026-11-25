using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed record ThemeBundleFile(string Path, long Bytes, string Sha256);
public sealed record ThemeBundleManifest(int Version, Guid ProjectId, List<ThemeBundleFile> Files);
public sealed record ThemeExportResult(int Included, int Omitted);
public sealed record ThemeImportResult(ThemeProject Project, IReadOnlyList<string> MediaPaths);

/// <summary>Data-only, bounded transfer. No executable files, arbitrary extraction paths, or overwriting imported projects.</summary>
public sealed class ThemeTransferService(ThemeProjectStore store)
{
    public const long MaximumFileBytes = 128L * 1024 * 1024;
    public const long MaximumTotalBytes = 512L * 1024 * 1024;
    private static readonly Regex AssetName = new("^assets/[a-f0-9]{64}\\.(png|jpg|jpeg|mp4)$", RegexOptions.CultureInvariant);
    private static IEnumerable<ThemeLayer> Layers(ThemeProject project) => project.AllLayouts.SelectMany(l => l.Layers).Where(l => l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper);

    public Task<ThemeExportResult> ExportAsync(ThemeProject source, string destination, bool bundle, IReadOnlyDictionary<Guid, WallpaperAsset> catalog)
    {
        var project = source.Clone(); project.Validate();
        if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(project)) > 2_000_000) throw new InvalidDataException("主题源项目超过2 MB。");
        foreach (var layer in Layers(project))
            if (layer.AssetId is Guid id && catalog.TryGetValue(id, out var asset))
            {
                layer.Path = asset.Path;
                if (!string.IsNullOrWhiteSpace(asset.CloudResourceId)) { layer.AssetOrigin = ThemeAssetOrigin.Cloud; layer.CloudResourceId = asset.CloudResourceId; }
                else if (layer.AssetOrigin != ThemeAssetOrigin.Cloud && string.IsNullOrWhiteSpace(layer.CloudResourceId)) layer.AssetOrigin = ThemeAssetOrigin.Local;
            }
        return Task.Run(async () =>
        {
            var temporary = Path.GetFullPath(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (Layers(project).Any(l => l.Path.Length > 0 && Path.GetFullPath(l.Path).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("导出目标不能覆盖素材原文件。");
                if (!bundle)
                {
                    await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
                    File.Move(temporary, destination, true); return new ThemeExportResult(0, 0);
                }
                var omitted = 0; long total = 0; var files = new Dictionary<string, string>(StringComparer.Ordinal); var manifestFiles = new List<ThemeBundleFile>();
                foreach (var layer in Layers(project))
                {
                    var path = layer.Path; layer.AssetId = null; layer.Path = "";
                    if (layer.AssetOrigin != ThemeAssetOrigin.Local || !string.IsNullOrWhiteSpace(layer.CloudResourceId) || !File.Exists(path)) { omitted++; continue; }
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension is not (".png" or ".jpg" or ".jpeg" or ".mp4")) throw new InvalidDataException("仅允许打包 PNG、JPEG、MP4 素材。");
                    if (Path.GetFullPath(path).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("导出目标不能覆盖素材原文件。");
                    var size = new FileInfo(path).Length; if (size <= 0 || size > MaximumFileBytes) throw new InvalidDataException("单个打包素材必须小于128 MB。");
                    await using var input = File.OpenRead(path); var hash = Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant(); var name = "assets/" + hash + extension;
                    layer.Path = name;
                    if (files.TryAdd(name, path)) { total += size; if (total > MaximumTotalBytes) throw new InvalidDataException("素材总大小超过512 MB。"); manifestFiles.Add(new(name, size, hash)); }
                }
                using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    foreach (var file in files) zip.CreateEntryFromFile(file.Value, file.Key, CompressionLevel.Fastest);
                    await WriteEntry(zip, "project.theme.json", JsonSerializer.Serialize(project));
                    await WriteEntry(zip, "manifest.json", JsonSerializer.Serialize(new ThemeBundleManifest(1, project.Id, manifestFiles)));
                }
                using (var verify = ZipFile.OpenRead(temporary))
                    foreach (var file in manifestFiles)
                    {
                        await using var data = verify.GetEntry(file.Path)!.Open();
                        if (Convert.ToHexString(await SHA256.HashDataAsync(data)).ToLowerInvariant() != file.Sha256) throw new IOException("素材在导出期间发生变化，请重试。");
                    }
                File.Move(temporary, destination, true); return new ThemeExportResult(files.Count, omitted);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        });
    }
    private static async Task WriteEntry(ZipArchive zip, string name, string value) { await using var stream = zip.CreateEntry(name).Open(); await using var writer = new StreamWriter(stream); await writer.WriteAsync(value); }

    public Task<ThemeImportResult> ImportAsync(string path) => Task.Run(async () =>
    {
        if (!path.EndsWith(".d92theme", StringComparison.OrdinalIgnoreCase))
        {
            var project = await store.ReadAsync(path); project.Id = Guid.NewGuid(); project.Name = ImportedName(project.Name);
            foreach (var layer in Layers(project)) layer.AssetId = null;
            return new ThemeImportResult(project, []);
        }
        if (new FileInfo(path).Length > MaximumTotalBytes) throw new InvalidDataException("主题包超过512 MB。");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count is < 2 or > 202) throw new InvalidDataException("主题包文件数量无效。");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var entry in zip.Entries)
        {
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000 || (entry.FullName != "project.theme.json" && entry.FullName != "manifest.json" && !AssetName.IsMatch(entry.FullName)) || !entries.TryAdd(entry.FullName, entry)) throw new InvalidDataException("主题包包含非法路径、链接或重复文件。");
            var limit = AssetName.IsMatch(entry.FullName) ? MaximumFileBytes : 2_000_000;
            total += entry.Length; if (entry.Length < 0 || entry.Length > limit || total > MaximumTotalBytes) throw new InvalidDataException("主题包解压大小超过限制。");
        }
        if (!entries.TryGetValue("project.theme.json", out var source) || !entries.TryGetValue("manifest.json", out var manifestEntry)) throw new InvalidDataException("主题包缺少源项目或清单。");
        var projectSource = JsonSerializer.Deserialize<ThemeProject>(await ReadText(source)) ?? throw new InvalidDataException("项目为空。"); projectSource.Validate();
        var manifest = JsonSerializer.Deserialize<ThemeBundleManifest>(await ReadText(manifestEntry)) ?? throw new InvalidDataException("清单为空。");
        if (manifest.Version != 1 || manifest.ProjectId != projectSource.Id || manifest.Files is null || manifest.Files.Count != entries.Count - 2 || manifest.Files.Any(f => f is null || f.Path is null || !AssetName.IsMatch(f.Path) || !entries.ContainsKey(f.Path) || f.Bytes != entries[f.Path].Length || !string.Equals(Path.GetFileNameWithoutExtension(f.Path), f.Sha256, StringComparison.Ordinal)) || manifest.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count) throw new InvalidDataException("主题包清单不匹配。");
        projectSource.Id = Guid.NewGuid(); projectSource.Name = ImportedName(projectSource.Name);
        var directory = store.ImportedDirectory(projectSource.Id); var created = new List<string>();
        if (Directory.Exists(directory)) throw new IOException("导入目录已存在。");
        Directory.CreateDirectory(directory);
        try
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in manifest.Files)
            {
                var target = Path.Combine(directory, Path.GetFileName(file.Path)); created.Add(target);
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await CopyBounded(entries[file.Path], output, MaximumFileBytes);
                await using var check = File.OpenRead(target); var hash = Convert.ToHexString(await SHA256.HashDataAsync(check)).ToLowerInvariant();
                if (hash != file.Sha256) throw new InvalidDataException("主题包素材哈希校验失败。"); map.Add(file.Path, target);
            }
            foreach (var layer in Layers(projectSource))
            {
                layer.AssetId = null;
                if (map.TryGetValue(layer.Path, out var resolved)) { layer.Path = resolved; layer.AssetOrigin = ThemeAssetOrigin.Local; layer.CloudResourceId = ""; }
                else if (layer.Path.Length != 0) throw new InvalidDataException("项目引用了未列入清单的素材。");
            }
            return new ThemeImportResult(projectSource, map.Values.ToArray());
        }
        catch
        {
            foreach (var file in created) if (File.Exists(file)) File.Delete(file);
            // Only remove our newly created, empty directory; never recursively traverse user files.
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            throw;
        }
    });
    private static string ImportedName(string name) => (name.Length > 155 ? name[..155] : name) + " 导入";
    private static async Task<string> ReadText(ZipArchiveEntry entry) { using var memory = new MemoryStream(); await CopyBounded(entry, memory, 2_000_000); return System.Text.Encoding.UTF8.GetString(memory.ToArray()); }
    private static async Task CopyBounded(ZipArchiveEntry entry, Stream destination, long limit)
    {
        await using var input = entry.Open(); var buffer = new byte[65536]; long read = 0; int count;
        while ((count = await input.ReadAsync(buffer)) != 0) { read += count; if (read > limit || read > entry.Length) throw new InvalidDataException("主题包数据超出声明大小。"); await destination.WriteAsync(buffer.AsMemory(0, count)); }
        if (read != entry.Length) throw new InvalidDataException("主题包数据长度不符。");
    }
}
