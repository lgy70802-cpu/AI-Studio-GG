using System.IO;
using System.Windows;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;
using Microsoft.Win32;

namespace DeskDisplay.App.ViewModels;

public sealed record CreatorDraft(ThemeDraft Draft)
{
    public string Name => Draft.Project.Name;
    public string Details => $"草稿 · {Draft.SavedAt.LocalDateTime:MM-dd HH:mm:ss} · 恢复为独立副本";
}
public sealed record MissingThemeAsset(Guid LayerId, ThemeOrientation Orientation, string Name, string Path, bool IsVideo)
{
    public string Details => (Orientation == ThemeOrientation.Portrait ? "竖屏" : "横屏") + (IsVideo ? " · 视频" : " · 图片");
}

public sealed partial class CreatorViewModel
{
    private DispatcherTimer _draftTimer = null!;
    private Task _draftTask = Task.CompletedTask;
    private long _revision, _lastDraftRevision = -1;
    private Guid _lastDraftProject;
    private bool _templatePicker, _repairing;
    private ThemeTemplate _selectedTemplate = ThemeTemplates.All[0];
    public IReadOnlyList<CreatorDraft> Drafts { get; private set; } = [];
    public bool HasDrafts => Drafts.Count > 0;
    public string DraftFeedback { get; private set; } = "草稿每15秒自动保存，与正式项目分开";
    public IReadOnlyList<ThemeTemplate> Templates => ThemeTemplates.All;
    public ThemeScene TemplateScene { get; private set; } = null!;
    public ThemeTemplate SelectedTemplate
    {
        get => _selectedTemplate;
        set { if (value is null || !SetProperty(ref _selectedTemplate, value)) return; RefreshTemplatePreview(); }
    }
    public double TemplateAspect => SelectedTemplate.Aspect;
    public bool IsTemplates => _templatePicker;
    public bool IsRepairing => _repairing;
    public bool ShowProjectTools => _editing && !_templatePicker && !_repairing;
    public bool ShowSaveState => _editing && !_templatePicker;
    public IReadOnlyList<MissingThemeAsset> MissingAssets { get; private set; } = [];
    public string MissingSummary => MissingAssets.Count == 0 ? "没有发现失效素材" : $"发现 {MissingAssets.Count} 个失效素材引用（包含横竖布局）";
    public ActionCommand BrowseTemplatesCommand { get; private set; } = null!;
    public ActionCommand ReturnFromTemplatesCommand { get; private set; } = null!;
    public AsyncCommand UseTemplateCommand { get; private set; } = null!;
    public ActionCommand RecoverDraftCommand { get; private set; } = null!;
    public ActionCommand DiscardDraftCommand { get; private set; } = null!;
    public AsyncCommand CheckAssetsCommand { get; private set; } = null!;
    public ActionCommand ReturnFromRepairCommand { get; private set; } = null!;
    public ActionCommand RepairAssetCommand { get; private set; } = null!;
    public AsyncCommand ExportSourceCommand { get; private set; } = null!;
    public AsyncCommand ExportBundleCommand { get; private set; } = null!;

