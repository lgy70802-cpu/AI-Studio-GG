using DeskDisplay.Core.Models;
using DeskDisplay.Library;
using DeskDisplay.App.Views;
using System.Windows;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    public AsyncCommand HistoryCommand { get; private set; } = null!;
    public AsyncCommand SaveTemplateCommand { get; private set; } = null!;
    public AsyncCommand CustomTemplatesCommand { get; private set; } = null!;
    public AsyncCommand ExportCheckCommand { get; private set; } = null!;
    public ActionCommand LocateRestrictedCommand { get; private set; } = null!;
    public ActionCommand ClearGuidesCommand { get; private set; } = null!;
    private void InitializeProductivity()
    {
        HistoryCommand = new(() => RunAsync(async () => await BrowseSourcesAsync(await _store.HistoryAsync(Project.Id), "历史版本", "恢复为副本")), () => CanEdit && _editing, _logger);
        SaveTemplateCommand = new(() => RunAsync(async () => { await _store.SaveTemplateAsync(Project); Status = "已保存为自定义模板，源主题与素材保持不变。"; }), () => CanEdit && _editing, _logger);
        CustomTemplatesCommand = new(() => RunAsync(async () => await BrowseSourcesAsync(await _store.ListTemplatesAsync(), "自定义模板", "使用模板")), () => CanEdit, _logger);
        ExportCheckCommand = new(() => RunAsync(async () => { var report = await ExportInspectionAsync(); new CreatorExportReport(report) { Owner = Application.Current?.MainWindow }.ShowDialog(); }), () => CanEdit && _editing, _logger);
        LocateRestrictedCommand = new(_ => LocateRestrictedLayer(), _ => CanEdit && _editing);
        ClearGuidesCommand = new(_ => Change(() => { Project.ActiveLayout.VerticalGuides.Clear(); Project.ActiveLayout.HorizontalGuides.Clear(); }), _ => CanEdit && _editing);
    }
    private async Task BrowseSourcesAsync(IReadOnlyList<ThemeProject> sources, string title, string action)
    {
        using var preview = new CreatorViewModel(_main, _logger, _store);
        var browser = new CreatorSourceBrowser(sources, preview, title, action, title == "自定义模板" ? _store.RenameTemplateAsync : null, title == "自定义模板" ? _store.DeleteTemplateAsync : null) { Owner = Application.Current?.MainWindow };
        if (browser.ShowDialog() == true && browser.SelectedSource is { } source) await OpenSourceCopyAsync(source);
    }
    public async Task OpenSourceCopyAsync(ThemeProject source)
    {
        await SaveIfDirtyAsync(); var copy = source.Clone(); copy.Id = Guid.NewGuid(); copy.Name = copy.Name[..Math.Min(155, copy.Name.Length)] + " 副本";
        await OpenProjectAsync(copy); _dirty = true; Changed(false); await PersistDraftAsync();
    }
    public Task<ThemeProject> SaveCustomTemplateAsync() => _store.SaveTemplateAsync(Project);
    public Task<IReadOnlyList<ThemeProject>> CustomTemplatesAsync() => _store.ListTemplatesAsync();
    public Task<IReadOnlyList<ThemeProject>> ProjectHistoryAsync() => _store.HistoryAsync(Project.Id);
    public Task<string> ExportInspectionAsync()
    {
        var snapshot = Project.Clone(); var catalog = LibraryAssets.ToDictionary(i => i.Asset.Id, i => i.Asset);
        return Task.Run(() => ThemeExportInspection.Markdown(snapshot, ThemeExportInspection.Inspect(snapshot, catalog)));
    }
    public void SetGuide(bool vertical, int index, double? position)
    {
        if (!CanEdit) return; var layout = Project.ActiveLayout; var guides = vertical ? layout.VerticalGuides : layout.HorizontalGuides;
        if (index < -1 || index >= guides.Count || position is double v && (!double.IsFinite(v) || v < 0 || v > (vertical ? layout.Width : layout.Height)) || index == -1 && (position is null || guides.Count >= 32)) return;
        Change(() => { if (index == -1) guides.Add(position!.Value); else if (position is null) guides.RemoveAt(index); else guides[index] = position.Value; });
    }
    private Guid? _lastRestricted;
    public void LocateRestrictedLayer()
    {
        var items = Layers.Where(l => l.Locked || !l.Visible).ToArray(); if (items.Length == 0) { Status = "没有隐藏或锁定的图层。"; return; }
        var next = items[(Array.FindIndex(items, l => l.Id == _lastRestricted) + 1) % items.Length]; _lastRestricted = next.Id;
        if (next.GroupId is Guid group) _collapsedGroups.Remove(group);
        RefreshLayers(); SelectedLayer = next; Status = "已定位：" + next.Name + (next.Locked ? " · 已锁定" : "") + (!next.Visible ? " · 已隐藏" : "");
    }
    private void NotifyProductivity()
    {
        foreach (var c in new[] { HistoryCommand, SaveTemplateCommand, CustomTemplatesCommand, ExportCheckCommand }) c?.NotifyCanExecuteChanged();
        LocateRestrictedCommand?.Notify(); ClearGuidesCommand?.Notify();
    }
}
