using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public enum OnlineResourceKind { Wallpaper, Clock, Plugin, Theme }
public sealed record OnlineResourceMenuItem(OnlineResourceKind Kind, string Name, string Glyph, string Availability);

public sealed class CloudLibraryViewModel : ObservableObject, IDisposable
{
    private OnlineResourceKind _resourceKind;
    private string _layoutMode = "舒适";
    private int _viewportColumns;
    public IReadOnlyList<OnlineResourceMenuItem> ResourceKinds { get; } = [new(OnlineResourceKind.Wallpaper, "壁纸", "\uEB9F", ""), new(OnlineResourceKind.Clock, "时钟", "\uE823", "暂未开放"), new(OnlineResourceKind.Plugin, "插件", "\uEA86", "暂未开放"), new(OnlineResourceKind.Theme, "主题", "\uE790", "暂未开放")];
    public OnlineResourceKind ResourceKind { get => _resourceKind; set { if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _resourceKind, value)) return; _refreshRequest?.Cancel(); foreach (var property in new[] { nameof(IsCatalogType), nameof(ShowTypeUnavailable), nameof(ResourceKindName), nameof(UnavailableMessage), nameof(SearchPlaceholder), nameof(IsGallery), nameof(IsDetails) }) OnPropertyChanged(property); Notify(); if (IsCatalogType) RequestRefresh(); } }
    public bool IsCatalogType => ResourceKind == OnlineResourceKind.Wallpaper;
    public bool ShowTypeUnavailable => !IsCatalogType;
    public string ResourceKindName => ResourceKinds.First(item => item.Kind == ResourceKind).Name;
    public string SearchPlaceholder => "搜索" + ResourceKindName;
    public string UnavailableMessage => $"在线{ResourceKindName}尚未开放";
    public IReadOnlyList<string> LayoutModes { get; } = ["舒适", "紧凑"];
    public string LayoutMode { get => _layoutMode; set { if (_disposed || !LayoutModes.Contains(value) || !SetProperty(ref _layoutMode, value)) return; _viewportWidth = 0; _page = 1; ResetScroll(); OnPropertyChanged(nameof(ResourceColumns)); OnPropertyChanged(nameof(LayoutLabel)); RequestRefresh(); } }
    public string LayoutLabel => $"{LayoutMode}布局 · {ResourceColumns}列";
    public ActionCommand SelectResourceKindCommand { get; }
    private readonly MainViewModel _main;
    private readonly ISettingsService _settings;
    private readonly StudioApiClient _client;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _refreshRequest;
    private CancellationTokenSource? _downloadRequest;
    private string _serverUrl, _status = "连接 D92 Studio 后可浏览官方免费壁纸。";
    private bool _busy, _initialized, _disposed, _details, _loadFailed, _refreshPending;
    private double _scrollOffset;
    private double _viewportWidth, _viewportHeight;
    private bool _viewportPortrait;
    private int _screenItems = 8, _loadedScreens = 1;
    private string _scope = "全部资源";
    public IReadOnlyList<string> Scopes { get; } = ["全部资源", "云端收藏", "已授权资源"];
    public string Scope { get => _scope; set { if (SetProperty(ref _scope, value)) { _page = 1; ResetScroll(); RequestRefresh(); } } }
    private int PageSize => Math.Min(96, _screenItems * 3);
    public IReadOnlyList<StudioResource> VisibleItems => Items.Take(_screenItems * _loadedScreens).ToArray();
    public bool ShowPagination => Items.Count > 0 && (_loadedScreens >= 3 || VisibleItems.Count >= Items.Count);
    private StudioResource? _selected;
    private string _search = "";
    private StudioCategory? _selectedCategory;
    private StudioCategory? _selectedPrimaryCategory;
    private StudioTag? _selectedTag;
    private int _page = 1, _total;
    private int _downloadProgress;
    private bool _isDownloading;
    private bool _downloadFailed;
    public ObservableCollection<DownloadTaskViewModel> DownloadTasks { get; } = [];
    public bool HasDownloadTasks => DownloadTasks.Count > 0;
    public string DownloadSummary => $"下载管理 · 进行中 {DownloadTasks.Count(t => t.IsRunning)} · 完成 {DownloadTasks.Count(t => t.State == DownloadTaskState.Completed)} · 待重试 {DownloadTasks.Count(t => t.CanRetry)}";
    public ActionCommand ClearFinishedTasksCommand { get; }
    public ActionCommand RetryTaskCommand { get; }
    private string _sortMode = "推荐";
    private string _displayOrientation = "横屏超宽";
    private IReadOnlyList<StudioResource> _sourceItems = [];
    public IReadOnlyList<StudioResource> Items { get; private set; } = [];
    public IReadOnlyList<StudioCategory> Categories { get; private set; } = [];
    public IReadOnlyList<StudioTag> Tags { get; private set; } = [];
    public IReadOnlyList<string> SortModes { get; } = ["推荐", "新品"];
    public IReadOnlyList<string> DisplayOrientations { get; } = ["横屏超宽", "竖屏超窄"];
    public IReadOnlyList<string> WallpaperKinds { get; } = ["动态", "静态"];
    public string SortMode { get => _sortMode; set { if (SetProperty(ref _sortMode, value)) { _page = 1; ResetScroll(); RequestRefresh(); } } }
    public string DisplayOrientation { get => _displayOrientation; set { if (SetProperty(ref _displayOrientation, value)) { _page = 1; ResetScroll(); OnPropertyChanged(nameof(IsPortraitMode)); OnPropertyChanged(nameof(IsLandscapeMode)); OnPropertyChanged(nameof(ResourceColumns)); OnPropertyChanged(nameof(LayoutLabel)); RequestRefresh(); } } }
    public bool IsPortraitMode => _displayOrientation == "竖屏超窄";
    public bool IsLandscapeMode => !IsPortraitMode;
    public int ResourceColumns => IsPortraitMode ? (LayoutMode == "紧凑" ? 8 : 6) : (LayoutMode == "紧凑" ? 3 : 2);
    public StudioResource? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) { _downloadFailed = false; Notify(); } } }
    public string ServerUrl { get => _serverUrl; set => SetProperty(ref _serverUrl, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Search { get => _search; set => SetProperty(ref _search, value); }
    public IReadOnlyList<StudioCategory> PrimaryCategories => Categories.Where(category => category.ParentId is null).ToArray();
    public IReadOnlyList<StudioCategory> SecondaryCategories => _selectedPrimaryCategory is null ? [] : Categories.Where(category => category.ParentId == _selectedPrimaryCategory.Id).ToArray();
    public StudioCategory? SelectedPrimaryCategory
    {
        get => _selectedPrimaryCategory;
        set
        {
            if (!SetProperty(ref _selectedPrimaryCategory, value)) return;
            _selectedCategory = null; OnPropertyChanged(nameof(SelectedCategory)); OnPropertyChanged(nameof(SecondaryCategories)); OnPropertyChanged(nameof(HasSecondaryCategories));
            _page = 1; ResetScroll(); RequestRefresh();
        }
    }
    public StudioCategory? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) { _page = 1; ResetScroll(); RequestRefresh(); } } }
    public StudioTag? SelectedTag { get => _selectedTag; set { if (SetProperty(ref _selectedTag, value)) { _page = 1; ResetScroll(); RequestRefresh(); } } }
    public string PageStatus => $"第 {_page} 页 · 共 {_total} 项";
    public bool CanPrevious => IsCatalogType && !_busy && _page > 1;
    public bool CanNext => IsCatalogType && !_busy && _page * PageSize < _total;
    public bool IsBusy => _busy;
    public bool IsEmpty => _initialized && Items.Count == 0;
    public bool ShowDemoItems => Items.Count == 0 && !_busy && _loadFailed && _scope == "全部资源";
    public bool ShowEmptyState => IsEmpty && !_busy && !_loadFailed;
    public string EmptyMessage => _scope == "云端收藏" ? "当前筛选下没有云端收藏。" : _scope == "已授权资源" ? "当前筛选下没有已授权资源。" : "没有符合条件的资源，请调整分类或搜索。";
    public bool LoadFailed => _loadFailed;
    public bool HasSecondaryCategories => SecondaryCategories.Count > 0;
    public bool ShowDemoCategories => Categories.Count == 0 && !_busy;
    public bool IsGallery => !_details && IsCatalogType;
    public bool IsDetails => _details && IsCatalogType;
    public bool CanDownload => IsCatalogType && !_busy && _selected?.IsCompatible == true && _selected.HasDownloadEntitlement;
    public int DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }
    public bool IsDownloading { get => _isDownloading; private set => SetProperty(ref _isDownloading, value); }
    public string DownloadProgressLabel => IsDownloading ? $"正在下载 {DownloadProgress}%" : "";
    public bool CanApplyUsb => CanDownload && _main.Usb?.CanApply == true;
    public bool CanFavorite => IsCatalogType && !_busy && _selected is not null && _client.HasSession(ServerUrl);
    public string FavoriteAction => _selected?.IsFavorite == true ? "取消收藏" : "收藏";
    public string DownloadAction => _downloadFailed ? "重试下载" : _selected?.HasUpdate == true ? "更新到最新版本" : _selected?.IsDownloaded == true ? "重新下载 / 校验" : "下载到我的资源";
    public int AvailableUpdateCount => IsCatalogType ? Items.Count(item => item.HasUpdate && item.IsCompatible && item.HasDownloadEntitlement) : 0;
    public string BatchUpdateLabel => $"更新当前页资源（{AvailableUpdateCount}）";
    public ActionCommand CancelDownloadCommand { get; }
    public AsyncCommand BatchUpdateCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand SaveServerCommand { get; }
    public AsyncCommand DownloadCommand { get; }
    public AsyncCommand ApplyUsbCommand { get; }
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand PreviousCommand { get; }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand FavoriteCommand { get; }
    public ActionCommand OpenDetailCommand { get; }
    public ActionCommand BackCommand { get; }
    public ActionCommand ClearPrimaryCategoryCommand { get; }
    public ActionCommand ClearSecondaryCategoryCommand { get; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    public event Action<double>? ScrollRestoreRequested;
    public CloudLibraryViewModel(MainViewModel main, ISettingsService settings, AppSettings initial, ILogger logger, StudioApiClient? client = null)
    {
        _main = main; _settings = settings; _logger = logger; _client = client ?? new StudioApiClient(logger); _serverUrl = initial.StudioServerUrl;
        SelectResourceKindCommand = new(value => { if (value is OnlineResourceKind kind) ResourceKind = kind; });
        RefreshCommand = new AsyncCommand(() => RunAsync(RefreshAsync), () => IsCatalogType && !_busy, logger);
        SaveServerCommand = new AsyncCommand(() => RunAsync(SaveAndRefreshAsync), () => !_busy && !string.IsNullOrWhiteSpace(ServerUrl), logger);
        DownloadCommand = new AsyncCommand(() => RunAsync(() => DownloadAsync(false)), () => CanDownload, logger);
        ApplyUsbCommand = new AsyncCommand(() => RunAsync(() => DownloadAsync(true)), () => CanApplyUsb, logger);
        SearchCommand = new AsyncCommand(() => { _details = false; OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsDetails)); _page = 1; ResetScroll(); RequestRefresh(); return PendingOperation; }, () => IsCatalogType && !_disposed && !_isDownloading, logger);
        PreviousCommand = new AsyncCommand(() => { _page--; ResetScroll(); return RunAsync(RefreshAsync); }, () => CanPrevious, logger);
        NextCommand = new AsyncCommand(() => { _page++; ResetScroll(); return RunAsync(RefreshAsync); }, () => CanNext, logger);
        FavoriteCommand = new AsyncCommand(() => RunAsync(ToggleFavoriteAsync), () => CanFavorite, logger);
        CancelDownloadCommand = new(_ => _downloadRequest?.Cancel());
        ClearFinishedTasksCommand = new(_ => { foreach (var task in DownloadTasks.Where(t => !t.IsRunning).ToArray()) DownloadTasks.Remove(task); NotifyDownloads(); });
        RetryTaskCommand = new(value => { if (value is DownloadTaskViewModel task && task.CanRetry && !_busy && task.ServerUrl == ServerUrl) _ = RunAsync(() => DownloadItemAsync(task.Resource, false, task)); });
        BatchUpdateCommand = new(() => RunAsync(UpdatePageAsync), () => !_busy && AvailableUpdateCount > 0, logger);
        OpenDetailCommand = new ActionCommand(value =>
        {
            if (value is not StudioResource item) return;
            ResourceKind = OnlineResourceKind.Wallpaper;
            Selected = item; _details = true; OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsDetails));
        });
        BackCommand = new ActionCommand(_ => { _details = false; OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsDetails)); ScrollRestoreRequested?.Invoke(_scrollOffset); });
        ClearPrimaryCategoryCommand = new ActionCommand(_ =>
        {
            _selectedPrimaryCategory = null; _selectedCategory = null; OnPropertyChanged(nameof(SelectedPrimaryCategory)); OnPropertyChanged(nameof(SelectedCategory)); OnPropertyChanged(nameof(SecondaryCategories)); OnPropertyChanged(nameof(HasSecondaryCategories));
            _page = 1; ResetScroll(); RequestRefresh();
        });
        ClearSecondaryCategoryCommand = new ActionCommand(_ => SelectedCategory = null);
        _main.PropertyChanged += OnMainChanged;
    }
    public Task InitializeAsync() => RunAsync(RefreshAsync);
    public Task RefreshAfterAccountChangeAsync()
    {
        foreach (var item in Items) { item.IsEntitled = false; item.IsFavorite = false; }
        _main.Library?.SetCloudOwnedResources([]);
        OnPropertyChanged(nameof(Selected)); Notify();
        RequestRefresh();
        return PendingOperation;
    }
    private Task RunAsync(Func<Task> action)
    {
        if (_busy || _disposed) return Task.CompletedTask;
        _busy = true; Notify(); PendingOperation = ExecuteAsync(action); return PendingOperation;
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { if (_isDownloading) Status = "下载已取消，可重新下载。"; }
        catch (Exception exception) { if (!_isDownloading) _loadFailed = true; _downloadFailed = _isDownloading; _logger.Warning(exception, "D92 Studio operation failed"); Status = exception is HttpRequestException http && http.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "请先在个人中心登录，再查看收藏和授权资源。" : _isDownloading ? "下载失败，请检查网络或资源权限后重试。" : "暂时无法连接服务器，请刷新重试。"; }
        finally
        {
            var refreshAgain = _refreshPending && !_disposed;
            _refreshPending = false; _busy = false;
            if (_isDownloading) { IsDownloading = false; OnPropertyChanged(nameof(DownloadProgressLabel)); }
            Notify(); NotifyDownloads();
            if (refreshAgain) await RunAsync(RefreshAsync);
        }
    }
    private async Task RefreshAsync()
    {
        if (!IsCatalogType) return;
        Status = "正在读取在线资源…";
        _loadFailed = false;
        _refreshRequest?.Dispose();
        _refreshRequest = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _refreshRequest.Token;
        // Keep results staged until all requests complete; cancelled requests cannot publish metadata.
        var server = ServerUrl;
        var categoryId = SelectedCategory?.Id ?? SelectedPrimaryCategory?.Id;
        var page = await _client.GetResourcesAsync(server, Search, categoryId, SelectedTag?.Id, _page, token,
            IsPortraitMode ? "portrait" : "landscape", _sortMode == "新品" ? "newest" : "recommended", PageSize,
            _scope == "云端收藏" ? "favorites" : _scope == "已授权资源" ? "owned" : "all");
        var options = Categories.Count == 0 && Tags.Count == 0 ? await _client.GetCatalogOptionsAsync(server, token) : null;
        var userLibrary = await _client.GetUserLibraryAsync(server, token);
        token.ThrowIfCancellationRequested();
        if (options is not null) { Categories = options.Categories; Tags = options.Tags; foreach (var property in new[] { nameof(Tags), nameof(Categories), nameof(PrimaryCategories), nameof(SecondaryCategories), nameof(ShowDemoCategories) }) OnPropertyChanged(property); }
        _sourceItems = page.Items; Items = _sourceItems; _total = page.Total; _initialized = true;
        _main.Library?.SetCloudOwnedResources(userLibrary?.ResourceIds ?? []);
        if (userLibrary is not null)
        {
            var entitled = userLibrary.ResourceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var favorites = userLibrary.FavoriteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items)
            {
                item.IsEntitled = entitled.Contains(item.Id) || userLibrary.HasVip && item.AccessPolicy is "VIP" or "PURCHASE_OR_VIP";
                item.IsFavorite = favorites.Contains(item.Id);
            }
        }
        ApplySort(false);
        foreach (var item in Items) { var localVersion = _main.Library?.GetCloudVersion(item.Id); item.IsDownloaded = localVersion is not null; item.HasUpdate = localVersion is not null && localVersion != item.Version; }
        if (_selected is not null)
        {
            _selected = Items.FirstOrDefault(item => item.Id == _selected.Id);
            if (_selected is null) _details = false;
        }
        OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(VisibleItems)); OnPropertyChanged(nameof(ShowPagination)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(Selected)); OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsDetails)); OnPropertyChanged(nameof(HasSecondaryCategories));
        Status = Items.Count == 0 ? "服务器暂时没有符合条件的已发布静态壁纸。" : $"已加载 {Items.Count} 份在线资源。";
    }
    private void ApplySort(bool notify = true)
    {
        Items = _sortMode == "新品" ? _sourceItems.OrderByDescending(item => item.UpdatedAt).ToArray() : _sourceItems;
        if (notify) OnPropertyChanged(nameof(Items));
    }
    private async Task SaveAndRefreshAsync()
    {
        var value = ServerUrl.Trim().TrimEnd('/');
        if (!await _settings.UpdateAsync(settings => settings.StudioServerUrl = value)) throw new IOException("服务器地址保存失败");
        ServerUrl = value;
        Categories = []; Tags = []; _selectedCategory = null; _selectedPrimaryCategory = null; _selectedTag = null;
        _selected = null; _details = false; _sourceItems = []; Items = []; _page = 1; _scope = "全部资源"; _search = ""; ResetScroll();
        foreach (var property in new[] { nameof(Categories), nameof(Tags), nameof(SelectedCategory), nameof(SelectedPrimaryCategory), nameof(SelectedTag), nameof(PrimaryCategories), nameof(SecondaryCategories), nameof(Selected), nameof(Scope), nameof(Search), nameof(IsGallery), nameof(IsDetails) }) OnPropertyChanged(property);
        await RefreshAsync();
    }
    private async Task DownloadAsync(bool applyUsb)
    {
        var item = _selected; if (item is null) return;
        await DownloadItemAsync(item, applyUsb);
    }
    private async Task DownloadItemAsync(StudioResource item, bool applyUsb, DownloadTaskViewModel? retry = null)
    {
        if (!item.IsCompatible) { Status = item.CompatibilityMessage; return; }
        if (!item.HasDownloadEntitlement) { Status = item.AccessDescription; return; }
        var task = retry ?? new DownloadTaskViewModel(item, ServerUrl);
        task.State = DownloadTaskState.Running; task.Progress = 0; task.Message = "下载并校验中";
        if (retry is null) DownloadTasks.Insert(0, task);
        while (DownloadTasks.Count > 50) DownloadTasks.RemoveAt(DownloadTasks.Count - 1);
        NotifyDownloads();
        try
        {
        IsDownloading = true; DownloadProgress = 0; OnPropertyChanged(nameof(DownloadProgressLabel));
        _downloadFailed = false;
        OnPropertyChanged(nameof(IsDownloading));
        _downloadRequest?.Dispose(); _downloadRequest = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        Status = $"正在下载并校验：{item.Name}";
        var progress = new Progress<StudioDownloadProgress>(value => { if (!task.IsRunning) return; task.Progress = value.Percentage; DownloadProgress = value.Percentage; OnPropertyChanged(nameof(DownloadProgressLabel)); });
        var path = await _client.DownloadAsync(ServerUrl, item, progress, _downloadRequest.Token);
        _downloadRequest.Token.ThrowIfCancellationRequested();
        task.Message = "已校验，正在导入资源库";
        var category = item.Categories.FirstOrDefault(category => category.ParentId is not null)?.Name ?? item.Categories.FirstOrDefault()?.Name ?? "官方";
        await _main.Library!.ImportCloudAsync(path, item.Name, item.Id, item.Version, category, string.Join(' ', item.Tags.Select(tag => tag.Name)), applyUsb);
        if (_main.Library.GetCloudVersion(item.Id) != item.Version) throw new IOException("本地资源库尚未完成导入，请重试");
        DownloadProgress = 100; IsDownloading = false; OnPropertyChanged(nameof(DownloadProgressLabel));
        item.IsDownloaded = true; item.HasUpdate = false; OnPropertyChanged(nameof(Selected)); OnPropertyChanged(nameof(VisibleItems)); Notify();
        task.Progress = 100; task.State = DownloadTaskState.Completed; task.Message = "已下载并加入我的资源库";
        Status = applyUsb ? _main.Library.Status : $"已下载：{item.Name}；已加入“我的资源库”，可离线使用。";
        }
        catch (OperationCanceledException) { task.State = DownloadTaskState.Cancelled; task.Message = "已取消，可重试"; throw; }
        catch (Exception exception) { task.State = DownloadTaskState.Failed; task.Message = exception is HttpRequestException http && http.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ? "登录失效或没有下载权限" : exception is InvalidDataException ? "资源校验失败" : "网络或本地导入失败"; throw; }
    }
    private void NotifyDownloads() { OnPropertyChanged(nameof(HasDownloadTasks)); OnPropertyChanged(nameof(DownloadSummary)); }
    private async Task UpdatePageAsync()
    {
        var updates = Items.Where(item => item.HasUpdate && item.IsCompatible && item.HasDownloadEntitlement).ToArray();
        var success = 0; var failed = 0; var cancelled = false;
        foreach (var item in updates)
        {
            try { await DownloadItemAsync(item, false); success++; }
            catch (OperationCanceledException) { cancelled = true; break; }
            catch (Exception exception) { failed++; _logger.Warning(exception, "Batch cloud update failed for {ResourceId}", item.Id); }
        }
        Status = $"当前页更新：成功 {success}，失败 {failed}，未完成 {updates.Length - success - failed}" + (cancelled ? "（已取消）" : "");
    }
    private async Task ToggleFavoriteAsync()
    {
        if (_selected is null) return;
        var favorite = !_selected.IsFavorite;
        await _client.SetFavoriteAsync(ServerUrl, _selected.Id, favorite, _lifetime.Token);
        _selected.IsFavorite = favorite;
        OnPropertyChanged(nameof(Selected)); OnPropertyChanged(nameof(FavoriteAction));
        Status = favorite ? $"已收藏：{_selected.Name}" : $"已取消收藏：{_selected.Name}";
    }
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNavigation) && _main.SelectedNavigation == "在线资源")
        {
            if (!_initialized && !_busy) _ = RunAsync(RefreshAsync);
            else ScrollRestoreRequested?.Invoke(_scrollOffset);
        }
    }
    private void RequestRefresh()
    {
        _refreshRequest?.Cancel();
        if (_busy) { _refreshPending = true; return; }
        _ = RunAsync(RefreshAsync);
    }
    private void ResetScroll() { _loadedScreens = 1; _scrollOffset = 0; OnPropertyChanged(nameof(VisibleItems)); OnPropertyChanged(nameof(ShowPagination)); ScrollRestoreRequested?.Invoke(0); }
    public void UpdateViewport(double width, double height, double offset, double scrollableHeight)
    {
        if (width <= 0 || height <= 0) return;
        var cardWidth = Math.Max(40, width / ResourceColumns - 16);
        var cardHeight = cardWidth / (IsPortraitMode ? .25 : 4) + 48;
        var count = Math.Clamp(ResourceColumns * Math.Max(1, (int)Math.Ceiling(height / cardHeight)), ResourceColumns, 32);
        var layoutChanged = _viewportWidth == 0 || Math.Abs(width - _viewportWidth) >= 32 || Math.Abs(height - _viewportHeight) >= 32 || _viewportPortrait != IsPortraitMode || _viewportColumns != ResourceColumns;
        if (layoutChanged && !IsBusy)
        {
            _viewportWidth = width; _viewportHeight = height; _viewportPortrait = IsPortraitMode; _viewportColumns = ResourceColumns;
            if (count != _screenItems) { _screenItems = count; _page = 1; ResetScroll(); RequestRefresh(); return; }
        }
        if (!IsBusy && offset >= scrollableHeight - 48 && _loadedScreens < 3 && VisibleItems.Count < Items.Count)
        { _loadedScreens++; OnPropertyChanged(nameof(VisibleItems)); OnPropertyChanged(nameof(ShowPagination)); }
    }
    public void UpdateScrollOffset(double offset) => _scrollOffset = Math.Max(0, offset);
    private void Notify()
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(CanApplyUsb)); OnPropertyChanged(nameof(CanFavorite)); OnPropertyChanged(nameof(FavoriteAction)); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(DownloadProgressLabel)); OnPropertyChanged(nameof(ShowDemoItems)); OnPropertyChanged(nameof(LoadFailed)); OnPropertyChanged(nameof(ShowDemoCategories)); OnPropertyChanged(nameof(HasSecondaryCategories));
        RefreshCommand.NotifyCanExecuteChanged(); SaveServerCommand.NotifyCanExecuteChanged(); DownloadCommand.NotifyCanExecuteChanged(); ApplyUsbCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanPrevious)); OnPropertyChanged(nameof(CanNext));
        FavoriteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(DownloadAction)); OnPropertyChanged(nameof(AvailableUpdateCount)); OnPropertyChanged(nameof(BatchUpdateLabel)); BatchUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState)); OnPropertyChanged(nameof(EmptyMessage));
    }
    public Task PrepareForCloseAsync() { _downloadRequest?.Cancel(); _refreshRequest?.Cancel(); _lifetime.Cancel(); return PendingOperation; }
    public void Dispose() { if (_disposed) return; _disposed = true; _main.PropertyChanged -= OnMainChanged; _refreshRequest?.Cancel(); _refreshRequest?.Dispose(); _lifetime.Cancel(); _lifetime.Dispose(); _client.Dispose(); }
}
