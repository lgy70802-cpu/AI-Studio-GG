using System.Text.Json;

namespace DeskDisplay.Library;

public sealed record ThemeUsage(Guid ProjectId, DateTimeOffset? LastUsedAt = null, bool Favorite = false);

public sealed partial class ThemeProjectStore
{
    public Task<DeskDisplay.Core.Models.ThemeProject> ReadSavedAsync(Guid id) => ReadAsync(Path.Combine(_directory, id.ToString("N") + ".theme.json"));
    public async Task<IReadOnlyList<ThemeUsage>> ReadUsageAsync()
    {
        await _gate.WaitAsync();
        try { return await ReadUsageCoreAsync(); }
        finally { _gate.Release(); }
    }
    private async Task<List<ThemeUsage>> ReadUsageCoreAsync()
    {
        var path = Path.Combine(_directory, "usage.json");
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("主题使用记录过大，原文件已保留。");
        return JsonSerializer.Deserialize<List<ThemeUsage>>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("主题使用记录为空，原文件已保留。");
    }
    public async Task UpdateUsageAsync(Guid id, DateTimeOffset? usedAt = null, bool? favorite = null)
    {
        await _gate.WaitAsync();
        string? temporary = null;
        try
        {
            if (!File.Exists(Path.Combine(_directory, id.ToString("N") + ".theme.json"))) throw new FileNotFoundException("主题已不存在。");
            var items = await ReadUsageCoreAsync(); var old = items.FirstOrDefault(i => i.ProjectId == id) ?? new(id);
            items.RemoveAll(i => i.ProjectId == id);
            items.Add(old with { LastUsedAt = usedAt ?? old.LastUsedAt, Favorite = favorite ?? old.Favorite });
            var path = Path.Combine(_directory, "usage.json"); temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(items)); File.Move(temporary, path, true);
        }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }
}
