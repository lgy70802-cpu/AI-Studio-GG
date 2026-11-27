using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MiraBox.Core;

public sealed record CraftPluginEntry(string Id, string Name, string Version, string Author, string Description,
    string PackageDirectory, string CodePath, string ActionId, int Width, int Height, bool Hour12,
    string ContentHash, long Bytes, int Files, DateTimeOffset ImportedAt, string? DefaultSettings = null, string Format = "Craft");
public sealed record CraftPluginImport(CraftPluginEntry Entry, bool AlreadyImported);

/// <summary>Imports a self-contained Woodland package. Importing never executes plugin code.</summary>
public sealed class CraftPluginStore(string directory)
{
    public const string WoodlandId = "com.mirabox.streamdock.woodlandclock";
    const int MaximumFiles = 2048;
    const long MaximumBytes = 256L * 1024 * 1024;
    readonly string root = Path.GetFullPath(directory);
    string Index => Path.Combine(root, "plugins.json");

    public IReadOnlyList<CraftPluginEntry> Entries => File.Exists(Index)
        ? JsonSerializer.Deserialize<List<CraftPluginEntry>>(File.ReadAllText(Index))
            ?? throw new InvalidDataException("插件索引无效，未覆盖原文件。") : [];

    public string PackagePath(CraftPluginEntry entry)
    {
        if (!Guid.TryParseExact(entry.PackageDirectory, "N", out _)) throw new InvalidDataException("插件副本路径无效。");
        return Path.Combine(root, "Packages", entry.PackageDirectory);
    }

