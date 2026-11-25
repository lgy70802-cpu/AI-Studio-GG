using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace MiraBox.App;

// Packages contain a checked index and actual media bytes, never absolute source paths.
internal sealed class StudioPackageStore(string root, string webRoot)
{
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    sealed record Asset(byte[]? Data, string? Source, long Length, string Hash);
    static readonly string[] StateNames = ["desktop-state.json", "schedule-state.json"];
    static readonly string[] TransactionNames = StateNames.Prepend("library-workspace.json")
        .SelectMany(name => new[] { name, name + ".previous" }).ToArray();
    public void Export(string path, JsonObject snapshot, JsonObject preferences, string? themeId = null)
    {
        var state = snapshot.DeepClone().AsObject();
        if (themeId != null)
        {
            var theme = state["themes"]!.AsArray().Single(t => t!["id"]!.GetValue<string>() == themeId)!.DeepClone();
            state["themes"] = new JsonArray(theme); state["playlists"] = new JsonArray(); state["schedules"] = new JsonArray();
        }
        var files = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        void Data(string name, byte[] bytes) => files[name] = new(bytes, null, bytes.LongLength, Hash(bytes));
        string Media(string source)
        {
            var hash = FileHash(source); var name = hash + Path.GetExtension(source).ToLowerInvariant();
            files["media/" + name] = new(null, source, new FileInfo(source).Length, hash); return name;
        }
        void Collect(JsonNode? node)
        {
            if (node is JsonObject obj)
                foreach (var (key, child) in obj.ToArray())
                {
                    if (child is JsonValue value && value.TryGetValue<string>(out var url) &&
                        (url.StartsWith("https://media.studio.local/", StringComparison.Ordinal) || url.StartsWith("/src/assets/", StringComparison.Ordinal)))
                    {
                        var managed = url.StartsWith("https:", StringComparison.Ordinal);
                        var baseDir = managed ? Path.Combine(root, "Library") : webRoot;
                        var relative = managed ? new Uri(url).AbsolutePath.TrimStart('/') : url.TrimStart('/');
                        var source = SafePath(baseDir, Uri.UnescapeDataString(relative));
                        if (!File.Exists(source)) throw new FileNotFoundException("打包缺少素材：" + relative);
                        var name = Media(source);
                        obj[key] = "https://media.studio.local/" + name;
                    }
                    else if (child is JsonValue blob && blob.TryGetValue<string>(out var text) && text.StartsWith("blob:", StringComparison.Ordinal))
                        throw new InvalidDataException("工程仍含临时素材，不能生成可迁移包。");
                    else Collect(child);
                }
            else if (node is JsonArray array) foreach (var child in array) Collect(child);
        }
        Collect(state);
        if (themeId == null)
        {
            foreach (var name in StateNames)
            {
                if (!File.Exists(Path.Combine(root, name))) continue;
                var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, name)))!;
                if (name == "desktop-state.json")
                    foreach (var device in data["Devices"]!.AsArray())
                    {
                        foreach (var field in new[] { "ImageFile", "ThemeFile" })
                        {
                            if (device?[field]?.GetValue<string>() is not { } local) continue;
                            var bytes = File.ReadAllBytes(SafePath(root, local));
                            if (field == "ThemeFile") { var theme = JsonNode.Parse(bytes)!; Collect(theme); bytes = System.Text.Encoding.UTF8.GetBytes(theme.ToJsonString()); }
                            var entry = "output-" + Hash(bytes) + Path.GetExtension(local);
                            Data("state/" + entry, bytes); device![field] = entry;
                        }
                        if (device?["VideoFile"]?.GetValue<string>() is { } video)
                        {
                            var namePart = Media(SafePath(root, video)); device!["VideoFile"] = "Library/" + namePart;
                        }
                    }
                Data("state/" + name, System.Text.Encoding.UTF8.GetBytes(data.ToJsonString()));
            }
            Data("preferences.json", System.Text.Encoding.UTF8.GetBytes(preferences.ToJsonString()));
        }
        Data("workspace.json", System.Text.Encoding.UTF8.GetBytes(state.ToJsonString()));
        var manifest = new JsonObject { ["format"] = "MiraBoxStudio.Package", ["version"] = 1, ["scope"] = themeId == null ? "workspace" : "theme", ["createdAt"] = DateTimeOffset.Now.ToString("O"),
            ["files"] = new JsonArray(files.Select(p => (JsonNode)new JsonObject { ["path"] = p.Key, ["sha256"] = p.Value.Hash, ["bytes"] = p.Value.Length }).ToArray()) };
        try
        {
            using (var archive = ZipFile.Open(path + ".tmp", ZipArchiveMode.Create))
            {
                foreach (var (name, asset) in files)
                {
                    using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
                    if (asset.Data != null) stream.Write(asset.Data);
                    else
                    {
                        using var source = File.OpenRead(asset.Source!); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        var buffer = new byte[128 * 1024]; int read; long total = 0;
                        while ((read = source.Read(buffer)) > 0) { stream.Write(buffer, 0, read); hash.AppendData(buffer, 0, read); total += read; }
                        if (total != asset.Length || Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != asset.Hash) throw new IOException("打包期间素材发生变化，请重试。");
                    }
                }
                using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open()); writer.Write(manifest.ToJsonString());
            }
            File.Move(path + ".tmp", path, true);
        }
        finally { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); }
    }
    public static string SafePath(string baseDir, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(baseDir, relative));
        if (!path.StartsWith(Path.GetFullPath(baseDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("包内路径超出目标目录。");
        return path;
    }
    public sealed record CheckedPackage(string Directory, JsonObject Manifest, JsonObject Snapshot);
    public CheckedPackage Inspect(string path)
    {
        var stage = Path.Combine(Path.GetTempPath(), "MiraBoxPackage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try
        {
            using var archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count > 20000 || archive.Entries.Sum(e => e.Length) > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("包过大。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                if (!names.Add(entry.FullName)) throw new InvalidDataException("包内存在重复路径。");
                var target = SafePath(stage, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var source = entry.Open(); using var file = File.Create(target); source.CopyTo(file);
            }
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(stage, "manifest.json")))!.AsObject();
            if (manifest["format"]?.GetValue<string>() != "MiraBoxStudio.Package" || manifest["version"]?.GetValue<int>() != 1) throw new InvalidDataException("不支持此包版本。");
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "manifest.json" };
            foreach (var item in manifest["files"]!.AsArray())
            {
                var name = item!["path"]!.GetValue<string>();
                if (!listed.Add(name) || !(name is "workspace.json" or "preferences.json" || name.StartsWith("media/", StringComparison.Ordinal) && !name[6..].Contains('/') ||
                    name is "state/desktop-state.json" or "state/schedule-state.json" || name.StartsWith("state/output-", StringComparison.Ordinal) && !name[6..].Contains('/')))
                    throw new InvalidDataException("包内文件范围无效。");
                var file = SafePath(stage, item!["path"]!.GetValue<string>());
                if (new FileInfo(file).Length != item["bytes"]!.GetValue<long>() || FileHash(file) != item["sha256"]!.GetValue<string>()) throw new InvalidDataException("素材校验失败：" + Path.GetFileName(file));
            }
            if (!listed.SetEquals(names) || !listed.Contains("workspace.json")) throw new InvalidDataException("包清单与实际文件不一致。");
            var snapshot = JsonNode.Parse(File.ReadAllText(Path.Combine(stage, "workspace.json")))!.AsObject();
            if (snapshot["themes"] is not JsonArray || snapshot["playlists"] is not JsonArray || snapshot["schedules"] is not JsonArray) throw new InvalidDataException("包内索引不完整。");
            var desktop = Path.Combine(stage, "state", "desktop-state.json");
            if (File.Exists(desktop))
            {
                var typed = System.Text.Json.JsonSerializer.Deserialize<StudioWindow.Settings>(File.ReadAllText(desktop))
                    ?? throw new InvalidDataException("设备状态为空。");
                if (typed.Devices == null || typed.Devices.Any(d => d == null)) throw new InvalidDataException("设备状态缺少设备集合。");
                var settings = JsonNode.Parse(File.ReadAllText(desktop))!;
                if (settings["Devices"] is not JsonArray devices) throw new InvalidDataException("设备状态不完整。");
                var ids = new HashSet<string>();
                foreach (var device in devices)
                {
                    var id = device!["Serial"]!.GetValue<string>();
                    if (!ids.Add(id) || string.IsNullOrWhiteSpace(id) || device["Model"]!.GetValue<string>() is not ("D69" or "D92")) throw new InvalidDataException("设备状态无效。");
                    foreach (var field in new[] { "Playing", "Portrait", "Flipped", "ManualStandby" }) if (device[field] != null) _ = device[field]!.GetValue<bool>();
                    if (device["Brightness"] != null && device["Brightness"]!.GetValue<int>() is < 0 or > 100) throw new InvalidDataException("画面亮度无效。");
                    foreach (var field in new[] { "ImageFile", "ThemeFile" })
                        if (device[field]?.GetValue<string>() is { } local && (!local.StartsWith("output-", StringComparison.Ordinal) || !File.Exists(SafePath(Path.Combine(stage, "state"), local)))) throw new InvalidDataException("设备输出依赖不完整。");
                }
            }
            var scheduleFile = Path.Combine(stage, "state", "schedule-state.json");
            if (File.Exists(scheduleFile))
            {
                var schedules = System.Text.Json.JsonSerializer.Deserialize<StudioScheduleState>(File.ReadAllText(scheduleFile));
                if (schedules == null || schedules.History == null || schedules.OnceAt == null || schedules.Completed == null || schedules.Receipts == null || schedules.Versions == null)
                    throw new InvalidDataException("定时状态不完整。");
            }
            return new(stage, manifest, snapshot);
        }
        catch { Directory.Delete(stage, true); throw; }
    }
    public JsonObject Import(CheckedPackage package, bool replace)
    {
        var store = new StudioLibraryStore(root);
        var snapshot = package.Snapshot.DeepClone().AsObject();
        if (replace && package.Manifest["scope"]?.GetValue<string>() != "workspace") throw new InvalidDataException("工程包只能合并导入。");
        var previous = new JsonObject();
        foreach (var name in TransactionNames) previous[name] = File.Exists(Path.Combine(root, name)) ? File.ReadAllText(Path.Combine(root, name)) : null;
        StudioAtomicFile.Write(Path.Combine(root, "restore.pending.json"), previous.ToJsonString());
        try
        {
            var media = Path.Combine(package.Directory, "media");
            if (Directory.Exists(media)) foreach (var source in Directory.GetFiles(media))
            {
                var target = SafePath(store.MediaRoot, Path.GetFileName(source));
                if (File.Exists(target) && FileHash(target) != FileHash(source)) throw new InvalidDataException("现有素材同名但内容不同。");
                if (!File.Exists(target)) File.Copy(source, target);
            }
            if (!replace)
            {
                var current = store.Load() ?? new JsonObject { ["themes"] = new JsonArray(), ["playlists"] = new JsonArray(), ["schedules"] = new JsonArray() };
                var remap = new Dictionary<(string Collection, string Id), string>();
                foreach (var collection in new[] { "themes", "playlists", "schedules" })
                    foreach (var item in snapshot[collection]!.AsArray())
                    {
                        var id = item!["id"]!.GetValue<string>();
                        if (current[collection]!.AsArray().Any(n => n!["id"]!.GetValue<string>() == id))
                        {
                            var next = "import-" + Guid.NewGuid().ToString("N"); remap[(collection, id)] = next; item["id"] = next;
                            if (collection == "themes" && item["editorProject"] is JsonObject project) project["template_id"] = next;
                        }
                    }
                void References(JsonNode? node)
                {
                    if (node is JsonObject obj) foreach (var (key, child) in obj.ToArray())
                    { if (key is "themeId" or "playlistId" && child is JsonValue value && value.TryGetValue<string>(out var id) && remap.TryGetValue((key == "themeId" ? "themes" : "playlists", id), out var next)) obj[key] = next; else References(child); }
                    else if (node is JsonArray array) foreach (var child in array) References(child);
                }
                References(snapshot);
                foreach (var collection in new[] { "themes", "playlists", "schedules" }) foreach (var item in snapshot[collection]!.AsArray()) current[collection]!.AsArray().Add(item!.DeepClone());
                snapshot = current;
            }
            store.Save(snapshot);
            if (replace)
            {
                var stateDir = Path.Combine(package.Directory, "state");
                if (Directory.Exists(stateDir)) foreach (var file in Directory.GetFiles(stateDir))
                {
                    var target = SafePath(root, Path.GetFileName(file));
                    if (StateNames.Contains(Path.GetFileName(file))) StudioAtomicFile.Write(target, File.ReadAllText(file));
                    else if (!File.Exists(target)) File.Copy(file, target);
                    else if (FileHash(target) != FileHash(file)) throw new InvalidDataException("输出缓存同名但内容不同。");
                }
                // Pause on disk before committing the transaction, not only after
                // the host reloads. A crash immediately after Import returns must
                // not start the backup's previously running devices or schedules.
                foreach (var name in StateNames)
                {
                    var target = Path.Combine(root, name);
                    if (!File.Exists(target)) continue;
                    var state = JsonNode.Parse(File.ReadAllText(target))!;
                    if (name == "desktop-state.json")
                    {
                        foreach (var device in state["Devices"]!.AsArray()) device!["Playing"] = false;
                        foreach (var session in state["PlaylistSessions"]?.AsArray() ?? new JsonArray()) session!["Running"] = false;
                    }
                    else state["Enabled"] = false;
                    StudioAtomicFile.Write(target, state.ToJsonString());
                    File.Copy(target, target + ".previous", true);
                }
            }
            File.Delete(Path.Combine(root, "restore.pending.json"));
            return snapshot;
        }
        catch { RecoverRestore(root); throw; }
    }
    public static void RecoverRestore(string root)
    {
        var marker = Path.Combine(root, "restore.pending.json"); if (!File.Exists(marker)) return;
        var previous = JsonNode.Parse(File.ReadAllText(marker))!.AsObject();
        foreach (var (name, value) in previous)
        {
            if (!TransactionNames.Contains(name)) throw new InvalidDataException("恢复记录路径无效。");
            var path = SafePath(root, name);
            if (value == null) { if (File.Exists(path)) File.Delete(path); }
            else { File.WriteAllText(path + ".rollback", value.GetValue<string>()); File.Move(path + ".rollback", path, true); }
        }
        File.Delete(marker);
    }
}
