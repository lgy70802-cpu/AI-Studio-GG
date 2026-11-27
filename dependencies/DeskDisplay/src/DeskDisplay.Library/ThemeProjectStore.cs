using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

/// <summary>Project sources only; assets remain in the existing catalog or original user files.</summary>
public sealed partial class ThemeProjectStore(string? directory = null)
{
    private readonly string _directory = directory ?? Path.Combine(DeskDisplay.Core.Storage.StudioDataPaths.Root, "Themes");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string CoverPath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".png");
    public async Task<IReadOnlyList<ThemeProject>> ListAsync()
    {
        if (!Directory.Exists(_directory)) return [];
        var projects = new List<ThemeProject>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.theme.json"))
        {
            try { var project = await ReadAsync(path); projects.Add(project); }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException) { Serilog.Log.Warning(exception, "Cannot read theme source {Path}; preserving file", path); }
        }
        return projects.OrderByDescending(p => p.ModifiedAt).ToArray();
    }
    public async Task<ThemeProject> ReadAsync(string path)
    {
        var info = new FileInfo(path); if (info.Length > 2_000_000) throw new InvalidDataException("主题项目文件过大。");
        var project = await Task.Run(async () => JsonSerializer.Deserialize<ThemeProject>(await File.ReadAllTextAsync(path))) ?? throw new InvalidDataException("项目为空。");
        project.Validate(); return project;
    }
    public async Task SaveAsync(ThemeProject project)
    {
        project.Validate(); var snapshot = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        if (System.Text.Encoding.UTF8.GetByteCount(snapshot) > 2_000_000) throw new InvalidDataException("主题源项目超过2 MB，请减少图层文字内容。");
        await _gate.WaitAsync(); string? temp = null;
        try { Directory.CreateDirectory(_directory); var path = Path.Combine(_directory, project.Id.ToString("N") + ".theme.json"); temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; await File.WriteAllTextAsync(temp, snapshot); if (File.Exists(path)) await BackupSourceAsync(project.Id, path); File.Move(temp, path, true); }
        finally { if (temp is not null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
    }
    public async Task DeleteAsync(Guid id)
    {
        await _gate.WaitAsync();
        try { DeleteOptionalFile(Path.Combine(_directory, id.ToString("N") + ".theme.json")); DeleteOptionalFile(CoverPath(id)); DeleteOptionalFile(DraftPath(id)); }
        finally { _gate.Release(); }
    }
}
