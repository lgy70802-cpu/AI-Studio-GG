using System.Text.Json;

namespace DeskDisplay.Library;

public sealed record ThemePlaylistEntry(Guid ProjectId, int Seconds);
public sealed record ThemePlaylistSettings(string Target, List<ThemePlaylistEntry> Entries)
{
    public void Validate() { if (Target is not ("D92 USB" or "Windows 副屏") || Entries is null || Entries.Count > 100 || Entries.Any(e => e is null || e.ProjectId == Guid.Empty || e.Seconds is < 10 or > 86400) || Entries.Select(e => e.ProjectId).Distinct().Count() != Entries.Count) throw new InvalidDataException("主题轮播配置无效。"); }
}
public sealed partial class ThemeProjectStore
{
    public async Task<ThemePlaylistSettings> ReadThemePlaylistAsync()
    {
        await _gate.WaitAsync(); var path = Path.Combine(_directory, "theme-playlist.json");
        try { if (!File.Exists(path)) return new("D92 USB", []); if (new FileInfo(path).Length > 20000) throw new InvalidDataException("轮播配置过大。"); var result = JsonSerializer.Deserialize<ThemePlaylistSettings>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException(); result.Validate(); return result; }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or ArgumentException) { Serilog.Log.Warning(e, "Cannot read theme playlist; preserving file"); return new("D92 USB", []); }
        finally { _gate.Release(); }
    }
    public async Task SaveThemePlaylistAsync(ThemePlaylistSettings settings)
    {
        settings.Validate(); var json = JsonSerializer.Serialize(settings); string? temp = null; await _gate.WaitAsync();
        try { Directory.CreateDirectory(_directory); var path = Path.Combine(_directory, "theme-playlist.json"); temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; await File.WriteAllTextAsync(temp, json); File.Move(temp, path, true); }
        finally { if (temp is not null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
    }
    public async Task RenameTemplateAsync(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) throw new ArgumentException("模板名称需要1至160个字符。");
        var source = (await TemplatesStore.ListAsync()).Single(p => p.Id == id); source.Name = name.Trim(); source.ModifiedAt = DateTimeOffset.Now; await TemplatesStore.SaveAsync(source);
    }
    public Task DeleteTemplateAsync(Guid id) => TemplatesStore.DeleteAsync(id);
}