    private void InitializeManagement()
    {
        InitializeFolderRepair();
        TemplateScene = new ThemeScene(SelectedTemplate.Create());
        BrowseTemplatesCommand = new(_ => OpenTemplateLibrary(), _ => CanEdit);
        ReturnFromTemplatesCommand = new(_ => { _templatePicker = false; NotifyPage(); });
        UseTemplateCommand = new(() => RunAsync(UseSelectedTemplateAsync), () => CanEdit && TemplateAvailable, _logger);
        RecoverDraftCommand = new(p => { if (p is CreatorDraft draft) Track(RunAsync(() => PreviewDraftRecoveryAsync(draft.Draft))); }, _ => CanEdit);
        DiscardDraftCommand = new(p => { if (p is CreatorDraft draft && MessageBox.Show("丢弃该恢复草稿？正式项目和素材不会删除。", "丢弃草稿", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Track(RunAsync(async () => { await _store.DeleteDraftAsync(draft.Draft.Project.Id); await RefreshDraftsAsync(); })); }, _ => CanEdit);
        CheckAssetsCommand = new(() => RunAsync(async () => { await RefreshMissingAssetsAsync(); _repairing = true; Status = ""; NotifyPage(); }), () => CanEdit && _editing, _logger);
        ReturnFromRepairCommand = new(_ => { _repairing = false; NotifyPage(); });
        RepairAssetCommand = new(p =>
        {
            if (p is not MissingThemeAsset missing) return;
            var dialog = new OpenFileDialog { Title = "重新定位：" + missing.Name, Filter = missing.IsVideo ? "视频|*.mp4" : "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
            if (dialog.ShowDialog() == true) Track(RunAsync(() => RepairAssetAsync(missing, dialog.FileName)));
        }, _ => CanEdit);
        ExportSourceCommand = new(() => RunAsync(() => PickExportAsync(false)), () => CanEdit && _editing, _logger);
        ExportBundleCommand = new(() => RunAsync(() => PickExportAsync(true)), () => CanEdit && _editing, _logger);
        _draftTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
        _draftTimer.Tick += DraftTick; _draftTimer.Start();
    }
    private async void DraftTick(object? sender, EventArgs e)
    {
        if (!CanEdit || _gesture || !_editing || !_dirty || !_draftTask.IsCompleted || _lastDraftProject == Project.Id && _lastDraftRevision == _revision) return;
        try { await PersistDraftAsync(); }
        catch (Exception exception) { _logger.Warning(exception, "Cannot save recovery draft"); DraftFeedback = "草稿保存失败，请手动保存主题"; OnPropertyChanged(nameof(DraftFeedback)); }
    }
    public Task PersistDraftAsync()
    {
        if (!_editing || !_dirty) return Task.CompletedTask;
        var snapshot = Project.Clone(); var revision = _revision;
        _draftTask = WriteDraftAsync(snapshot, revision, _draftTask); return _draftTask;
    }
    private async Task WriteDraftAsync(ThemeProject snapshot, long revision, Task previous)
    {
        try { await previous; } catch (Exception) { /* A failed prior draft must not disable retry. */ }
        await _store.SaveDraftAsync(snapshot);
        _lastDraftProject = snapshot.Id; _lastDraftRevision = revision;
        DraftFeedback = $"草稿已自动保存 · {DateTime.Now:HH:mm:ss}"; OnPropertyChanged(nameof(DraftFeedback));
        OnPropertyChanged(nameof(EditorSaveSummary));
    }
    public async Task RefreshDraftsAsync()
    {
        Drafts = (await _store.ListDraftsAsync()).Where(d => !Projects.Any(p => p.Project.Id == d.Project.Id && p.Project.ModifiedAt >= d.SavedAt)).Select(d => new CreatorDraft(d)).ToArray();
        OnPropertyChanged(nameof(Drafts)); OnPropertyChanged(nameof(HasDrafts));
        if (_store.DraftReadIssueCount > 0) { DraftFeedback = $"已保留 {_store.DraftReadIssueCount} 份无法读取的草稿，可查看诊断日志；其他草稿可正常恢复。"; OnPropertyChanged(nameof(DraftFeedback)); }
    }
    public async Task RecoverDraftAsync(ThemeDraft draft)
    {
        await SaveIfDirtyAsync(); var project = draft.Project.Clone(); project.Id = Guid.NewGuid(); project.Name = (project.Name.Length > 155 ? project.Name[..155] : project.Name) + " 恢复";
        await OpenProjectAsync(project); _dirty = true; Changed(false);
        await PersistDraftAsync(); await _store.DeleteDraftAsync(draft.Project.Id); await RefreshDraftsAsync(); Status = "草稿已恢复为副本，原正式项目保持不变。";
    }
    private async Task FinishSavedDraftAsync()
    {
        try { await _store.DeleteDraftAsync(Project.Id); await RefreshDraftsAsync(); }
        catch (Exception exception) { _logger.Warning(exception, "Theme saved but old draft cleanup failed"); }
    }
    public async Task RefreshMissingAssetsAsync()
    {
        var project = Project.Clone(); var paths = LibraryAssets.ToDictionary(i => i.Asset.Id, i => i.Asset.Path);
        MissingAssets = await Task.Run(() => project.AllLayouts.Select(l => (l.Width >= l.Height ? ThemeOrientation.Landscape : ThemeOrientation.Portrait, l))
            .SelectMany(pair => pair.Item2.Layers.Where(l => l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper)
                .Where(l => !File.Exists(l.AssetId is Guid id && paths.TryGetValue(id, out var path) ? path : l.Path))
                .Select(l => new MissingThemeAsset(l.Id, pair.Item1, l.Name, l.Path, l.IsVideo))).ToArray());
        OnPropertyChanged(nameof(MissingAssets)); OnPropertyChanged(nameof(MissingSummary));
    }
    public async Task RepairAssetAsync(MissingThemeAsset missing, string path)
    {
        var layout = Project.AllLayouts.FirstOrDefault(l => l.Layers.Any(layer => layer.Id == missing.LayerId && layer.Path == missing.Path)) ?? (missing.Orientation == ThemeOrientation.Portrait ? Project.Portrait : Project.Landscape);
        var selected = layout.Layers.FirstOrDefault(l => l.Id == missing.LayerId) ?? throw new InvalidOperationException("该图层已移除。");
        var video = Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        if (video != selected.IsVideo || !File.Exists(path)) throw new InvalidDataException("请选择与原素材相同类型的文件。");
        if (!video) await new DeskDisplay.Display.DataProviders.ImageSceneLoader().LoadAsync(path, selected.Fill);
        var oldId = selected.AssetId; var oldPath = selected.Path;
        var cloudId = selected.CloudResourceId.Length > 0 ? selected.CloudResourceId : LibraryAssets.FirstOrDefault(i => i.Asset.Id == oldId)?.Asset.CloudResourceId ?? "";
        var wasCloud = selected.AssetOrigin == ThemeAssetOrigin.Cloud || cloudId.Length > 0;
        var asset = await AdoptMediaAsync(path);
        _session.Checkpoint();
        foreach (var layer in Project.AllLayouts.SelectMany(l => l.Layers).Where(l => l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper).Where(l => oldId is not null ? l.AssetId == oldId : oldPath.Length > 0 ? l.Path == oldPath : l.Id == selected.Id))
        { layer.AssetId = asset.Id; layer.Path = asset.Path; layer.AssetOrigin = wasCloud || !string.IsNullOrWhiteSpace(asset.CloudResourceId) ? ThemeAssetOrigin.Cloud : ThemeAssetOrigin.Local; layer.CloudResourceId = wasCloud ? cloudId : asset.CloudResourceId; }
        Changed(); await ReloadAssetsAsync(); await RefreshMissingAssetsAsync(); Status = "素材引用已更新，可撤销此次修复。";
    }
    private async Task<WallpaperAsset> AdoptMediaAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = LibraryAssets.FirstOrDefault(i => string.Equals(i.Asset.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Asset;
        if (_main.Library is null) throw new InvalidOperationException("资源库尚未就绪。");
        try { await _main.Library.ImportAsync([fullPath]); }
        finally { _main.SelectedPage = AppPage.Creator; }
        var imported = LibraryAssets.FirstOrDefault(i => string.Equals(i.Asset.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        return imported?.Asset ?? throw new InvalidDataException("素材无法导入，请检查文件格式和资源库状态。");
    }
    public async Task ImportProjectFileAsync(string path)
    {
        await SaveIfDirtyAsync(); var imported = await new ThemeTransferService(_store).ImportAsync(path);
        var assets = new Dictionary<string, WallpaperAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var media in imported.MediaPaths) assets[media] = await AdoptMediaAsync(media);
        foreach (var layer in imported.Project.AllLayouts.SelectMany(l => l.Layers)) if (assets.TryGetValue(layer.Path, out var asset)) layer.AssetId = asset.Id;
        await OpenProjectAsync(imported.Project); _dirty = true; Changed(false); await PersistDraftAsync(); Status = "已导入为新项目，未覆盖已有主题。";
    }
    public Task<ThemeExportResult> ExportProjectAsync(string path, bool bundle) => new ThemeTransferService(_store).ExportAsync(Project, path, bundle, LibraryAssets.ToDictionary(i => i.Asset.Id, i => i.Asset));
    private async Task PickExportAsync(bool bundle)
    {
        var dialog = new SaveFileDialog { Filter = bundle ? "主题包（只打包本地素材）|*.d92theme" : "主题源项目（不含素材）|*.theme.json", FileName = "我的主题", AddExtension = true, DefaultExt = bundle ? ".d92theme" : ".theme.json", OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        if (bundle && new DeskDisplay.App.Views.CreatorExportReport(await ExportInspectionAsync(), true) { Owner = Application.Current?.MainWindow }.ShowDialog() != true) return;
        await _assets; var result = await ExportProjectAsync(dialog.FileName, bundle);
        Status = bundle ? $"已导出项目包：{result.Included} 份本地素材；{result.Omitted} 个云端、未知来源或缺失引用未打包。" : "源项目已导出；换机时需另外提供素材。";
    }
    private void NotifyManagement()
    {
        foreach (var p in new[] { nameof(IsTemplates), nameof(IsRepairing), nameof(ShowProjectTools), nameof(ShowSaveState) }) OnPropertyChanged(p);
        foreach (var command in new[] { BrowseTemplatesCommand, RecoverDraftCommand, DiscardDraftCommand, RepairAssetCommand }) command?.Notify();
        foreach (var command in new[] { UseTemplateCommand, CheckAssetsCommand, ExportSourceCommand, ExportBundleCommand, RepairFolderCommand }) command?.NotifyCanExecuteChanged();
    }
}
