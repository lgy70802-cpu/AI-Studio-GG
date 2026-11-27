using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed record ThemeDraft(ThemeProject Project, DateTimeOffset SavedAt);

public sealed partial class ThemeProjectStore
{
    public int DraftReadIssueCount { get; private set; }
    internal string ImportedDirectory(Guid id) => Path.Combine(_directory, "ImportedAssets", id.ToString("N"));
    private string DraftPath(Guid id) => Path.Combine(_directory, "Drafts", id.ToString("N") + ".draft.json");
    public async Task SaveDraftAsync(ThemeProject project)
    {
        project.Validate();
        var snapshot = JsonSerializer.Serialize(new ThemeDraft(project, DateTimeOffset.UtcNow));
        if (System.Text.Encoding.UTF8.GetByteCount(snapshot) > 2_000_000) throw new InvalidDataException("草稿超过2 MB。");
        await _gate.WaitAsync(); string? temporary = null;
        try
        {
            var path = DraftPath(project.Id); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, snapshot); File.Move(temporary, path, true);
        }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }
    public async Task<IReadOnlyList<ThemeDraft>> ListDraftsAsync()
    {
        DraftReadIssueCount = 0;
        var directory = Path.Combine(_directory, "Drafts"); if (!Directory.Exists(directory)) return [];
        var drafts = new List<ThemeDraft>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.draft.json"))
        {
            try
            {
                if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("草稿文件过大。");
                var draft = await Task.Run(async () => JsonSerializer.Deserialize<ThemeDraft>(await File.ReadAllTextAsync(path))) ?? throw new InvalidDataException("草稿为空。");
                if (draft.Project is null) throw new InvalidDataException("草稿项目为空。"); draft.Project.Validate();
                if (!string.Equals(Path.GetFileName(path), draft.Project.Id.ToString("N") + ".draft.json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("草稿文件名与项目ID不一致。");
                if (draft.SavedAt == default) throw new InvalidDataException("草稿时间无效。"); drafts.Add(draft);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException)
            { DraftReadIssueCount++; Serilog.Log.Warning(exception, "Cannot read draft {Path}; preserving file", path); }
        }
        return drafts.OrderByDescending(d => d.SavedAt).DistinctBy(d => d.Project.Id).ToArray();
    }
    public async Task DeleteDraftAsync(Guid id)
    {
        await _gate.WaitAsync(); try { DeleteOptionalFile(DraftPath(id)); } finally { _gate.Release(); }
    }
    private static void DeleteOptionalFile(string path)
    {
        try { File.Delete(path); }
        catch (DirectoryNotFoundException) { /* A never-created draft directory is already clean. */ }
    }
}
