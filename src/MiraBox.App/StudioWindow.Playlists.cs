using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    readonly Dictionary<string, StudioPlaylistProgress> playlistProgress = new();
    readonly HashSet<string> playlistWorking = new();
    readonly Dictionary<string, long> playlistTicks = new();
    readonly Dictionary<string, int> generations = new();
    readonly Dictionary<string, SemaphoreSlim> outputGates = new();
    readonly DispatcherTimer playlistTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    JsonObject? librarySnapshot;
    long lastProgressSave;
    SemaphoreSlim OutputGate(string serial)
    {
        if (!outputGates.TryGetValue(serial, out var gate)) outputGates[serial] = gate = new(1);
        return gate;
    }
    void InvalidatePlaylistWork(string serial)
    {
        generations[serial] = generations.GetValueOrDefault(serial) + 1;
        playlistTicks[serial] = Stopwatch.GetTimestamp();
    }
    StudioPlaylistDefinition? FindPlaylist(string id)
    {
        var node = librarySnapshot?["playlists"]?.AsArray().FirstOrDefault(p => p?["id"]?.GetValue<string>() == id);
        if (node is null) return null;
        var items = node["items"]?.AsArray().Select(item => new StudioPlaylistEntry(
            item!["themeId"]!.GetValue<string>(), StudioPlaylistProgress.Duration(item["durationSeconds"]?.GetValue<double>() ?? 30)))
            .DistinctBy(item => item.ThemeId).ToArray() ?? [];
        return new(id, node["deviceId"]!.GetValue<string>(), node["orientation"]?.GetValue<string>() == "portrait",
            node["shuffle"]?.GetValue<bool>() ?? false, items);
    }
    JsonNode? FindTheme(string id) => librarySnapshot?["themes"]?.AsArray().FirstOrDefault(t => t?["id"]?.GetValue<string>() == id);
    string? ResolveMediaUrl(string? url)
    {
        string? path = null, root = null;
        if (url?.StartsWith("https://media.studio.local/", StringComparison.Ordinal) == true)
        {
            root = library.MediaRoot;
            path = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(new Uri(url).AbsolutePath.TrimStart('/'))));
        }
        else if (url?.StartsWith("/", StringComparison.Ordinal) == true)
        {
            root = Path.Combine(AppContext.BaseDirectory, "Web");
            path = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(url.TrimStart('/'))));
        }
        return root != null && path != null && path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }
    string? PlaylistImagePath(string themeId, bool allowOutsideLibrary = false)
    {
        var theme = FindTheme(themeId);
        if (theme is null || theme["deletedAt"] is not null || (!allowOutsideLibrary && theme["inLibrary"]?.GetValue<bool>() == false)) return null;
        var kind = theme["category"]?.GetValue<string>();
        if (IsRenderedTheme(theme)) return ThemeIssue(theme, null) == null ? Path.Combine(dataRoot, "library-workspace.json") : null;
        if (kind == "视频" && ResolveMediaUrl(theme["thumbnailUrl"]?.GetValue<string>()) == null) return null;
        return kind is "壁纸" or "视频" ? ResolveMediaUrl(theme[kind == "视频" ? "videoUrl" : "imageUrl"]?.GetValue<string>()) : null;
    }
    HashSet<string> Available(StudioPlaylistDefinition playlist) =>
        playlist.Items.Where(item => PlaylistImagePath(item.ThemeId) != null).Select(item => item.ThemeId).ToHashSet();
    SavedDevice PreparePlaylistImage(SavedDevice device, string themeId, bool allowOutsideLibrary = false)
    {
        if (FindTheme(themeId) is { } rendered && rendered["deletedAt"] is null && (allowOutsideLibrary || rendered["inLibrary"]?.GetValue<bool>() != false) && IsRenderedTheme(rendered))
            return PrepareRenderedTheme(device, rendered);
        var path = PlaylistImagePath(themeId, allowOutsideLibrary) ?? throw new InvalidOperationException("素材已回收、文件缺失或类型暂不支持。");
        var theme = FindTheme(themeId)!;
        var video = theme["category"]?.GetValue<string>() == "视频";
        var poster = video ? ResolveMediaUrl(theme["thumbnailUrl"]?.GetValue<string>()) ??
            throw new InvalidOperationException("视频缩略图缺失，请重新导入。") : path;
        var bytes = File.ReadAllBytes(poster);
        var name = "playlist-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".image";
        var target = Path.Combine(dataRoot, name);
        if (!File.Exists(target)) File.WriteAllBytes(target, bytes);
        return device with { ThemeId = themeId, ThemeName = theme["displayName"]!.GetValue<string>(), ImageFile = name,
            VideoFile = video ? Path.GetRelativePath(dataRoot, path) : null, ThemeFile = null, Playing = true };
    }
    async Task CommitPlaylistFrame(StudioPlaylistProgress progress, StudioPlaylistDefinition playlist, int index, bool online, bool keepPlaying = true)
    {
        var serial = progress.Serial;
        var generation = generations.GetValueOrDefault(serial);
        var next = PreparePlaylistImage(saved[serial], playlist.Items[index].ThemeId) with { Playing = keepPlaying };
        if (online) { await Start(next); if (!keepPlaying) await Stop(serial); }
        if (generations.GetValueOrDefault(serial) != generation || closing) return;
        saved[serial] = next;
        progress.Commit(playlist, index);
        playlistTicks[serial] = Stopwatch.GetTimestamp();
        Save();
        Log(new { at = DateTimeOffset.Now, action = "playlist-frame", serial, playlistId = playlist.Id, themeId = next.ThemeId, online });
    }
    async Task PlaylistCommand(string command, System.Text.Json.JsonElement message, ISet<string> online)
    {
        var serial = message.GetProperty("serial").GetString()!;
        await EndThemePreview(serial, true);
        if (!saved.TryGetValue(serial, out var device)) throw new InvalidOperationException("请先选择设备。");

        var playlistId = message.TryGetProperty("playlistId", out var requested) ? requested.GetString() : playlistProgress.GetValueOrDefault(serial)?.PlaylistId;
        var definition = playlistId is { } id ? FindPlaylist(id) : null;
        if (definition is null) throw new InvalidOperationException("轮播列表不存在或已删除。");
        if (definition.DeviceId != serial || definition.Portrait != device.Portrait)
            throw new InvalidOperationException("该列表属于另一台设备或显示方向，请切换到对应设备与方向。");
        if (!playlistProgress.TryGetValue(serial, out var progress) || progress.PlaylistId != definition.Id)
            progress = new() { Serial = serial, PlaylistId = definition.Id };
        if (command == "playlist-pause")
        {
            progress.Running = false;
            playlistTicks[serial] = Stopwatch.GetTimestamp();
            var outputGate = OutputGate(serial);
            await outputGate.WaitAsync();
            outputGate.Release();
            progress.Message = "轮播已暂停";
            playlistProgress[serial] = progress;
            Save();
            return;
        }
        InvalidatePlaylistWork(serial);
        var available = Available(definition);
        if (available.Count == 0) throw new InvalidOperationException("列表中暂无可播放素材，请恢复素材或导入图片、视频。");
        var index = Array.FindIndex(definition.Items, item => item.ThemeId == progress.ThemeId && available.Contains(item.ThemeId));
        var direction = message.TryGetProperty("direction", out var step) ? step.GetInt32() : 1;
        if (command == "playlist-step" || index < 0 || progress.RemainingSeconds <= 0)
            index = progress.Choose(definition, available, direction);
        var previousRunning = progress.Running;
        progress.Running = command == "playlist-start" || previousRunning;
        playlistWorking.Add(serial);
        try
        {
            if (index >= 0 && progress.ThemeId == definition.Items[index].ThemeId) progress.ReconcileDuration(definition.Items[index].DurationSeconds);
            if (command == "playlist-start" && index >= 0 && progress.ThemeId == definition.Items[index].ThemeId && progress.RemainingSeconds > 0)
            {
                var next = PreparePlaylistImage(device, progress.ThemeId);
                if (online.Contains(serial)) await Start(next);
                saved[serial] = next;
                progress.Message = null;
                playlistTicks[serial] = Stopwatch.GetTimestamp();
            }
            else await CommitPlaylistFrame(progress, definition, index, online.Contains(serial), command == "playlist-start" || device.Playing);
            playlistProgress[serial] = progress;
            Save();
        }
        catch { progress.Running = previousRunning; throw; }
        finally { playlistWorking.Remove(serial); }
    }
    void TickPlaylists()
    {
        if (closing || systemSuspended || librarySnapshot is null) return;
        var now = Stopwatch.GetTimestamp();
        foreach (var progress in playlistProgress.Values.ToArray())
        {
            var serial = progress.Serial;
            if (temporaryThemes.ContainsKey(serial)) { playlistTicks[serial] = now; continue; }
            var previous = playlistTicks.GetValueOrDefault(serial, now);
            playlistTicks[serial] = now;
            if (playlistWorking.Contains(serial) || !saved.TryGetValue(serial, out var device)) continue;
            var playlist = FindPlaylist(progress.PlaylistId);
            if (playlist is null) { progress.Running = false; progress.Message = "列表已删除，保留当前画面"; continue; }
            if (playlist.Portrait != device.Portrait || playlist.DeviceId != serial)
            { progress.Running = false; progress.Message = "列表设备或方向已变更"; continue; }
            if (!progress.Running || !device.Playing || standbySerials.Contains(serial) || !connectedSerials.Contains(serial)) continue;
            var available = Available(playlist);
            if (available.Count == 0) { progress.Message = "暂无可播放素材，等待恢复素材"; continue; }
            progress.Message = null;
            var index = Array.FindIndex(playlist.Items, item => item.ThemeId == progress.ThemeId);
            if (progress.ThemeId != null && available.Contains(progress.ThemeId) && !written.Contains(serial)) continue;
            if (index >= 0) progress.ReconcileDuration(playlist.Items[index].DurationSeconds);
            var elapsed = Stopwatch.GetElapsedTime(previous, now).TotalSeconds;
            progress.Advance(elapsed > 3 ? 0 : elapsed, true);
            if (progress.RemainingSeconds <= 0 || progress.ThemeId == null || !available.Contains(progress.ThemeId))
            {
                var next = progress.Choose(playlist, available, 1);
                playlistWorking.Add(serial);
                _ = AdvancePlaylist(progress, playlist, next);
            }
        }
        if (Stopwatch.GetElapsedTime(lastProgressSave, now).TotalSeconds >= 1)
        {
            lastProgressSave = now;
            try { Save(); } catch (Exception ex) { Log(new { at = DateTimeOffset.Now, error = ex.Message }); }
        }
    }
    async Task AdvancePlaylist(StudioPlaylistProgress progress, StudioPlaylistDefinition playlist, int index)
    {
        try { await CommitPlaylistFrame(progress, playlist, index, true); }
        catch (Exception ex) { progress.Message = ex.Message; errors[progress.Serial] = ex.Message; }
        finally { playlistWorking.Remove(progress.Serial); playlistTicks[progress.Serial] = Stopwatch.GetTimestamp(); }
    }
}
