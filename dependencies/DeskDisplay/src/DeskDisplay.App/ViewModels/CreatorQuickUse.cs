using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    public IReadOnlyList<ThemeCard> RecentUsedProjects => Projects.Where(c => c.LastUsedAt is not null).OrderByDescending(c => c.LastUsedAt).Take(6).ToArray();
    public async Task<ThemeProject> ReadSavedProjectAsync(Guid id) => await _store.ReadSavedAsync(id);
    public async Task RecordThemeUseAsync(Guid id)
    {
        await _store.UpdateUsageAsync(id, DateTimeOffset.Now); await RefreshProjectsAsync();
    }
    private async Task TryRecordThemeUseAsync(Guid id)
    {
        try { await RecordThemeUseAsync(id); }
        catch (Exception ex) { _logger.Warning(ex, "Theme output started but usage save failed"); Status += " 使用记录保存失败。"; }
    }
    public async Task ToggleThemeFavoriteAsync(ThemeCard card)
    {
        await _store.UpdateUsageAsync(card.Project.Id, favorite: !card.Favorite); await RefreshProjectsAsync();
    }
    public void ShowSavedThemes()
    {
        if (_main.Library is not { } library) return;
        library.SelectedResourceScope = "全部资源"; library.SelectedResourceType = "主题"; library.Search = "";
        _main.SelectedPage = AppPage.Library;
    }
}
