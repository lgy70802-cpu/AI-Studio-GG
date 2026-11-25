using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;
using Microsoft.Win32;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly WallpaperCatalog _catalog;
    private readonly ILogger _logger;
    private WallpaperLibrary _document = new();
    private List<WallpaperItemViewModel> _items = [];
    private readonly DispatcherTimer _rotation;
    private readonly DispatcherTimer _transport;
    private readonly CancellationTokenSource _lifetime = new();
    private Task _pending = Task.CompletedTask, _saveTail = Task.CompletedTask;
    private bool _busy, _closing, _disposed, _details, _rotating, _playlistOnly, _batchMode;
    private bool _initialized;
    public double LastRefreshMilliseconds{get;private set;}
    public double MaxRefreshMilliseconds{get;private set;}
    public long RefreshCount{get;private set;}
    private int _itemsPerScreen = 12, _loadedScreens = 1, _filteredCount, _generation, _currentPage = 1;
    private int _featuredIndex;
    private double _scrollOffset, _viewportWidth, _viewportHeight;
    private readonly Dictionary<string, GalleryState> _galleryStates = [];
    private string _tab = "动态壁纸", _resourceScope = "全部资源", _resourceType = "全部", _category = "全部", _subcategory = "全部", _search = "", _status = "导入你的第一组壁纸，建立自己的桌面收藏。";
    private WallpaperItemViewModel? _selected;
    private string _editName = "", _editCategory = "", _editSubcategory = "", _editTags = "";
    private string _batchCategory = "", _batchSubcategory = "", _batchTags = "";
    public IReadOnlyList<string> Tabs { get; } = ["动态壁纸", "静态壁纸", "在线资源", "小组件", "插件", "我的资源", "USB屏", "设备", "设置", "诊断"];
    public IReadOnlyList<string> ResourceScopes { get; } = ["全部资源", "收藏", "使用历史", "已下载", "已购买", "我的创作"];
    public IReadOnlyList<string> ResourceTypes { get; } = ["全部", "壁纸", "时钟", "插件", "主题"];
    public string SelectedResourceScope { get => _resourceScope; set { if (!string.IsNullOrWhiteSpace(value) && SetProperty(ref _resourceScope, value)) { ResetLoading(); Refresh(); } } }
    public string SelectedResourceType { get => _resourceType; set { if (!string.IsNullOrWhiteSpace(value) && SetProperty(ref _resourceType, value)) { _category = "全部"; _subcategory = "全部"; ResetLoading(); OnPropertyChanged(nameof(SelectedCategory)); OnPropertyChanged(nameof(SelectedSubcategory)); OnPropertyChanged(nameof(ShowCategoryFilters)); Refresh(); } } }
    public bool ShowCategoryFilters => _resourceType == "壁纸";
    private HashSet<string> _cloudOwnedIds = new(StringComparer.OrdinalIgnoreCase);
    public void SetCloudOwnedResources(IEnumerable<string> ids) { _cloudOwnedIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase); Refresh(); }
    public string EmptyStateMessage => _resourceScope == "已购买" ? "暂无已下载的云端授权资源；可在在线资源的“已授权资源”中下载。" : _resourceType is "时钟" or "插件" or "主题" ? $"暂无{_resourceType}资源。" : "当前筛选条件下没有资源。";
    private IReadOnlyList<string> _categories = ["全部"], _subcategories = ["全部"];
    public IReadOnlyList<string> Categories => _categories;
    public IReadOnlyList<string> Subcategories => _subcategories;
    public string SelectedTab { get => _tab; set => ChangeTab(value); }
    public string SelectedCategory { get => _category; set { if (!string.IsNullOrWhiteSpace(value) && SetProperty(ref _category, value)) { ResetLoading(); _subcategory = "全部"; OnPropertyChanged(nameof(SelectedSubcategory)); Refresh(); } } }
    public string SelectedSubcategory { get => _subcategory; set { if (!string.IsNullOrWhiteSpace(value) && SetProperty(ref _subcategory, value)) { ResetLoading(); Refresh(); } } }
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) { ResetLoading(); Refresh(); } } }
    public bool IsGallery => _main.SelectedNavigation == "资源库" && !_details;
    public bool IsDetails => _main.SelectedNavigation == "资源库" && _details;
    public bool IsEmpty => VisibleItems.Count == 0;
    public bool ShowDemoGallery => _initialized && _items.Count == 0 && IsGallery && SavedThemes.Count == 0 && _resourceType is "全部" or "壁纸";
    public bool ShowFilteredEmpty => IsEmpty && SavedThemes.Count == 0 && !ShowDemoGallery;
    public bool CanEdit => _initialized && !_busy && !_closing && _main.Workspace?.CanEdit == true;
    public bool CanPreviewSelected => CanEdit && _selected is not null;
    public bool CanApplySelected => CanPreviewSelected && _main.Devices.TestDisplayCommand.CanExecute(null);
    public bool CanApplyUsbSelected => CanPreviewSelected && _selected?.IsAvailable == true && _main.Usb?.CanApply == true;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public IReadOnlyList<WallpaperItemViewModel> VisibleItems { get; private set; } = [];
    public IReadOnlyList<WallpaperItemViewModel> AllItems => _items;
    public DateTimeOffset LastUsedAt(Guid id) => _document.History.Where(r => r.WallpaperId == id).Select(r => r.UsedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
    private string _browseOrientation = "全部方向";
    public IReadOnlyList<string> BrowseOrientations { get; } = ["全部方向", "横屏素材", "竖屏素材"];
    public string BrowseOrientation
    {
        get => _browseOrientation;
        set
        {
            if (!BrowseOrientations.Contains(value) || !SetProperty(ref _browseOrientation, value)) return;
            ResetLoading();
            Refresh();
        }
    }

    private bool MatchesBrowseOrientation(bool? portrait) => _browseOrientation switch
    {
        "横屏素材" => portrait == false,
        "竖屏素材" => portrait == true,
        _ => true
    };
    public bool ShowThemeResources => _resourceType is "全部" or "主题";
    public bool ShowThemeSection => ShowThemeResources && (SavedThemes.Count > 0 || _resourceType == "主题");
    public bool ShowWallpaperResources => _resourceType != "主题";
    public IReadOnlyList<ThemeCard> SavedThemes => !ShowThemeResources ? [] : (_main.Creator?.Projects ?? []).Where(c =>
        (_resourceScope is "全部资源" or "我的创作" || _resourceScope == "收藏" && c.Favorite || _resourceScope == "使用历史" && c.LastUsedAt is not null)
        && MatchesBrowseOrientation(c.Project.Orientation == ThemeOrientation.Portrait)
        && (string.IsNullOrWhiteSpace(Search) || c.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(c => _resourceScope == "使用历史" ? c.LastUsedAt : c.Project.ModifiedAt).ToArray();
    private int _themePage = 1;
    public IReadOnlyList<ThemeCard> VisibleThemes => SavedThemes.Skip((ThemePage - 1) * 12).Take(12).ToArray();
    public int ThemePage => Math.Clamp(_themePage, 1, ThemePageCount);
    public int ThemePageCount => Math.Max(1, (int)Math.Ceiling(SavedThemes.Count / 12d));
    public bool CanPreviousThemePage => ThemePage > 1;
    public bool CanNextThemePage => ThemePage < ThemePageCount;
    public string ThemePageLabel => $"主题 {SavedThemes.Count} 项 · 第 {ThemePage} / {ThemePageCount} 页";
    public void ChangeThemePage(int delta) { _themePage = Math.Clamp(ThemePage + delta, 1, ThemePageCount); NotifyThemeResources(); }
    public void NotifyThemeResources()
    {
        foreach (var name in new[] { nameof(SavedThemes), nameof(VisibleThemes), nameof(ThemePageLabel), nameof(CanPreviousThemePage), nameof(CanNextThemePage), nameof(ShowThemeResources), nameof(ShowThemeSection), nameof(ShowWallpaperResources), nameof(ShowFilteredEmpty), nameof(ShowDemoGallery) }) OnPropertyChanged(name);
    }
    public IReadOnlyList<WallpaperItemViewModel> RelatedItems { get; private set; } = [];
    public IReadOnlyList<WallpaperItemViewModel> RecentItems
    {
        get
        {
            var ids = _document.History.Select(record => record.WallpaperId).ToList();
            if (ids.Count == 0 && _document.LastPlayedId is Guid id) ids.Add(id);
            return ids.Select(id => _items.FirstOrDefault(item => item.Asset.Id == id && item.IsAvailable))
                .OfType<WallpaperItemViewModel>().Take(6).ToArray();
        }
    }
    public WallpaperItemViewModel? Featured => VisibleItems.ElementAtOrDefault(_featuredIndex);
    public WallpaperItemViewModel? CollectionOne => VisibleItems.Count == 0 ? null : VisibleItems[(_featuredIndex + 1) % VisibleItems.Count];
    public WallpaperItemViewModel? CollectionTwo => VisibleItems.Count == 0 ? null : VisibleItems[(_featuredIndex + 2) % VisibleItems.Count];
    public WallpaperItemViewModel? Selected => _selected;
    public bool SelectedIsVideo => _selected?.Asset.IsVideo == true;
    public bool SelectedIsImage => _selected is not null && !_selected.Asset.IsVideo;
    public bool HasMoreItems => VisibleItems.Count < CurrentPageItemCount;
    public string LoadStatus => HasMoreItems ? $"本页已显示 {VisibleItems.Count} / {CurrentPageItemCount} · 向下滚动继续加载" : $"本页已显示 {VisibleItems.Count} 项";
    public int CurrentPage => _currentPage;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageCapacity));
    public IReadOnlyList<int> PageNumbers => Enumerable.Range(1, PageCount).ToArray();
    public bool ShowPagination => _filteredCount > 0;
    public string PageLabel => HasMoreItems
        ? $"第 {_currentPage} / {PageCount} 页 · 继续下拉，加载满三屏后可翻页"
        : $"第 {_currentPage} / {PageCount} 页";
    public bool CanPreviousPage => _currentPage > 1;
    public bool CanNextPage => !HasMoreItems && _currentPage < PageCount;
    public string LibrarySummary => $"{_items.Count} 份本地壁纸 · {_items.Count(item => item.Asset.Favorite)} 份收藏";
    private bool _carouselPaused;
    private long _switchAt;
    private double _pausedSeconds;
    private string _presetName = "新方案";
    public string PresetName { get => _presetName; set => SetProperty(ref _presetName, value); }
    public IReadOnlyList<CarouselPreset> CarouselPresets => _document.CarouselPresets;
    public CarouselPreset? SelectedPreset
    {
        get => _document.CarouselPresets.FirstOrDefault(p => p.Id == _document.ActiveCarouselPresetId);
        set
        {
            if (!CanChangeRotationTarget || value is null || SelectedPreset == value) return;
            SyncPreset(); _document.ActiveCarouselPresetId = value.Id;
            _document.Playlist = value.Playlist.ToList(); _document.IntervalSeconds = Math.Clamp(value.IntervalSeconds, 10, 86400);
            _document.Shuffle = value.Shuffle; _document.RotateToUsb = value.RotateToUsb;
            _rotationTarget = value.RotateToUsb ? "D92 USB" : "Windows 副屏";
            PresetName = value.Name;
            OnPropertyChanged(nameof(IntervalSeconds)); OnPropertyChanged(nameof(Shuffle)); OnPropertyChanged(nameof(RotationTarget));
            QueueSave(); Refresh();
        }
    }
    private IReadOnlyList<WallpaperItemViewModel> _carouselCandidates = [];
    public IReadOnlyList<WallpaperItemViewModel> CarouselCandidates => _carouselCandidates;
    public ActionCommand CreatePresetCommand { get; }
    public ActionCommand RenamePresetCommand { get; }
    public ActionCommand CarouselPauseCommand { get; }
    public string CarouselPauseLabel => _carouselPaused ? "继续轮播" : "暂停轮播";
    public bool IsCarouselPaused=>_carouselPaused;
    public string CarouselPlaybackStatus => !_rotating ? "轮播已停止" : _carouselPaused ? "轮播已暂停" : UsbRotation && _main.Usb?.IsWaitingForDevice == true ? "等待设备重连" : "正在轮播";
    public string CarouselCurrent => (_rotating ? "当前：" : "最近播放：") + (_items.FirstOrDefault(i => i.Asset.Id == _document.LastPlayedId)?.Name ?? "尚未播放");
    public string CarouselNext
    {
        get
        {
            if (Shuffle) return "下一项：随机选择";
            var items = PlaylistItems.Where(i => i.IsAvailable).ToList();
            var index = items.FindIndex(i => i.Asset.Id == _document.LastPlayedId);
            return "下一项：" + (items.Count == 0 ? "无可用资源" : items[(index + 1) % items.Count].Name);
        }
    }
    public string CarouselCountdown => !_rotating ? "—" : _carouselPaused ? $"剩余 {Math.Ceiling(_pausedSeconds)} 秒" : UsbRotation && _main.Usb?.IsWaitingForDevice == true ? "等待重连" : $"{Math.Max(0, Math.Ceiling((_switchAt - Environment.TickCount64) / 1000d))} 秒后切换";
    public void AddCarouselItems(IEnumerable<Guid> ids)
    {
        if (!CanEdit) return;
        foreach (var id in ids.Distinct()) if (_items.Any(i => i.Asset.Id == id && i.IsAvailable) && !_document.Playlist.Contains(id)) _document.Playlist.Add(id);
        QueueSave(); Refresh();
    }
    public void RemoveCarouselItems(IEnumerable<Guid> ids)
    {
        if (!CanEdit) return;
        var selected = ids.ToHashSet(); _document.Playlist.RemoveAll(selected.Contains); QueueSave(); Refresh();
        if (_document.Playlist.Count == 0) StopRotation();
    }
    public void MoveCarouselItem(Guid source, Guid target)
    {
        if (!CanEdit || source == target) return;
        var from = _document.Playlist.IndexOf(source); var to = _document.Playlist.IndexOf(target);
        if (from < 0 || to < 0) return;
        _document.Playlist.RemoveAt(from); _document.Playlist.Insert(to, source); QueueSave(); Refresh();
    }
    private void SyncPreset()
    {
        var valid = _document.Assets.Select(a => a.Id).ToHashSet();
        foreach (var preset in _document.CarouselPresets) preset.Playlist = (preset.Playlist ?? []).Where(valid.Contains).Distinct().ToList();
        if (SelectedPreset is not { } current) return;
        current.Playlist = _document.Playlist.Where(valid.Contains).Distinct().ToList(); current.IntervalSeconds = IntervalSeconds;
        current.Shuffle = Shuffle; current.RotateToUsb = UsbRotation;
    }
    private void NotifyCarousel()
    {
        foreach (var property in new[] { nameof(CarouselPresets), nameof(SelectedPreset), nameof(CarouselPlaybackStatus), nameof(CarouselCurrent), nameof(CarouselNext), nameof(CarouselCountdown), nameof(CarouselPauseLabel) }) OnPropertyChanged(property);
        CreatePresetCommand?.Notify(); RenamePresetCommand?.Notify(); CarouselPauseCommand?.Notify();
    }
    public string PlaylistLabel => $"轮播列表 · 已选 {_document.Playlist.Count} 项";
    public IReadOnlyList<WallpaperItemViewModel> PlaylistItems => _document.Playlist
        .Select(id => _items.FirstOrDefault(item => item.Asset.Id == id)).OfType<WallpaperItemViewModel>().ToArray();
    public bool HasPlaylistItems => PlaylistItems.Count > 0;
    public bool IsPlaylistEmpty => !HasPlaylistItems;
    public WallpaperItemViewModel? PlaylistSelection
    {
        get => _selected;
        set { if (ReferenceEquals(_selected, value)) return; _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(Selected)); Notify(); }
    }
    public string PlaylistAction => _selected is not null && _document.Playlist.Contains(_selected.Asset.Id) ? "✓ 移出轮播" : "+ 加入轮播";
    public bool PlaylistOnly { get => _playlistOnly; set { if (value && _tab != "我的资源") SelectedTab = "我的资源"; if (SetProperty(ref _playlistOnly, value)) { ResetLoading(); if (value) SelectedCategory = "全部"; Refresh(); } } }
    public string RotationLabel => _rotating ? "Ⅱ 停止轮播" : "▶ 开启轮播";
    public IReadOnlyList<string> RotationTargets { get; } = ["Windows 副屏", "D92 USB"];
    private string _rotationTarget = "Windows 副屏";
    public string RotationTarget
    {
        get => _rotationTarget;
        set { if (CanChangeRotationTarget && RotationTargets.Contains(value) && SetProperty(ref _rotationTarget, value)) { _document.RotateToUsb = UsbRotation; QueueSave(); Notify(); } }
    }
    public bool CanChangeRotationTarget => CanEdit && !_rotating;
    public bool IsRotating => _rotating;
    private bool UsbRotation => RotationTarget == "D92 USB";
    private bool CanUseRotationTarget => UsbRotation ? _main.Usb?.CanApply == true : _main.Devices.TestDisplayCommand.CanExecute(null);
    public IReadOnlyList<int> Intervals { get; } = [10, 30, 60, 180, 300, 600];
    public int IntervalSeconds { get => _document.IntervalSeconds; set { if (!_initialized || value is < 10 or > 86400) return; _document.IntervalSeconds = value; OnPropertyChanged(); RestartRotationTimer(); QueueSave(); } }
    public bool Shuffle { get => _document.Shuffle; set { if (!_initialized) return; _document.Shuffle = value; OnPropertyChanged(); QueueSave(); } }
    public string EditName { get => _editName; set => SetProperty(ref _editName, value); }
    public string EditCategory { get => _editCategory; set => SetProperty(ref _editCategory, value); }
    public string EditSubcategory { get => _editSubcategory; set => SetProperty(ref _editSubcategory, value); }
    public string EditTags { get => _editTags; set => SetProperty(ref _editTags, value); }
    public string BatchCategory { get => _batchCategory; set => SetProperty(ref _batchCategory, value); }
    public string BatchSubcategory { get => _batchSubcategory; set => SetProperty(ref _batchSubcategory, value); }
    public string BatchTags { get => _batchTags; set => SetProperty(ref _batchTags, value); }
    public int SelectedCount => _items.Count(item => item.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public bool IsBatchMode { get => _batchMode; private set => SetProperty(ref _batchMode, value); }
    public double Duration => _main.Videos?.Transport?.Duration.TotalSeconds ?? 0;
    public double Position
    {
        get => _main.Videos?.Transport?.Position.TotalSeconds ?? 0;
        set { _main.Videos?.Transport?.Seek(TimeSpan.FromSeconds(Math.Clamp(value, 0, Duration))); OnPropertyChanged(); }
    }
    public string PositionLabel => $"{TimeSpan.FromSeconds(Position):mm\\:ss} / {TimeSpan.FromSeconds(Duration):mm\\:ss}";
    public string PauseLabel => _main.Videos?.Transport?.IsPaused == true ? "▶ 播放" : "Ⅱ 暂停";
    public bool ShowTransport => !SelectedIsUsbVideo && _selected?.Asset.IsVideo == true && _main.Scene is VideoScene video && SamePath(video.Path, _selected.Asset.Path);
    public bool SelectedIsUsbVideo => _selected is not null && _main.Usb is {IsVideoRunning:true,Scene:VideoFrameScene video} && SamePath(video.Path,_selected.Asset.Path);
    public AsyncCommand ImportCommand { get; }
    public ActionCommand OpenDetailCommand { get; }
    public ActionCommand BackCommand { get; }
    public AsyncCommand PreviewCommand { get; }
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand ApplyUsbCommand { get; }
    public AsyncCommand ApplyDashboardUsbCommand { get; }
    public ActionCommand ApplyCardUsbCommand { get; }
    public ActionCommand FavoriteCommand { get; }
    public ActionCommand PlaylistCommand { get; }
    public ActionCommand SaveMetadataCommand { get; }
    public ActionCommand RemoveCommand { get; }
    public ActionCommand BatchUpdateCommand { get; }
    public ActionCommand BatchRemoveCommand { get; }
    public ActionCommand ClearSelectionCommand { get; }
    public ActionCommand ToggleBatchModeCommand { get; }
    public ActionCommand MoveUpCommand { get; }
    public ActionCommand MoveDownCommand { get; }
    public ActionCommand NextFeaturedCommand { get; }
    public ActionCommand PreviousFeaturedCommand { get; }
    public ActionCommand PreviousPageCommand { get; }
    public ActionCommand NextPageCommand { get; }
    public ActionCommand GoToPageCommand { get; }
    public AsyncCommand RotationCommand { get; }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand PreviousCommand { get; }
    public ActionCommand PauseCommand { get; }
    public ActionCommand StopCommand { get; }
    public Task PendingOperation => _pending;
    public async Task FlushPersistenceAsync(){await _pending;await _saveTail;}
    public event Action<double>? ScrollRestoreRequested;
    private int PageCapacity => Math.Max(3, _itemsPerScreen * 3);
    private int CurrentPageItemCount => Math.Max(0, Math.Min(PageCapacity, _filteredCount - (_currentPage - 1) * PageCapacity));

    public LibraryViewModel(MainViewModel main, WallpaperCatalog catalog, ILogger logger)
    {
        _main = main; _catalog = catalog; _logger = logger;
        CreatePresetCommand = new ActionCommand(_ =>
        {
            SyncPreset();
            var preset = new CarouselPreset { Name = string.IsNullOrWhiteSpace(PresetName) ? "新方案" : PresetName.Trim(), Playlist = _document.Playlist.ToList(), IntervalSeconds = IntervalSeconds, Shuffle = Shuffle, RotateToUsb = UsbRotation };
            _document.CarouselPresets.Add(preset); SelectedPreset = preset; QueueSave(); NotifyCarousel();
        }, _ => CanChangeRotationTarget);
        RenamePresetCommand = new ActionCommand(_ => { if (SelectedPreset is { } preset && !string.IsNullOrWhiteSpace(PresetName)) { preset.Name = PresetName.Trim(); QueueSave(); NotifyCarousel(); } }, _ => CanChangeRotationTarget && SelectedPreset is not null);
        CarouselPauseCommand = new ActionCommand(_ =>
        {
            if (!_carouselPaused) { _pausedSeconds = Math.Max(0.1, (_switchAt - Environment.TickCount64) / 1000d); _rotation!.Stop(); _carouselPaused = true; }
            else { _carouselPaused = false; _switchAt = Environment.TickCount64 + (long)(_pausedSeconds * 1000); _rotation!.Interval = TimeSpan.FromSeconds(_pausedSeconds); _rotation.Start(); }
            RememberCarouselOutput();
            NotifyCarousel();
        }, _ => _rotating && CanEdit);
        ImportCommand = new AsyncCommand(() =>
        {
            var dialog = new OpenFileDialog { Title = "导入本地壁纸（支持多选）", Filter = "壁纸文件|*.jpg;*.jpeg;*.png;*.mp4", Multiselect = true, CheckFileExists = true };
            return dialog.ShowDialog() == true ? ImportAsync(dialog.FileNames) : Task.CompletedTask;
        }, () => CanEdit, logger);
        OpenDetailCommand = new ActionCommand(value => { if (value is WallpaperItemViewModel item) Select(item); }, value => CanEdit && value is WallpaperItemViewModel);
        BackCommand = new ActionCommand(_ => { StopUnusedPreview(); _selected?.ReleaseDetailImage(); _details = false; Refresh(); });
        PreviewCommand = new AsyncCommand(() => { StopRotation(); return RunAsync(() => OpenSelectedAsync(false)); }, () => CanPreviewSelected, logger);
        ApplyCommand = new AsyncCommand(() => { StopRotation(); return RunAsync(() => OpenSelectedAsync(true)); }, () => CanApplySelected, logger);
        ApplyUsbCommand = new AsyncCommand(() => { StopRotation(); return RunAsync(() => OpenSelectedAsync(true, true)); }, () => CanApplyUsbSelected, logger);
        ApplyCardUsbCommand = new ActionCommand(value =>
        {
            if (value is not WallpaperItemViewModel item) return;
            _ = UseWallpaperAsync(item);
        }, value => value is WallpaperItemViewModel item && CanEdit && item.IsAvailable && _main.Usb?.CanApply == true);
        ApplyDashboardUsbCommand = new AsyncCommand(() =>
        {
            StopRotation();
            return RunAsync(async () =>
            {
                if (_selected is null || !_selected.IsAvailable || _selected.Asset.IsVideo) return;
                await _main.Workspace!.OpenDashboardAssetAsync(_selected.Asset.Path, true);
                Status = _main.Workspace.Status;
                Notify();
            });
        }, () => CanApplyUsbSelected && SelectedIsImage, logger);
        FavoriteCommand = new ActionCommand(value =>
        {
            var item = value as WallpaperItemViewModel ?? _selected;
            if (item is null) return;
            item.Asset.Favorite = !item.Asset.Favorite; item.Refresh(); QueueSave(); Refresh();
        }, _ => !_closing);
        PlaylistCommand = new ActionCommand(value =>
        {
            var item = value as WallpaperItemViewModel ?? _selected;
            if (item is null) return;
            if (!_document.Playlist.Remove(item.Asset.Id)) _document.Playlist.Add(item.Asset.Id);
            QueueSave(); Refresh();
        }, _ => !_closing);
        SaveMetadataCommand = new ActionCommand(_ =>
        {
            if (_selected is null) return;
            _selected.Asset.Name = string.IsNullOrWhiteSpace(EditName) ? Path.GetFileNameWithoutExtension(_selected.Asset.Path) : EditName.Trim();
            _selected.Asset.Category = string.IsNullOrWhiteSpace(EditCategory) ? "未分类" : EditCategory.Trim();
            _selected.Asset.Subcategory = string.IsNullOrWhiteSpace(EditSubcategory) ? "未分类" : EditSubcategory.Trim();
            _selected.Asset.Tags = EditTags.Trim(); _selected.Refresh(); QueueSave(); Refresh(); Status = "名称、分类和标签已保存。";
        }, _ => CanPreviewSelected);
        RemoveCommand = new ActionCommand(_ =>
        {
            if (_selected is null) return;
            _document.Assets.Remove(_selected.Asset); _document.Playlist.Remove(_selected.Asset.Id); _document.History.RemoveAll(record => record.WallpaperId == _selected.Asset.Id); _items.Remove(_selected);
            _catalog.RemoveThumbnail(_selected.Asset.Id);
            _selected.PropertyChanged -= OnItemChanged; _selected.ReleaseThumbnail(); _selected = null; _details = false; QueueSave(); Refresh(); Status = "已从资源库移除，原文件未删除。";
        }, _ => CanPreviewSelected);
        BatchUpdateCommand = new ActionCommand(_ =>
        {
            var selected = _items.Where(item => item.IsSelected).ToArray();
            foreach (var item in selected)
            {
                if (!string.IsNullOrWhiteSpace(BatchCategory)) item.Asset.Category = BatchCategory.Trim();
                if (!string.IsNullOrWhiteSpace(BatchSubcategory)) item.Asset.Subcategory = BatchSubcategory.Trim();
                if (!string.IsNullOrWhiteSpace(BatchTags)) item.Asset.Tags = BatchTags.Trim();
                item.Refresh();
            }
            QueueSave(); Refresh(); Status = $"已更新 {selected.Length} 份壁纸的分类或标签。";
        }, _ => CanEdit && HasSelection && (!string.IsNullOrWhiteSpace(BatchCategory) || !string.IsNullOrWhiteSpace(BatchSubcategory) || !string.IsNullOrWhiteSpace(BatchTags)));
        BatchRemoveCommand = new ActionCommand(_ =>
        {
            var selected = _items.Where(item => item.IsSelected).ToArray();
            foreach (var item in selected)
            {
                _document.Assets.Remove(item.Asset); _document.Playlist.Remove(item.Asset.Id); _document.History.RemoveAll(record => record.WallpaperId == item.Asset.Id);
                _catalog.RemoveThumbnail(item.Asset.Id); item.PropertyChanged -= OnItemChanged; item.ReleaseThumbnail(); _items.Remove(item);
            }
            QueueSave(); Refresh(); Status = $"已移除 {selected.Length} 份壁纸，原文件未删除。";
        }, _ => CanEdit && HasSelection);
        ClearSelectionCommand = new ActionCommand(_ => { foreach (var item in _items) item.IsSelected = false; }, _ => HasSelection);
        ToggleBatchModeCommand = new ActionCommand(_ =>
        {
            IsBatchMode = !IsBatchMode;
            if (!IsBatchMode) foreach (var item in _items) item.IsSelected = false;
        });
        MoveUpCommand = new ActionCommand(_ => MovePlaylistItem(-1), _ => CanPreviewSelected && _document.Playlist.Contains(_selected!.Asset.Id));
        MoveDownCommand = new ActionCommand(_ => MovePlaylistItem(1), _ => CanPreviewSelected && _document.Playlist.Contains(_selected!.Asset.Id));
        NextFeaturedCommand = new ActionCommand(_ => { if (VisibleItems.Count > 0) _featuredIndex = (_featuredIndex + 1) % VisibleItems.Count; Refresh(); });
        PreviousFeaturedCommand = new ActionCommand(_ => { if (VisibleItems.Count > 0) _featuredIndex = (_featuredIndex + VisibleItems.Count - 1) % VisibleItems.Count; Refresh(); });
        PreviousPageCommand = new ActionCommand(_ => GoToPage(_currentPage - 1), _ => CanPreviousPage);
        NextPageCommand = new ActionCommand(_ => GoToPage(_currentPage + 1), _ => CanNextPage);
        GoToPageCommand = new ActionCommand(value => { if (value is int page) GoToPage(page); else if (int.TryParse(value?.ToString(), out page)) GoToPage(page); });
        RotationCommand = new AsyncCommand(async () =>
        {
            if (_rotating) { StopRotation(); _main.Workspace?.StopCommand.Execute(null); return; }
            await StartRotationAsync();
        }, () => !_closing && (_rotating || CanEdit && _document.Playlist.Count > 0 && CanUseRotationTarget), logger);
        NextCommand = new AsyncCommand(() => RunAsync(() => AdvanceAsync(1)), () => CanEdit && _document.Playlist.Count > 0 && CanUseRotationTarget, logger);
        PreviousCommand = new AsyncCommand(() => RunAsync(() => AdvanceAsync(-1)), () => CanEdit && _document.Playlist.Count > 0 && CanUseRotationTarget, logger);
        PauseCommand = new ActionCommand(_ => { var transport = _main.Videos?.Transport; if (transport?.IsPaused == true) transport.Resume(); else transport?.Pause(); NotifyTransport(); }, _ => ShowTransport && Duration > 0);
        StopCommand = new ActionCommand(_ => { StopRotation(); _main.Workspace?.StopCommand.Execute(null); });
        _rotation = new DispatcherTimer(); _rotation.Tick += OnRotationTick;
        _transport = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; _transport.Tick += OnTransportTick; _transport.Start();
        main.Workspace!.PropertyChanged += OnWorkspaceChanged;
        main.PropertyChanged += OnMainChanged;
        if (main.Usb is not null) main.Usb.PropertyChanged += OnUsbChanged;
    }
    public Task UseWallpaperAsync(WallpaperItemViewModel item)
    {
        if(!item.IsAvailable)
        { _main.Output.Report("原壁纸文件已丢失："+item.Name+"。请修复素材后重试。",true);var id=item.Asset.Id;var serial=_main.Usb?.SelectedSerial;_main.Output.SetRetry(()=>{if(_main.Usb?.SelectedSerial!=serial)throw new InvalidOperationException("目标设备已改变，请重新选择内容并应用。");var current=_items.FirstOrDefault(i=>i.Asset.Id==id)??throw new InvalidOperationException("资源已移除，请重新选择内容。");return UseWallpaperAsync(current);}); return Task.CompletedTask; }
        if(!CanEdit || _main.Usb?.CanApply!=true)
        { _main.Output.Report("暂时无法使用壁纸，请检查 D92 连接与输出设置，或等待当前操作完成。",true); return Task.CompletedTask; }
        StopRotation();_selected=item;Notify();
        return RunAsync(()=>OpenSelectedAsync(true,true));
    }
    public Task InitializeAsync() => RunAsync(async () =>
    {
        var firstUse = !_catalog.Exists;
        _document = await _catalog.LoadAsync();
        if (_closing) return;
        if (firstUse)
        {
            foreach (var path in _main.Workspace!.RememberedFiles.Where(File.Exists))
            {
                try { _document.Assets.Add(await _catalog.ImportAsync(path, _lifetime.Token)); }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
                catch (Exception exception) { _logger.Warning(exception, "Cannot migrate recent wallpaper {Path}", path); }
            }
        }
        _items = _document.Assets.Select(CreateItem).ToList();
        _document.CarouselPresets ??= [];
        _document.CarouselPresets = _document.CarouselPresets.Where(p => p is not null && p.Id != Guid.Empty).DistinctBy(p => p.Id).ToList();
        if (_document.CarouselPresets.Count == 0) _document.CarouselPresets.Add(new CarouselPreset { Playlist = _document.Playlist.ToList(), IntervalSeconds = _document.IntervalSeconds, Shuffle = _document.Shuffle, RotateToUsb = _document.RotateToUsb });
        if (!_document.CarouselPresets.Any(p => p.Id == _document.ActiveCarouselPresetId)) _document.ActiveCarouselPresetId = _document.CarouselPresets[0].Id;
        PresetName = SelectedPreset!.Name;
        _initialized = true;
        _rotationTarget = _document.RotateToUsb ? "D92 USB" : "Windows 副屏";
        OnPropertyChanged(nameof(RotationTarget));
        if (firstUse && _items.Count > 0) { QueueSave(); Status = "已收录最近使用的素材，可继续导入更多壁纸。"; }
        OnPropertyChanged(nameof(IntervalSeconds)); OnPropertyChanged(nameof(Shuffle)); Refresh();
    }, true);
    public Task ImportAsync(IEnumerable<string> paths) => RunAsync(async () =>
    {
        var imported = 0; var failed = 0; var skipped = 0;
        foreach (var path in paths)
        {
            if (_closing) break;
            try
            {
                if (_document.Assets.Any(item => SamePath(item.Path, Path.GetFullPath(path)))) { skipped++; continue; }
                var asset = await _catalog.ImportAsync(path, _lifetime.Token);
                if (_closing) break;
                _document.Assets.Add(asset); _items.Add(CreateItem(asset)); imported++;
                Status = $"正在导入…已完成 {imported} 项";
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { break; }
            catch (Exception exception) { failed++; _logger.Error(exception, "Cannot import wallpaper {Path}", path); }
        }
        _galleryStates.Remove("我的资源"); SelectedTab = "我的资源"; _details = false; _playlistOnly = false; _category = "全部"; _subcategory = "全部"; _search = ""; ResetLoading(); Refresh(); QueueSave();
        Status = $"导入完成：{imported} 项，重复跳过 {skipped} 项，失败 {failed} 项。";
    });
    public async Task RelinkAssetAsync(Guid id,string path)
    {
        if(!CanEdit)throw new InvalidOperationException("资源库正在处理其他操作，请稍后重试。");
        var item=_items.SingleOrDefault(i=>i.Asset.Id==id)??throw new InvalidOperationException("资源已移除。");
        var oldPath=item.Asset.Path;var oldWidth=item.Asset.Width;var oldHeight=item.Asset.Height;var oldDuration=item.Asset.DurationSeconds;
        _busy=true;Notify();WallpaperAsset? imported=null;
        try
        {
            imported=await _catalog.ImportAsync(path,_lifetime.Token);
            if(imported.IsVideo!=item.Asset.IsVideo)throw new InvalidOperationException("替换文件类型与原素材不一致。");
            item.Asset.Path=imported.Path;item.Asset.Width=imported.Width;item.Asset.Height=imported.Height;item.Asset.DurationSeconds=imported.DurationSeconds;
            await _saveTail;if(!await _catalog.SaveAsync(_document))throw new System.IO.IOException("资源库保存失败。");
            item.ReleaseThumbnail();_catalog.RemoveThumbnail(id);item.Refresh();Refresh();Status="资源路径已更新，原文件未修改。";
        }
        catch{item.Asset.Path=oldPath;item.Asset.Width=oldWidth;item.Asset.Height=oldHeight;item.Asset.DurationSeconds=oldDuration;throw;}
        finally{if(imported is not null)_catalog.RemoveThumbnail(imported.Id);_busy=false;Notify();}
    }
    public string? GetCloudVersion(string resourceId)
    {
        var asset = _items.FirstOrDefault(item => CloudMatches(item.Asset, resourceId))?.Asset;
        if (asset is null) return null;
        if (!string.IsNullOrWhiteSpace(asset.CloudVersion)) return asset.CloudVersion;
        var name = Path.GetFileNameWithoutExtension(asset.Path);
        return name.StartsWith(resourceId + "-", StringComparison.OrdinalIgnoreCase) ? name[(resourceId.Length + 1)..] : null;
    }
    private static bool CloudMatches(WallpaperAsset asset, string resourceId) => asset.CloudResourceId == resourceId || Path.GetFileNameWithoutExtension(asset.Path).StartsWith(resourceId + "-", StringComparison.OrdinalIgnoreCase);
    public Task ImportCloudAsync(string path, string name, string resourceId, string version, string category, string tags, bool applyUsb) => RunAsync(async () =>
    {
        var fullPath = Path.GetFullPath(path);
        var item = _items.FirstOrDefault(candidate => CloudMatches(candidate.Asset, resourceId)) ?? _items.FirstOrDefault(candidate => SamePath(candidate.Asset.Path, fullPath));
        if (item is null)
        {
            var asset = await _catalog.ImportAsync(fullPath, _lifetime.Token);
            asset.Name = string.IsNullOrWhiteSpace(name) ? asset.Name : name.Trim();
            asset.Category = "云端资源"; asset.Subcategory = string.IsNullOrWhiteSpace(category) ? "官方" : category;
            asset.Tags = tags; asset.CloudResourceId = resourceId; asset.CloudVersion = version;
            _document.Assets.Add(asset); item = CreateItem(asset); _items.Add(item); QueueSave();
        }
        else
        {
            var updated = await _catalog.ImportAsync(fullPath, _lifetime.Token);
            item.ReleaseThumbnail(); _catalog.RemoveThumbnail(item.Asset.Id);
            item.Asset.Path = fullPath; item.Asset.CloudResourceId = resourceId; item.Asset.CloudVersion = version;
            item.Asset.Width = updated.Width; item.Asset.Height = updated.Height;
            // Keep local name, categories, tags, favorite, identity and playlist membership.
            item.Refresh(); QueueSave();
        }
        if (applyUsb)
        {
            Select(item); await OpenSelectedAsync(true, true);
        }
        else Status = $"已下载到本地资源库：{item.Name}";
        Refresh();
    });
    public void Select(WallpaperItemViewModel item)
    {
        if (_main.Scene is VideoScene video && !SamePath(video.Path, item.Asset.Path)) StopUnusedPreview();
        if (!ReferenceEquals(_selected, item)) _selected?.ReleaseDetailImage();
        _selected = item; _details = true;
        EditName = item.Name; EditCategory = item.Category; EditSubcategory = item.Subcategory; EditTags = item.Tags;
        RelatedItems = _items.Where(other => other != item)
            .OrderByDescending(other => other.Category == item.Category && other.Subcategory == item.Subcategory)
            .ThenByDescending(other => other.Category == item.Category).Take(3).ToArray();
        Status = item.IsAvailable ? "点击预览壁纸，或直接应用到已选择的副屏。" : "原文件已丢失，请重新导入。";
        Refresh();
    }
    public bool SelectedIsLive => SelectedIsUsbVideo || _selected is not null && (_main.Scene switch
    {
        ImageScene image => SamePath(image.Path, _selected.Asset.Path),
        VideoScene video => SamePath(video.Path, _selected.Asset.Path),
        _ => false
    });
    private async Task<bool> OpenSelectedAsync(bool apply, bool usb = false)
    {
        var item = _selected;
        if (item is null || !item.IsAvailable) { Status = "文件不存在，请重新导入。"; return false; }
        if (usb && item.Asset.IsVideo)
        {
            var accepted = _main.Usb is { } output && await output.PlayVideoAsync(item.Asset.Path, preserveCarousel: _rotating);
            Status = _main.Usb?.Status ?? "请配置 D92。";
            if(accepted)RecordUsage(item,"D92 USB");Notify();return accepted;
        }
        if (SelectedIsLive && _main.Workspace!.IsReady)
        { if (apply) { if (usb) await _main.Workspace.ApplyUsbAsync(); else await _main.Workspace.ApplyAsync(); } }
        else await _main.Workspace!.OpenAssetAsync(item.Asset.Path, item.Asset.IsVideo, apply, usb);
        Status = _main.Workspace.Status;
        if (SelectedIsLive && _main.Workspace.IsReady)
        {
            RecordUsage(item, !apply ? "预览" : usb ? "D92 USB" : "Windows 副屏");
        }
        Notify();
        return SelectedIsLive && _main.Workspace.IsReady && (!apply || (usb ? _main.Workspace.IsUsbPreviewApplied : _main.Workspace.IsPreviewApplied));
    }
    private void RecordUsage(WallpaperItemViewModel item, string target)
    {
        _document.LastPlayedId = item.Asset.Id;
        _document.History.RemoveAll(record => record.WallpaperId == item.Asset.Id);
        _document.History.Insert(0, new WallpaperUsageRecord { WallpaperId = item.Asset.Id, UsedAt = DateTimeOffset.UtcNow, Target = target });
        if (_document.History.Count > 20) _document.History.RemoveRange(20, _document.History.Count - 20);
        QueueSave(); OnPropertyChanged(nameof(RecentItems));
    }
    public void RecordExternalUse(Guid assetId,string target){if(_items.FirstOrDefault(i=>i.Asset.Id==assetId) is { } item)RecordUsage(item,target);}
    public async Task StartRotationAsync(Guid? startAt=null,bool paused=false)
    {
        _main.ActiveMixedPlaylist?.Stop();
        if(_rotating || _closing || !CanEdit)return;
        _rotating=true;++_generation;Notify();await RunAsync(()=>AdvanceAsync(1,startAt));
        RestartRotationTimer();if(paused && _rotating)CarouselPauseCommand.Execute(null);
    }
    private void RememberCarouselOutput()
    {if(UsbRotation && _main.Usb is {IsRunning:true,SelectedSerial:{Length:>0} serial} usb)_main.RememberLegacyOutput(serial,usb.Scene);}
    private async Task AdvanceAsync(int direction,Guid? startAt=null)
    {
        _rotation.Stop();
        var generation = _generation;
        if (UsbRotation && _main.Usb?.IsWaitingForDevice == true)
        { Status = "D92 已断开，暂停切换壁纸；等待同一设备恢复。"; RestartRotationTimer(); return; }
        if (UsbRotation && _main.Usb?.ValidateConfiguration() != true)
        { StopRotation(); SelectedTab = "USB屏"; Status = "请先配置 USB 设备和画布尺寸。"; return; }
        var candidates = _document.Playlist.Select(id => _items.FirstOrDefault(item => item.Asset.Id == id)).OfType<WallpaperItemViewModel>().ToList();
        if (candidates.Count == 0) { StopRotation(); Status = "轮播列表为空，请在详情页添加壁纸或视频。"; return; }
        var last = candidates.FindIndex(item => item.Asset.Id == _document.LastPlayedId);
        if (last < 0 && direction < 0) last = 0;
        if (_document.Shuffle)
        {
            candidates = candidates.OrderBy(_ => Random.Shared.Next()).OrderBy(item => item.Asset.Id == _document.LastPlayedId).ToList();
            last = -1; direction = 1;
        }
        if(startAt is { } id){var requested=candidates.FindIndex(i=>i.Asset.Id==id);if(requested>=0){last=(requested-1+candidates.Count)%candidates.Count;direction=1;}}
        for (var offset = 1; offset <= candidates.Count; offset++)
        {
            if (_closing || generation != _generation) return;
            if (!CanUseRotationTarget) { StopRotation(); Status = "目标输出不可用或正忙，轮播已停止。"; return; }
            var index = ((last + direction * offset) % candidates.Count + candidates.Count) % candidates.Count;
            var item = candidates[index];
            if (!item.IsAvailable) { _logger.Warning("Skipping missing playlist asset {Path}", item.Asset.Path); continue; }
            _selected = item;
            EditName = item.Name; EditCategory = item.Category; EditSubcategory = item.Subcategory; EditTags = item.Tags;
            if (await OpenSelectedAsync(true, UsbRotation))
            {
                if (generation != _generation || _closing) return;
                Refresh();
                Status = $"{RotationTarget} 正在显示：{item.Name}"; RestartRotationTimer(); return;
            }
        }
        StopRotation(); Status = "列表中的文件均不可用，轮播已停止。";
    }
    private async void OnRotationTick(object? sender, EventArgs e)
    {
        _rotation.Stop();
        if (!_rotating || _carouselPaused || _closing) return;
        if (!CanEdit) { RestartRotationTimer(); return; }
        await RunAsync(() => AdvanceAsync(1));
    }
    private void RestartRotationTimer() { _rotation.Stop(); _pausedSeconds = IntervalSeconds; _switchAt = Environment.TickCount64 + IntervalSeconds * 1000L; _rotation.Interval = TimeSpan.FromSeconds(IntervalSeconds); if (_rotating && !_carouselPaused && !_closing) _rotation.Start(); NotifyCarousel(); }
    public void StopRotation() { var was=_rotating;++_generation; _rotating = false; _carouselPaused = false; _rotation.Stop(); Notify();if(was&&!_closing)RememberCarouselOutput(); }
    private void MovePlaylistItem(int delta)
    {
        var index = _selected is null ? -1 : _document.Playlist.IndexOf(_selected.Asset.Id);
        var next = index + delta;
        if (index < 0 || next < 0 || next >= _document.Playlist.Count) return;
        (_document.Playlist[index], _document.Playlist[next]) = (_document.Playlist[next], _document.Playlist[index]);
        QueueSave(); Refresh(); Status = "轮播顺序已更新。";
    }
    private static bool IsResourceTab(string tab) => tab is "动态壁纸" or "静态壁纸" or "我的资源";
    private void ChangeTab(string value)
    {
        if (!Tabs.Contains(value) || value == _tab) return;
        if (IsResourceTab(_tab)) SaveGalleryState(_tab);
        StopUnusedPreview();
        SetProperty(ref _tab, value);
        _main.SelectedNavigation = value switch { "设备" or "USB屏" or "设置" or "诊断" or "插件" => value, "在线资源" => "在线资源", "小组件" => "首页", _ => "资源库" };
        if (IsResourceTab(value)) RestoreGalleryState(value);
        Refresh();
        if (IsResourceTab(value)) RequestScrollRestore(_scrollOffset);
    }
    private void SaveGalleryState(string tab) => _galleryStates[tab] = new GalleryState(
        _category, _subcategory, _search, tab == "我的资源" && _playlistOnly,
        _currentPage, _loadedScreens, _details, _selected?.Asset.Id, _scrollOffset);
    private void RestoreGalleryState(string tab)
    {
        var state = _galleryStates.GetValueOrDefault(tab) ?? new GalleryState("全部", "全部", "", false, 1, 1, false, null, 0);
        _category = state.Category; _subcategory = state.Subcategory; _search = state.Search;
        _playlistOnly = tab == "我的资源" && state.PlaylistOnly;
        _currentPage = Math.Max(1, state.Page); _loadedScreens = Math.Clamp(state.LoadedScreens, 1, 3);
        _details = state.Details;
        _selected = state.SelectedId is Guid id ? _items.FirstOrDefault(item => item.Asset.Id == id) : null;
        if (_selected is null) _details = false;
        _scrollOffset = state.ScrollOffset;
        foreach (var property in new[] { nameof(SelectedCategory), nameof(SelectedSubcategory), nameof(Search), nameof(PlaylistOnly), nameof(Selected), nameof(IsGallery), nameof(IsDetails) }) OnPropertyChanged(property);
    }
    private void RequestScrollRestore(double offset) => Dispatcher.CurrentDispatcher.BeginInvoke(() => ScrollRestoreRequested?.Invoke(offset));
    public void UpdateGalleryViewport(double width, double height, double offset)
    {
        _scrollOffset = Math.Max(0, offset);
        if (width <= 0 || height <= 0) return;
        // A vertical scrollbar changes the reported viewport width by only a few pixels.
        // Refreshing for that change can toggle the scrollbar again and create an endless
        // ScrollChanged -> Refresh -> layout loop on the UI thread.
        if (_viewportWidth > 0 && Math.Abs(width - _viewportWidth) < 32 && Math.Abs(height - _viewportHeight) < 32) return;
        _viewportWidth = width; _viewportHeight = height;
        var cardWidth = Math.Max(320, (width - 16) / 2);
        var rows = Math.Max(1, (int)Math.Ceiling(height / (cardWidth / 4d + 70)));
        var count = rows * 2;
        if (count == _itemsPerScreen) return;
        _itemsPerScreen = count; _loadedScreens = 1; _currentPage = 1;
        Refresh();
    }
    private void GoToPage(int page)
    {
        var target = Math.Clamp(page, 1, PageCount);
        if (target == _currentPage) return;
        _currentPage = target; _loadedScreens = 1; _scrollOffset = 0;
        Refresh(); RequestScrollRestore(0);
    }
    private void Refresh()
    {
        var refreshWatch=System.Diagnostics.Stopwatch.StartNew();
        _themePage = 1; NotifyThemeResources();
        var historyIds = _document.History.Select(record => record.WallpaperId).ToHashSet();
        var playlistOrder = _document.Playlist.Select((id, index) => (id, index)).GroupBy(p => p.id).ToDictionary(g => g.Key, g => g.First().index);
        var filtered = _items.Where(item => (_tab != "动态壁纸" || item.Asset.IsVideo) && (_tab != "静态壁纸" || !item.Asset.IsVideo)
            && (_resourceType is "全部" or "壁纸")
            && MatchesBrowseOrientation(item.Asset.Width > 0 && item.Asset.Height > 0 ? item.IsPortrait : null)
            && (_resourceScope == "全部资源" || (_resourceScope == "收藏" && item.Asset.Favorite)
                || (_resourceScope == "使用历史" && historyIds.Contains(item.Asset.Id))
                || (_resourceScope == "已下载" && !string.IsNullOrWhiteSpace(item.Asset.CloudResourceId))
                || (_resourceScope == "已购买" && item.Asset.CloudResourceId is { } cloudId && _cloudOwnedIds.Contains(cloudId))
                || (_resourceScope == "我的创作" && string.IsNullOrWhiteSpace(item.Asset.CloudResourceId)))
            && (_category == "全部" || item.Category == _category)
            && (_subcategory == "全部" || item.Subcategory == _subcategory)
            && (!_playlistOnly || playlistOrder.ContainsKey(item.Asset.Id))
            && (string.IsNullOrWhiteSpace(_search) || $"{item.Name} {item.Tags} {item.Category} {item.Subcategory}".Contains(_search, StringComparison.OrdinalIgnoreCase))).ToList();
        if (_playlistOnly) filtered = filtered.OrderBy(item => playlistOrder.GetValueOrDefault(item.Asset.Id)).ToList();
        else if (_resourceScope == "使用历史")
        {
            var order = _document.History.GroupBy(record => record.WallpaperId).ToDictionary(group => group.Key, group => group.Max(record => record.UsedAt));
            filtered = filtered.OrderByDescending(item => order.GetValueOrDefault(item.Asset.Id)).ToList();
        }
        else filtered = filtered.OrderByDescending(item => item.Asset.AddedAt).ThenByDescending(item => item.Asset.Id).ToList();
        _filteredCount = filtered.Count;
        _currentPage = Math.Clamp(_currentPage, 1, PageCount);
        var pageItems = filtered.Skip((_currentPage - 1) * PageCapacity).Take(PageCapacity).ToList();
        var visible = pageItems.Take(Math.Min(pageItems.Count, _itemsPerScreen * _loadedScreens)).ToArray();
        if (!VisibleItems.SequenceEqual(visible)) { VisibleItems = visible; OnPropertyChanged(nameof(VisibleItems)); }
        var categories = new[] { "全部" }.Concat(_items.Select(item => item.Category).Distinct().Order()).ToArray();
        if (!_categories.SequenceEqual(categories)) { _categories = categories; OnPropertyChanged(nameof(Categories)); }
        var subcategories = new[] { "全部" }.Concat(_items.Where(MatchesPrimaryCategory).Select(item => item.Subcategory).Distinct().Order()).ToArray();
        if (!_subcategories.SequenceEqual(subcategories)) { _subcategories = subcategories; OnPropertyChanged(nameof(Subcategories)); }
        var candidates = _items.Where(i => !playlistOrder.ContainsKey(i.Asset.Id)).ToArray();
        if (!_carouselCandidates.SequenceEqual(candidates)) { _carouselCandidates = candidates; OnPropertyChanged(nameof(CarouselCandidates)); }
        _featuredIndex = Math.Clamp(_featuredIndex, 0, Math.Max(0, VisibleItems.Count - 1));
        // Retain a bounded warm cache across filters; do not repeatedly decode the same files.
        TrimTransientImages([]);
        foreach (var property in new[] { nameof(Featured), nameof(CollectionOne), nameof(CollectionTwo), nameof(IsEmpty), nameof(EmptyStateMessage), nameof(ShowDemoGallery), nameof(ShowFilteredEmpty), nameof(HasMoreItems), nameof(LoadStatus), nameof(LibrarySummary), nameof(AllItems), nameof(PlaylistLabel), nameof(PlaylistItems), nameof(HasPlaylistItems), nameof(IsPlaylistEmpty), nameof(PlaylistAction), nameof(Selected), nameof(RelatedItems), nameof(RecentItems), nameof(IsGallery), nameof(IsDetails), nameof(CurrentPage), nameof(PageCount), nameof(PageNumbers), nameof(ShowPagination), nameof(PageLabel), nameof(CanPreviousPage), nameof(CanNextPage) }) OnPropertyChanged(property);
        PreviousPageCommand?.Notify(); NextPageCommand?.Notify();
        Notify();
        LastRefreshMilliseconds=refreshWatch.Elapsed.TotalMilliseconds;MaxRefreshMilliseconds=Math.Max(MaxRefreshMilliseconds,LastRefreshMilliseconds);RefreshCount++;
        OnPropertyChanged(nameof(LastRefreshMilliseconds));OnPropertyChanged(nameof(MaxRefreshMilliseconds));OnPropertyChanged(nameof(RefreshCount));
    }
    public void TrimTransientImages(IEnumerable<WallpaperItemViewModel> active)
    {
        var protectedItems = VisibleItems.Concat(RelatedItems).Concat(RecentItems).Concat(active).ToHashSet();
        if (_selected is not null) protectedItems.Add(_selected);
        foreach (var item in _items.Where(i => i.HasCachedThumbnail).OrderByDescending(i => i.ThumbnailLastUsed).Skip(72))
            if (!protectedItems.Contains(item)) item.ReleaseThumbnail();
    }
    private WallpaperItemViewModel CreateItem(WallpaperAsset asset)
    {
        var item = new WallpaperItemViewModel(asset, _catalog.ThumbnailPath(asset.Id));
        item.PropertyChanged += OnItemChanged;
        return item;
    }
    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WallpaperItemViewModel.IsSelected)) return;
        OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(HasSelection));
        BatchUpdateCommand?.Notify(); BatchRemoveCommand?.Notify(); ClearSelectionCommand?.Notify();
    }
    public void LoadMore()
    {
        if (!IsGallery || !HasMoreItems) return;
        _loadedScreens = Math.Min(3, _loadedScreens + 1);
        Refresh();
    }
    private void ResetLoading() { _loadedScreens = 1; _currentPage = 1; _scrollOffset = 0; RequestScrollRestore(0); }
    private bool MatchesPrimaryCategory(WallpaperItemViewModel item) =>
        (_tab != "动态壁纸" || item.Asset.IsVideo) && (_tab != "静态壁纸" || !item.Asset.IsVideo)
        && (_category == "全部" || item.Category == _category);
    private Task RunAsync(Func<Task> action, bool initialize = false)
    {
        if (_closing || _busy || (!initialize && !CanEdit)) return Task.CompletedTask;
        _busy = true; Notify(); _pending = ExecuteAsync(action); return _pending;
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { _logger.Error(exception, "Wallpaper library operation failed"); Status = OutputTroubleshooting.DescribeFailure("资源操作",exception.Message);_main.Output.Report(Status,true); }
        finally { _busy = false; Notify(); }
    }
    private void QueueSave() { if (_initialized) { SyncPreset(); _saveTail = SaveAfterAsync(_saveTail); } }
    private async Task SaveAfterAsync(Task previous)
    {
        try { await previous; if (!await _catalog.SaveAsync(_document)) Status = "资源库保存失败，请检查日志。"; }
        catch (Exception exception) { _logger.Error(exception, "Catalog persistence failed"); Status = "资源库保存失败。"; }
    }
    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rotating && e.PropertyName == nameof(WorkspaceViewModel.OutputStatus) && _main.Workspace!.OutputStatus.Contains("已停止") && !_busy) StopRotation();
        Notify();
    }
    private void OnUsbChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rotating && UsbRotation && !_busy && _main.Usb?.IsRunning != true) StopRotation();
        Notify();
    }
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Scene) && _rotating && !_busy) StopRotation();
        if (e.PropertyName == nameof(MainViewModel.SelectedNavigation))
        { OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsDetails)); OnPropertyChanged(nameof(ShowDemoGallery)); }
        if (e.PropertyName == nameof(MainViewModel.Scene))
        { OnPropertyChanged(nameof(SelectedIsLive)); OnPropertyChanged(nameof(ShowTransport)); NotifyTransport(); }
    }
    private void StopUnusedPreview()
    {
        if (!_rotating && _main.Scene is VideoScene && _main.Workspace?.IsPreviewApplied != true) _main.Videos?.StopPlayback();
    }
    private void OnTransportTick(object? sender, EventArgs e) { if (IsDetails && ShowTransport) NotifyTransport(); if (_rotating) { OnPropertyChanged(nameof(CarouselCountdown)); OnPropertyChanged(nameof(CarouselPlaybackStatus)); } }
    private void NotifyTransport() { OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(Position)); OnPropertyChanged(nameof(PositionLabel)); OnPropertyChanged(nameof(PauseLabel)); PauseCommand.Notify(); }
    private void Notify()
    {
        NotifyCarousel();
        foreach (var property in new[] { nameof(CanEdit), nameof(CanPreviewSelected), nameof(CanApplySelected), nameof(CanApplyUsbSelected), nameof(CanChangeRotationTarget), nameof(IsRotating), nameof(RotationLabel), nameof(SelectedIsLive), nameof(ShowTransport), nameof(SelectedIsVideo), nameof(SelectedIsImage) }) OnPropertyChanged(property);
        ImportCommand?.NotifyCanExecuteChanged(); OpenDetailCommand?.Notify(); PreviewCommand?.NotifyCanExecuteChanged(); ApplyCommand?.NotifyCanExecuteChanged();
        ApplyUsbCommand?.NotifyCanExecuteChanged();
        ApplyDashboardUsbCommand?.NotifyCanExecuteChanged();
        ApplyCardUsbCommand?.Notify();
        RotationCommand?.NotifyCanExecuteChanged(); NextCommand?.NotifyCanExecuteChanged(); PreviousCommand?.NotifyCanExecuteChanged(); SaveMetadataCommand?.Notify(); RemoveCommand?.Notify();
        MoveUpCommand?.Notify(); MoveDownCommand?.Notify();
        BatchUpdateCommand?.Notify(); BatchRemoveCommand?.Notify(); ClearSelectionCommand?.Notify();
    }
    public async Task PrepareForCloseAsync()
    {
        _closing = true; StopRotation(); _transport.Stop(); _lifetime.Cancel();
        await _pending; await _saveTail;
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _closing = true;
        _rotation.Stop(); _rotation.Tick -= OnRotationTick; _transport.Stop(); _transport.Tick -= OnTransportTick;
        _lifetime.Cancel(); _lifetime.Dispose();
        _main.Workspace!.PropertyChanged -= OnWorkspaceChanged; _main.PropertyChanged -= OnMainChanged;
        if (_main.Usb is not null) _main.Usb.PropertyChanged -= OnUsbChanged;
        foreach (var item in _items) { item.PropertyChanged -= OnItemChanged; item.ReleaseThumbnail(); }
    }
    private static bool SamePath(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    private sealed record GalleryState(string Category, string Subcategory, string Search, bool PlaylistOnly, int Page, int LoadedScreens, bool Details, Guid? SelectedId, double ScrollOffset);
}