    public CraftPluginImport ImportWoodland(string selectedDirectory, DateTimeOffset importedAt) => Import(selectedDirectory, importedAt, true);
    public CraftPluginImport Import(string selectedDirectory, DateTimeOffset importedAt, bool woodlandOnly = false)
    {
        var source = ResolvePackage(selectedDirectory);
        if (source.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择原始插件目录，不能导入包含插件库的目录。");
        var files = PackageFiles(source);
        var manifestPath = Path.Combine(source, "manifest.json");
        if (new FileInfo(manifestPath).Length > 1024 * 1024) throw new InvalidDataException("插件清单过大。");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var document = manifest.RootElement;
        if (document.ValueKind != JsonValueKind.Object) throw new InvalidDataException("插件清单必须是 JSON 对象。");
        string Text(JsonElement node, string field) => node.TryGetProperty(field, out var item) &&
            item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())
                ? item.GetString()! : throw new InvalidDataException($"插件清单缺少 {field}。");
        bool native = document.TryGetProperty("PixBuddyApiVersion", out var api) && !woodlandOnly;
        if (native && (api.ValueKind != JsonValueKind.Number || !api.TryGetInt32(out var value) || value != 1))
            throw new InvalidDataException("小屏宝插件 API 版本不受支持。");
        var codePath = Text(document, "CodePath");
        if (!codePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(codePath) || codePath.Contains("..") || codePath.IndexOfAny(['#', '?', '%', ':', '\\']) >= 0 || !File.Exists(Path.Combine(source, codePath)) || (!native && (!document.TryGetProperty("SDKVersion", out var sdk) || sdk.ValueKind != JsonValueKind.Number || !sdk.TryGetInt32(out var sdkVersion) || sdkVersion != 1 ||
            Text(document, "APIVersion") != "1.0")))
            throw new InvalidDataException("需要包内 HTML 入口。Craft 插件须使用 SDK 1 / API 1.0。");
        if (!document.TryGetProperty("Actions", out var actions) || actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() != 1)
            throw new InvalidDataException("当前支持包含一个操作的插件。");
        var action = actions[0];
        if (action.ValueKind != JsonValueKind.Object) throw new InvalidDataException("插件操作必须是 JSON 对象。");
        var actionId = Text(action, "UUID");
        if (woodlandOnly && actionId != WoodlandId + ".clock") throw new InvalidDataException("请选择 Woodland Clock 插件，本批尚未支持其他 Craft 插件。");
        if ((woodlandOnly && Text(action, "PropertyInspectorPath") != "index.html") ||
            !action.TryGetProperty("Controllers", out var controllers) || controllers.ValueKind != JsonValueKind.Array ||
            !controllers.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "SecondaryScreen"))
            throw new InvalidDataException("Woodland Clock 的配置页面或设备类型无效。");
        if (!action.TryGetProperty("Settings", out var settings) || settings.ValueKind != JsonValueKind.Object ||
            !settings.TryGetProperty("ActionGeometry", out var geometry) || geometry.ValueKind != JsonValueKind.Object ||
            !geometry.TryGetProperty("width", out var widthValue) || widthValue.ValueKind != JsonValueKind.Number || !widthValue.TryGetInt32(out var width) || width is < 1 or > 8192 ||
            !geometry.TryGetProperty("height", out var heightValue) || heightValue.ValueKind != JsonValueKind.Number || !heightValue.TryGetInt32(out var height) || height is < 1 or > 8192 ||
            (woodlandOnly && (!settings.TryGetProperty("hour12", out var strictHour12) || strictHour12.ValueKind is not (JsonValueKind.True or JsonValueKind.False))))
            throw new InvalidDataException("插件默认尺寸或时间制式无效。");
        if (woodlandOnly) foreach (var required in new[] { "index.html", "interval.js", "images/woodland.png", "fonts/fredoka.ttf", "fonts/fredoka-OFL.txt" })
            if (!File.Exists(Path.Combine(source, required))) throw new InvalidDataException("插件缺少资源：" + required);
        var pluginId = actionId.Contains('.') ? actionId[..actionId.LastIndexOf('.')] : actionId;
        var hash = Hash(source, files);
        Directory.CreateDirectory(root);
        using var importLock = new FileStream(Path.Combine(root, ".import.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var entries = Entries.ToList();
        var existing = entries.FirstOrDefault(entry => entry.Id == pluginId);
        if (existing != null)
        {
            if (existing.ContentHash != hash) throw new InvalidDataException("已导入同一插件的其他内容。本批不覆盖已有插件，请保留当前版本。");
            var managed = PackagePath(existing);
            if (!Directory.Exists(managed) || Hash(managed, PackageFiles(managed)) != existing.ContentHash)
                throw new InvalidDataException("已导入插件的副本缺失或已变更，请先检查插件库。");
            return new(existing, true);
        }
        var id = Guid.NewGuid().ToString("N");
        var entry = new CraftPluginEntry(pluginId, Text(document, "Name"), Text(document, "Version"),
            Text(document, "Author"), Text(document, "Description"), id, codePath, actionId, width, height,
            settings.TryGetProperty("hour12", out var hour12) && hour12.ValueKind == JsonValueKind.True, hash, files.Sum(file => new FileInfo(file).Length), files.Length, importedAt, settings.GetRawText(), native ? "小屏宝" : "Craft");
        var staging = Path.Combine(root, ".import-" + id);
        var destination = PackagePath(entry);
        var temporaryIndex = Path.Combine(root, ".index-" + id + ".tmp");
        var committed = false;
        try
        {
            Directory.CreateDirectory(staging);
            foreach (var file in files)
            {
                var target = Path.Combine(staging, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            if (Hash(staging, PackageFiles(staging)) != hash) throw new IOException("复制期间插件内容发生变化，请重新导入。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            entries.Add(entry);
            using (var stream = new FileStream(temporaryIndex, FileMode.CreateNew, FileAccess.Write))
            { JsonSerializer.Serialize(stream, entries); stream.Flush(true); }
            File.Move(temporaryIndex, Index, true);
            committed = true;
            return new(entry, false);
        }
        finally
        {
            // All cleanup paths are newly generated children of this store; never delete a selected source.
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (!committed && Directory.Exists(destination)) Directory.Delete(destination, true);
            if (File.Exists(temporaryIndex)) File.Delete(temporaryIndex);
        }
    }

    static string ResolvePackage(string selected)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selected));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("插件目录不存在。");
        RejectLink(path);
        if (File.Exists(Path.Combine(path, "manifest.json"))) return path;
        var candidates = Directory.GetDirectories(path, "*.mPanelPlugin");
        if (candidates.Length != 1) throw new InvalidDataException("请选择含 manifest.json 的插件目录，或仅含一个 .mPanelPlugin 的外层目录。");
        RejectLink(candidates[0]);
        if (!File.Exists(Path.Combine(candidates[0], "manifest.json"))) throw new InvalidDataException("插件清单 manifest.json 不存在。");
        return candidates[0];
    }

    static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("插件包不能包含符号链接或目录联接。");
    }

    static string[] PackageFiles(string folder)
    {
        var files = new List<string>();
        long bytes = 0;
        void Visit(string current, int depth)
        {
            if (depth > 32) throw new InvalidDataException("插件目录层级过深。");
            RejectLink(current);
            foreach (var file in Directory.EnumerateFiles(current))
            {
                RejectLink(file);
                bytes += new FileInfo(file).Length;
                files.Add(file);
                if (files.Count > MaximumFiles || bytes > MaximumBytes) throw new InvalidDataException("插件包超过 2048 文件或 256 MB 限制。");
            }
            foreach (var child in Directory.EnumerateDirectories(current)) Visit(child, depth + 1);
        }
        Visit(folder, 0);
        return files.OrderBy(file => Path.GetRelativePath(folder, file), StringComparer.Ordinal).ToArray();
    }

    static string Hash(string folder, string[] files)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            digest.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(folder, file).Replace('\\', '/') + "\0"));
            using var stream = File.OpenRead(file);
            digest.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }
}
