using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed partial class ThemeProjectStore
{
    public const int HistoryLimit = 10;
    private string HistoryDirectory(Guid id) => Path.Combine(_directory, "History", id.ToString("N"));
    private async Task BackupSourceAsync(Guid id, string source)
    {
        var directory = HistoryDirectory(id); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".theme.json");
        // Preserve exactly the previous source before replacing it, including a damaged file.
        try
        {
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await input.CopyToAsync(output);
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        foreach (var old in Directory.EnumerateFiles(directory, "*.theme.json").Where(p => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(p), @"^\d{21}-[0-9a-f]{32}\.theme\.json$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)).OrderByDescending(Path.GetFileName).Skip(HistoryLimit)) File.Delete(old);
    }
    public async Task<IReadOnlyList<ThemeProject>> HistoryAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            var directory = HistoryDirectory(id); if (!Directory.Exists(directory)) return [];
            var versions = new List<ThemeProject>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.theme.json").OrderByDescending(Path.GetFileName))
                try { var project = await ReadAsync(path); if (project.Id == id) versions.Add(project); }
                catch (Exception e) when (e is IOException or JsonException or InvalidDataException or ArgumentException) { Serilog.Log.Warning(e, "Cannot read theme history {Path}; preserving file", path); }
            return versions;
        }
        finally { _gate.Release(); }
    }
    private ThemeProjectStore TemplatesStore => new(Path.Combine(_directory, "Templates"));
    public Task<IReadOnlyList<ThemeProject>> ListTemplatesAsync() => TemplatesStore.ListAsync();
    public async Task<ThemeProject> SaveTemplateAsync(ThemeProject source)
    {
        var copy = source.Clone(); copy.Id = Guid.NewGuid(); copy.ModifiedAt = DateTimeOffset.UtcNow;
        await TemplatesStore.SaveAsync(copy); return copy;
    }
}
