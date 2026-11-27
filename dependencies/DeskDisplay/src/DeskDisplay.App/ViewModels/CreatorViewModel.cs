using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using DeskDisplay.Library;
using Microsoft.Win32;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed record ThemeCard(ThemeProject Project, string CoverPath)
{
    public DateTimeOffset? LastUsedAt { get; init; }
    public bool Favorite { get; init; }
    public string FavoriteLabel => Favorite ? "♥ 已收藏" : "♡ 收藏";
    public string Name => Project.Name;
    public int LayoutCount => 2 + Project.LayoutVersions.Count;
    public string Details => $"{LayoutCount} 个布局 · {(Project.Orientation == ThemeOrientation.Portrait ? "竖屏" : "横屏")} · {Project.ModifiedAt.LocalDateTime:yyyy-MM-dd HH:mm}";
    public double Aspect => Project.Orientation == ThemeOrientation.Portrait ? 0.25 : 4;
    public Uri? CoverUri => File.Exists(CoverPath) ? new Uri(CoverPath) : null;
}

public sealed partial class CreatorViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly ThemeProjectStore _store;
    private readonly ILogger _logger;
    private readonly DispatcherTimer _clock;
    private readonly List<WindowsVideoPlaybackService> _players = [];
    private readonly Dictionary<string, ImageScene> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    public int ImageDecodeCount { get; private set; }
    private CancellationTokenSource _load = new();
    private Task _pending = Task.CompletedTask;
    private Task _assets = Task.CompletedTask;
    private ThemeEditSession _session = new(new ThemeProject());
    private Guid? _selectedId;
    private ThemeLayer? _clipboard;
    private bool _editing, _preview, _busy, _dirty, _disposed, _gesture, _gestureCheckpoint, _refreshingLayers;
    private string _status = "", _material = "图片";
    public ThemeScene Scene { get; }
    public ThemeProject Project => _session.Project;
    public IReadOnlyList<ThemeCard> Projects { get; private set; } = [];
    public IReadOnlyList<ThemeCard> RecentProjects => Projects.Take(4).ToArray();
    public bool HasProjects => Projects.Count > 0;
    public bool IsHome => !_editing && !_templatePicker && !_repairing;
    public bool IsEditor => _editing && !_preview && !_templatePicker && !_repairing;
    public bool IsPreview => _editing && _preview && !_templatePicker && !_repairing;
    public bool IsBusy => _busy;
    public bool CanEdit => !_busy && !_saving && !_disposed;
    public bool IsDirty => _dirty;
    public string ProjectName { get => Project.Name; set { if (value == Project.Name || string.IsNullOrWhiteSpace(value) || value.Length > 160) return; Change(() => Project.Name = value); } }
    public IReadOnlyList<ThemeOrientation> Orientations { get; } = Enum.GetValues<ThemeOrientation>();
    public ThemeOrientation Orientation { get => Project.Orientation; set { if (Project.Orientation == value || !CanEdit) return; LeavingEditorLayout?.Invoke(this, EventArgs.Empty); Change(() => { Project.SelectedLayoutId = null; Project.Orientation = value; }); SelectLayers([]); RefreshLayers(); Track(RestoreLayoutViewportAsync()); } }
    public string OrientationLabel => Orientation == ThemeOrientation.Portrait ? "竖屏 · 1:4" : "横屏 · 4:1";
    public double Aspect => Project.ActiveLayout.Width / Project.ActiveLayout.Height;
    public IReadOnlyList<ThemeLayer> Layers => Project.ActiveLayout.Layers.OrderByDescending(l => l.ZIndex).ToArray();
    public ThemeLayer? SelectedLayer { get => Project.ActiveLayout.Layers.FirstOrDefault(l => l.Id == _selectedId); set { if (_refreshingLayers && value is null || _selectedId == value?.Id && _selection.Count <= 1) return; _selection.Clear(); _selectedId = value?.Id; NotifySelection(); } }
    public bool HasSelection => SelectedLayer is not null;
    public bool IsText => IsSingleSelection && SelectedLayer?.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday;
    public bool IsLiteralText => IsSingleSelection && SelectedLayer?.Kind == ThemeElementKind.Text;
    public bool ShowColor => IsText || SelectedLayer?.Kind is ThemeElementKind.Rectangle or ThemeElementKind.Ellipse;
    public bool IsClock => IsSingleSelection && SelectedLayer?.Kind == ThemeElementKind.DigitalClock;
    public bool IsImage => IsSingleSelection && SelectedLayer?.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper;
    public IReadOnlyList<WallpaperItemViewModel> LibraryAssets => _main.Library?.AllItems ?? [];
    public IReadOnlyList<string> MaterialTabs { get; } = ["图片", "时钟", "插件", "文字", "形状"];
    public string MaterialTab { get => _material; set { if (SetProperty(ref _material, value)) foreach (var p in new[] { nameof(ShowAssets), nameof(ShowClocks), nameof(ShowPlugins), nameof(ShowText), nameof(ShowShapes) }) OnPropertyChanged(p); } }
    public bool ShowAssets => MaterialTab is "图片" or "壁纸" or "我的素材";
    public bool ShowClocks => MaterialTab == "时钟";
    public bool ShowPlugins => MaterialTab == "插件";
    public bool ShowText => MaterialTab == "文字";
    public bool ShowShapes => MaterialTab == "形状";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Capability => (Orientation == ThemeOrientation.Portrait ? "竖屏布局按设备已保存参数映射宽高与旋转，保持编码画面尺寸；不会覆盖设备配置。" : "") + (Scene.ContainsVideo ? "视频背景与实时组件可合成输出；每个布局支持一个可见视频，实际帧率取决于图层与设备吞吐。" : "静态与实时组件使用现有 Windows HID 输出链路。");
    public string DeviceStatus => _main.Usb?.IsSelectedConnected == true ? "D92 已连接" : "D92 未连接";
    public UsbOutputViewModel? Usb => _main.Usb;
    public bool IsUsbTarget => OutputTarget == "D92 USB";
    public string OutputCheck => Project.ActiveLayout.Layers.Count(l => l.Visible && l.IsVideo) > 1 ? "不能应用：每个布局目前支持一个可见视频图层。" : Project.ActiveLayout.Layers.Any(l => l.Visible && (l.Kind is ThemeElementKind.Wallpaper or ThemeElementKind.Image or ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock) && !Scene.Children.ContainsKey(l.Id)) ? "不能应用：有素材丢失、加载失败或插件未启用。" : "图层检查通过。输出前请核对设备、方向和画布设置。";
    private string _outputTarget = "D92 USB";
    public string OutputTarget { get => _outputTarget; set { if (SetProperty(ref _outputTarget, value)) { OnPropertyChanged(nameof(IsUsbTarget)); OnPropertyChanged(nameof(OutputCheck)); } } }
    public IReadOnlyList<string> OutputTargets { get; } = ["D92 USB", "Windows 副屏"];
    public bool CanCopyCurrent => _main.Scene is ThemeScene;
    public bool UsesScene(Scene child) => _templatePicker && _main.SelectedPage == AppPage.Creator && TemplateScene is not null && TemplateScene.Children.Values.Any(s => ReferenceEquals(s, child)) || ((_editing && _main.SelectedPage == AppPage.Creator) || ReferenceEquals(_main.Scene, Scene)) && Scene.Children.Values.Any(s => ReferenceEquals(s, child)) || _themePlayback?.UsesScene(child) == true;
    private static readonly Lazy<IReadOnlyList<string>> InstalledFonts = new(() => System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray());
    public IReadOnlyList<string> Fonts => InstalledFonts.Value;
    public IReadOnlyList<string> Formats { get; } = ["HH", "HH:mm"];
    public IReadOnlyList<string> Alignments { get; } = ["居左", "居中", "居右"];
    public IReadOnlyList<ImageDisplayMode> FillModes { get; } = Enum.GetValues<ImageDisplayMode>();
    public string LayerName { get => SelectedLayer?.Name ?? ""; set => Edit(l => l.Name = value); }
    public double X { get => SelectedLayer?.X ?? 0; set { if (double.IsFinite(value) && Math.Abs(value) <= 32768) Edit(l => l.X = value); } }
    public double Y { get => SelectedLayer?.Y ?? 0; set { if (double.IsFinite(value) && Math.Abs(value) <= 32768) Edit(l => l.Y = value); } }
    public double Width { get => SelectedLayer?.Width ?? 1; set { if (double.IsFinite(value) && value is >= 1 and <= 16384) { if (LockAspect && SelectedLayer is { } l) SetConstrainedSize(value, value / l.Width * l.Height); else Edit(l => l.Width = value); } } }
    public double Height { get => SelectedLayer?.Height ?? 1; set { if (double.IsFinite(value) && value is >= 1 and <= 16384) { if (LockAspect && SelectedLayer is { } l) SetConstrainedSize(value / l.Height * l.Width, value); else Edit(l => l.Height = value); } } }
    public double Rotation { get => SelectedLayer?.Rotation ?? 0; set { if (double.IsFinite(value)) Edit(l => l.Rotation = value); } }
    public double Opacity { get => SelectedLayer?.Opacity ?? 1; set { if (double.IsFinite(value)) Edit(l => l.Opacity = Math.Clamp(value, 0, 1)); } }
    public bool Locked { get => SelectedLayer?.Locked ?? false; set { if (SelectedLayer is null || !CanEdit || SelectedLayer.Locked == value) return; Change(() => SelectedLayer.Locked = value); } }
    public bool Visible { get => SelectedLayer?.Visible ?? true; set { if (SelectedLayer is null || !CanEdit || SelectedLayer.Visible == value) return; Change(() => SelectedLayer.Visible = value); } }
    public string Text { get => SelectedLayer?.Text ?? ""; set { if (value.Length <= 8192) Edit(l => l.Text = value); } }
    public string Format { get => SelectedLayer?.Format ?? "HH:mm"; set { if (Formats.Contains(value)) Edit(l => l.Format = value); } }
    public bool Hour12 { get => SelectedLayer?.Hour12 ?? false; set => Edit(l => l.Hour12 = value); }
    public bool ShowSeconds { get => SelectedLayer?.ShowSeconds ?? false; set => Edit(l => { l.ShowSeconds = value; GrowClockBounds(l); }); }
    public string FontFamily { get => SelectedLayer?.FontFamily ?? "Consolas"; set { if (!string.IsNullOrWhiteSpace(value) && Fonts.Contains(value, StringComparer.OrdinalIgnoreCase)) Edit(l => l.FontFamily = value); else Status = "请选择已安装的字体。"; } }
    public double FontSize { get => SelectedLayer?.FontSize ?? 100; set { if (double.IsFinite(value) && value is >= 1 and <= 2000) Edit(l => l.FontSize = value); } }
    public bool Bold { get => SelectedLayer?.Bold ?? false; set => Edit(l => { l.Bold = value; l.FontWeightValue = value ? 700 : 400; }); }
    public string Color { get => SelectedLayer?.Color ?? "#FFFFFF"; set { try { _ = ColorConverter.ConvertFromString(value); Edit(l => l.Color = value); } catch (Exception e) when (e is FormatException or NotSupportedException) { Status = "请输入有效颜色，例如 #FFFFFF。"; } } }
    public string Alignment { get => SelectedLayer?.Alignment ?? "居中"; set { if (Alignments.Contains(value)) Edit(l => l.Alignment = value); } }
    public double Spacing { get => SelectedLayer?.Spacing ?? 0; set { if (double.IsFinite(value) && Math.Abs(value) <= 200) Edit(l => l.Spacing = value); } }
    public bool Shadow { get => SelectedLayer?.Shadow ?? false; set => Edit(l => l.Shadow = value); }
    public ImageDisplayMode Fill { get => SelectedLayer?.Fill ?? ImageDisplayMode.Uniform; set => Edit(l => l.Fill = value); }
    public double CropX { get => SelectedLayer?.CropX ?? 0.5; set { if (double.IsFinite(value)) Edit(l => l.CropX = Math.Clamp(value, 0, 1)); } }
    public double CropY { get => SelectedLayer?.CropY ?? 0.5; set { if (double.IsFinite(value)) Edit(l => l.CropY = Math.Clamp(value, 0, 1)); } }
    public double Brightness { get => SelectedLayer?.Brightness ?? 1; set { if (double.IsFinite(value)) Edit(l => l.Brightness = Math.Clamp(value, 0, 1)); } }
    public string AssetSource => SelectedLayer?.Path ?? "";
    public ActionCommand AddCommand { get; }
    public ActionCommand AddAssetCommand { get; }
    public ActionCommand UndoCommand { get; }
    public ActionCommand RedoCommand { get; }
    public ActionCommand CopyCommand { get; }
    public ActionCommand PasteCommand { get; }
    public ActionCommand DeleteCommand { get; }
    public ActionCommand LayerUpCommand { get; }
    public ActionCommand LayerDownCommand { get; }
    public AsyncCommand NewCommand { get; }
    public AsyncCommand TemplateCommand { get; }
    public AsyncCommand CopyCurrentCommand { get; }
    public ActionCommand OpenCommand { get; }
    public AsyncCommand ImportCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand BackCommand { get; }
    public ActionCommand PreviewCommand { get; }
    public ActionCommand EditCommand { get; }
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand ImportAssetCommand { get; }
    public ActionCommand DuplicateProjectCommand { get; }
    public ActionCommand DeleteProjectCommand { get; }
    public ActionCommand PluginSettingsCommand { get; }
    public Task PendingOperation => _pending;

    public CreatorViewModel(MainViewModel main, ILogger logger, ThemeProjectStore? store = null)
    {
        _main = main; _logger = logger; _store = store ?? new(); Scene = new(Project);
        InitializeExperience();
        InitializeEditingTools();
        InitializeAdvanced();
        InitializeFinishing();
        InitializeManagement();
        InitializeProductivity();
        InitializeNavigationTools();
        InitializeStudioTools();
        InitializeThemePlaylist();
        AddCommand = new(p => { if (Enum.TryParse<ThemeElementKind>(p?.ToString(), out var kind)) Add(kind); }, _ => CanEdit);
        AddAssetCommand = new(p => { if (p is WallpaperItemViewModel item) AddAsset(item.Asset); }, _ => CanEdit);
        UndoCommand = new(_ => { LeavingEditorLayout?.Invoke(this, EventArgs.Empty); var orientation = Orientation; var layoutId = Project.SelectedLayoutId; _session.Undo(); Changed(); Track(orientation != Orientation || layoutId != Project.SelectedLayoutId ? RestoreLayoutViewportAsync() : ReloadAssetsAsync()); }, _ => CanEdit && _session.CanUndo);
        RedoCommand = new(_ => { LeavingEditorLayout?.Invoke(this, EventArgs.Empty); var orientation = Orientation; var layoutId = Project.SelectedLayoutId; _session.Redo(); Changed(); Track(orientation != Orientation || layoutId != Project.SelectedLayoutId ? RestoreLayoutViewportAsync() : ReloadAssetsAsync()); }, _ => CanEdit && _session.CanRedo);
        CopyCommand = new(_ => CopySelection(), _ => SelectedLayers.Count > 0);
        PasteCommand = new(_ => PasteSelection(), _ => CanEdit && _clipboard is not null);
        DeleteCommand = new(_ => DeleteSelection(), _ => CanEdit && SelectedLayers.Any(l => !l.Locked));
        LayerUpCommand = new(_ => MoveSelected(1), _ => CanReorderSelected);
        LayerDownCommand = new(_ => MoveSelected(-1), _ => CanReorderSelected);
        NewCommand = new(() => RunAsync(async () => { await SaveIfDirtyAsync(); await OpenProjectAsync(new ThemeProject()); }), () => CanEdit, logger);
        TemplateCommand = new(() => RunAsync(async () => { await SaveIfDirtyAsync(); var project = new ThemeProject { Name = "数字时钟主题" }; project.Landscape.Layers.Add(new ThemeLayer { Kind = ThemeElementKind.DigitalClock, Name = "数字时钟", X = 480, Y = 100, Width = 960, Height = 240, FontSize = 180, ShowSeconds = true }); project.Portrait.Layers.Add(new ThemeLayer { Kind = ThemeElementKind.DigitalClock, Name = "数字时钟", X = 20, Y = 800, Width = 440, Height = 240, FontSize = 110 }); await OpenProjectAsync(project); _dirty = true; Changed(false); }), () => CanEdit, logger);
        CopyCurrentCommand = new(() => RunAsync(async () => { var copy = ((ThemeScene)_main.Scene).Project.Clone(); copy.Id = Guid.NewGuid(); copy.Name += " 副本"; await SaveIfDirtyAsync(); await OpenProjectAsync(copy); _dirty = true; Changed(false); }), () => CanEdit && CanCopyCurrent, logger);
        OpenCommand = new(p => { if (p is ThemeCard card) Track(RunAsync(async () => { await SaveIfDirtyAsync(); await OpenProjectAsync(card.Project.Clone()); })); }, _ => CanEdit);
        ImportCommand = new(() => RunAsync(async () => { var dialog = new OpenFileDialog { Filter = "主题项目或项目包|*.theme.json;*.d92theme", CheckFileExists = true }; if (dialog.ShowDialog() == true) await ImportProjectFileAsync(dialog.FileName); }), () => CanEdit, logger);
        SaveCommand = new(() => RunAsync(SaveAsync), () => CanEdit && _editing, logger);
        BackCommand = new(() => RunAsync(async () => { await SaveIfDirtyAsync(); _editing = false; _preview = false; NotifyPage(); await RefreshProjectsAsync(); }), () => CanEdit, logger);
        PreviewCommand = new(_ => { _preview = true; NotifyPage(); }, _ => CanEdit);
        EditCommand = new(_ => { _preview = false; NotifyPage(); }, _ => CanEdit);
        ApplyCommand = new(() => RunAsync(async () =>
        {
            await SaveAsync();
            var applicationCheck = CheckThemeApplication(); if (!applicationCheck.CanApply) { Status = string.Join("；", applicationCheck.Problems); return; }
            StopThemePlaylist(); _main.Library?.StopRotation();
            if (OutputCheck.StartsWith("不能应用", StringComparison.Ordinal)) { Status = OutputCheck; return; }
            if (OutputTarget == "D92 USB" && _main.Usb?.IsSelectedConnected != true) { Status = "请先在设备管理中选择并连接 D92。"; return; }
            _main.PreviewScene(Scene);
            if (OutputTarget == "D92 USB") { await _main.Usb!.ApplyCurrentAsync(); Status = _main.Usb.Status; }
            else { await _main.Devices.ApplySceneAsync(Scene); Status = _main.Devices.Status; }
            if (OutputTarget == "D92 USB" ? _main.Usb?.IsRunning == true || Scene.ContainsVideo && _main.Devices.Managed?.Cards.Any(c => c.IsActive && c.Device.Identity.SerialNumber == _main.Usb?.SelectedSerial) == true : _main.Devices.IsRenderOpen)
                await TryRecordThemeUseAsync(Project.Id);
        }), () => CanEdit, logger);
        ImportAssetCommand = new(() => RunAsync(async () => { var dialog = new OpenFileDialog { Filter = "壁纸|*.png;*.jpg;*.jpeg;*.mp4", Multiselect = true }; if (dialog.ShowDialog() == true && _main.Library is { } library) { await library.ImportAsync(dialog.FileNames); OnPropertyChanged(nameof(LibraryAssets)); NotifyMaterials(); } }), () => CanEdit, logger);
        DuplicateProjectCommand = new(p => { if (p is ThemeCard card) Track(RunAsync(() => DuplicateSavedProjectAsync(card))); }, _ => CanEdit);
        DeleteProjectCommand = new(p => { if (p is ThemeCard card && MessageBox.Show($"删除主题“{card.Name}”？仅删除项目、封面和草稿，不删除原始素材。", "删除主题", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Track(RunAsync(() => DeleteSavedProjectAsync(card.Project.Id))); }, _ => CanEdit);
        PluginSettingsCommand = new(_ => _main.SelectedPage = AppPage.Plugins);
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _clock.Tick += Tick; _clock.Start();
        _main.PropertyChanged += MainChanged;
        if (_main.Usb is { } usb) usb.PropertyChanged += DeviceChanged;
    }

    public Task InitializeAsync() => RunAsync(async () => { await RefreshProjectsAsync(); await RefreshDraftsAsync(); });
    public async Task RefreshProjectsAsync()
    {
        IReadOnlyList<ThemeUsage> usage = [];
        try { usage = await _store.ReadUsageAsync(); } catch (Exception ex) { _logger.Warning(ex, "Cannot read theme usage; preserving metadata"); }
        Projects = (await _store.ListAsync()).Select(p => { var state = usage.LastOrDefault(u => u.ProjectId == p.Id); return new ThemeCard(p, _store.CoverPath(p.Id)) { LastUsedAt = state?.LastUsedAt, Favorite = state?.Favorite == true }; }).ToArray();
        foreach(var row in ThemePlaylistItems)row.RefreshSource();
        foreach (var name in new[] { nameof(Projects), nameof(RecentProjects), nameof(RecentUsedProjects), nameof(HasProjects), nameof(VisibleProjects), nameof(ProjectCategories) }) OnPropertyChanged(name);
    }
    private void Track(Task task) => _pending = task;
    public event EventHandler? CommitPendingEdits;
    private Task RunAsync(Func<Task> work)
    {
        if (!CanEdit) return Task.CompletedTask;
        CommitPendingEdits?.Invoke(this, EventArgs.Empty);
        _busy = true; NotifyCommands(); _pending = ExecuteAsync(work); return _pending;
    }
    private async Task ExecuteAsync(Func<Task> work)
    {
        try { await work(); }
        catch (OperationCanceledException) { Status = "已取消，编辑内容已保留。"; }
        catch (Exception exception) { _logger.Error(exception, "Creator operation failed"); Status = "操作未完成：" + exception.Message; }
        finally { _busy = false; NotifyCommands(); }
    }
    public async Task OpenProjectAsync(ThemeProject project)
    {
        project.Validate();
        await RefreshClockStylesAsync();
        LeavingEditorLayout?.Invoke(this, EventArgs.Empty); await FlushEditorViewportAsync();
        if (ReferenceEquals(_main.Scene, Scene)) { await (_main.Usb?.StopAsync() ?? Task.CompletedTask); _main.Devices.CloseRender(); }
        _templatePicker = false; _repairing = false; _session = new(project); _selection.Clear(); _selectedId = null; _dirty = false; _editing = true; _preview = false; SaveFeedback = "未修改";
        OnPropertyChanged(nameof(EditorSaveSummary));
        Scene.UpdateProject(project); Scene.SetCanvasSize((int)project.ActiveLayout.Width, (int)project.ActiveLayout.Height);
        Status = ""; RefreshLayers(); NotifyPage(); await RestoreEditorViewportAsync(); await ReloadAssetsAsync();
    }
    public void Add(ThemeElementKind kind)
    {
        if (!CanEdit || Project.ActiveLayout.Layers.Count >= 100) { Status = "最多支持100个图层。"; return; }
        if (kind == ThemeElementKind.StockPlugin && _main.Plugins?.Enabled != true || kind == ThemeElementKind.CameraClock && _main.Plugins?.CameraEnabled != true) { Status = "请先在插件管理中启用该插件。"; return; }
        var layout = Project.ActiveLayout;
        var layer = new ThemeLayer { Kind = kind, Name = kind switch { ThemeElementKind.DigitalClock => "数字时钟", ThemeElementKind.Date => "日期", ThemeElementKind.Weekday => "星期", ThemeElementKind.Text => "文字", ThemeElementKind.Rectangle => "矩形", ThemeElementKind.Ellipse => "椭圆", ThemeElementKind.StockPlugin => "股票插件", ThemeElementKind.CameraClock => "复古相机翻页时钟", _ => "图片" }, X = layout.Width * .1, Y = layout.Height * .1, Width = layout.Width * .8, Height = Math.Min(240, layout.Height * .5), FontSize = Orientation == ThemeOrientation.Portrait ? 80 : 120 };
        if (kind == ThemeElementKind.DigitalClock) SizeNewClock(layer);
        Insert(layer);
    }
    public void AddAsset(WallpaperAsset asset)
    {
        if (!CanEdit || !File.Exists(asset.Path)) { Status = "素材文件不存在，请先修复资源路径。"; return; }
        var layout = Project.ActiveLayout;
        Insert(new ThemeLayer { Kind = MaterialTab is "图片" or "我的素材" ? ThemeElementKind.Image : ThemeElementKind.Wallpaper, Name = asset.Name, AssetId = asset.Id, Path = asset.Path, IsVideo = asset.IsVideo, AssetOrigin = string.IsNullOrWhiteSpace(asset.CloudResourceId) ? ThemeAssetOrigin.Local : ThemeAssetOrigin.Cloud, CloudResourceId = asset.CloudResourceId, Width = layout.Width, Height = layout.Height });
    }
    private void Insert(ThemeLayer layer, bool asBackground = false)
    {
        if (Project.ActiveLayout.Layers.Count >= 100) return;
        Change(() => { var layers = Project.ActiveLayout.Layers; layer.ZIndex = asBackground ? layers.Select(l => l.ZIndex).DefaultIfEmpty(0).Min() - 1 : layers.Select(l => l.ZIndex).DefaultIfEmpty(0).Max() + 1; layers.Add(layer); });
        SelectedLayer = layer; Track(ReloadAssetsAsync());
    }
    public void BeginGesture() { if (!CanEdit || !SelectedLayers.Any(l => !l.Locked)) return; _gesture = true; _gestureCheckpoint = false; }
    public void EndGesture() { _gesture = false; _gestureCheckpoint = false; RefreshLayers(); }
    public void MoveBy(double x, double y) => MoveSelection(x, y);
    private void Edit(Action<ThemeLayer> edit)
    {
        if (SelectedLayer is not { Locked: false } layer || !CanEdit) return;
        var candidate = CloneLayer(layer); edit(candidate);
        if (candidate.Kind == ThemeElementKind.Text && candidate.AutoGrowTextHeight) candidate.Height = Math.Clamp(Math.Max(candidate.Height, ThemeTextMetrics.Measure(candidate).Height), 1, 16384);
        if (System.Text.Json.JsonSerializer.Serialize(candidate) == System.Text.Json.JsonSerializer.Serialize(layer)) return;
        Change(() => { edit(layer); layer.Height = candidate.Height; });
    }
    private void Change(Action change) { if (!CanEdit) return; if (!_gesture || !_gestureCheckpoint) { _session.Checkpoint(); _gestureCheckpoint = true; } change(); AdvanceLayoutRevision(); Changed(); }
    private void Changed(bool dirty = true) { if (dirty) _dirty = true; if (_dirty) { _revision++; SaveFeedback = "有未保存的修改"; } Scene.UpdateProject(Project); NotifySelection(); if (!_gesture) RefreshLayers(); OnPropertyChanged(nameof(ProjectName)); NotifyLayout(); OnPropertyChanged(nameof(Capability)); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DirtyIndicator)); OnPropertyChanged(nameof(EditorSaveSummary)); }
    private bool CanReorderSelected => CanEdit && SelectedLayer is { Locked: false } layer && (layer.GroupId is not Guid group || GroupMembers(group).All(l => !l.Locked));
    public void Reorder(Guid source, Guid target)
    {
        var list = Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).ToList();
        var moving = list.FirstOrDefault(l => l.Id == source); var destination = list.FirstOrDefault(l => l.Id == target);
        if (!CanEdit || moving is null || destination is null || source == target) return;
        var members = list.Where(l => moving.GroupId is Guid g ? l.GroupId == g : l.Id == source).ToArray();
        var targets = list.Where(l => destination.GroupId is Guid g ? l.GroupId == g : l.Id == target).ToArray();
        if (members.Any(l => l.Locked) || targets.Any(members.Contains)) return;
        var forward = list.IndexOf(moving) < list.IndexOf(destination);
        Change(() => { list.RemoveAll(members.Contains); var index = forward ? list.IndexOf(targets[^1]) + 1 : list.IndexOf(targets[0]); list.InsertRange(index, members); for (var i = 0; i < list.Count; i++) list[i].ZIndex = i; });
    }
    private void MoveSelected(int step)
    {
        var list = Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).ToList(); var selected = SelectedLayer; if (selected is null) return;
        var members = list.Where(l => selected.GroupId is Guid group ? l.GroupId == group : l.Id == selected.Id).ToArray();
        var edge = step > 0 ? list.IndexOf(members[^1]) : list.IndexOf(members[0]);
        var target = step > 0 ? list.Skip(edge + 1).FirstOrDefault(l => !members.Contains(l)) : list.Take(edge).LastOrDefault(l => !members.Contains(l));
        if (target is not null) Reorder(selected.Id, target.Id);
    }
    private static ThemeLayer CloneLayer(ThemeLayer layer) => layer.Clone();
    private void RefreshLayers() { _refreshingLayers = true; try { LayerRows = BuildLayerRows().ToArray(); OnPropertyChanged(nameof(Layers)); OnPropertyChanged(nameof(LayerRows)); OnPropertyChanged(nameof(SelectedLayerRow)); } finally { _refreshingLayers = false; } NotifyCommands(); }
    private void NotifySelection()
    {

        NotifyEditingTools();
        foreach (var p in new[] { nameof(SelectedLayer), nameof(HasSelection), nameof(IsText), nameof(IsLiteralText), nameof(ShowColor), nameof(IsClock), nameof(IsImage), nameof(LayerName), nameof(X), nameof(Y), nameof(Width), nameof(Height), nameof(Rotation), nameof(Opacity), nameof(Locked), nameof(Visible), nameof(Text), nameof(Format), nameof(Hour12), nameof(ShowSeconds), nameof(FontFamily), nameof(FontSize), nameof(Bold), nameof(Color), nameof(Alignment), nameof(Spacing), nameof(Shadow), nameof(TextOverflowHint), nameof(AutoGrowTextHeight), nameof(TextVerticalAlignment), nameof(Fill), nameof(CropX), nameof(CropY), nameof(Brightness), nameof(AssetSource), nameof(OutputCheck) }) OnPropertyChanged(p);
        NotifyCommands();
        OnPropertyChanged(nameof(CanEditSelection)); OnPropertyChanged(nameof(SelectedLayerRow));
    }
    private void NotifyCommands()
    {
        NotifyLayoutVersions(); NotifyUsability(); NotifyPrecision();
        NotifyEditingTools();
        NotifyManagement();
        NotifyProductivity();
        NotifyNavigationTools();
        NotifyStudioTools();
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanCopyCurrent));
        OnPropertyChanged(nameof(CanEditSelection)); CenterLayerCommand?.Notify(); CopyToOtherLayoutCommand?.Notify();
        foreach (var command in new[] { AddCommand, AddAssetCommand, UndoCommand, RedoCommand, CopyCommand, PasteCommand, DeleteCommand, LayerUpCommand, LayerDownCommand, OpenCommand, DuplicateProjectCommand, DeleteProjectCommand }) command?.Notify();
        foreach (var command in new[] { NewCommand, TemplateCommand, CopyCurrentCommand, ImportCommand, SaveCommand, BackCommand, ApplyCommand, ImportAssetCommand }) command?.NotifyCanExecuteChanged();
    }
    private void NotifyPage() { foreach (var p in new[] { nameof(IsHome), nameof(IsEditor), nameof(IsPreview), nameof(ProjectName), nameof(Orientation), nameof(IsLandscape), nameof(IsPortrait), nameof(OrientationLabel), nameof(Aspect), nameof(LibraryAssets), nameof(Capability), nameof(DeviceStatus), nameof(OutputCheck) }) OnPropertyChanged(p); NotifyManagement(); NotifyMaterials(); NotifySelection(); }
    private void Tick(object? sender, EventArgs e) { if (_templatePicker && _main.SelectedPage == AppPage.Creator) TemplateScene.Tick(DateTimeOffset.Now); if ((_editing && _main.SelectedPage == AppPage.Creator) || ReferenceEquals(_main.Scene, Scene)) Scene.Tick(DateTimeOffset.Now); }
    private void MainChanged(object? sender, PropertyChangedEventArgs e) { ObserveThemePlaylist(e); if (_templatePicker && e.PropertyName == nameof(MainViewModel.SelectedPage) && _main.SelectedPage == AppPage.Creator) { AttachTemplatePlugins(); RefreshTemplatePreview(); } if (e.PropertyName == nameof(MainViewModel.Scene)) { OnPropertyChanged(nameof(CanCopyCurrent)); CopyCurrentCommand.NotifyCanExecuteChanged(); } if (e.PropertyName == nameof(MainViewModel.SelectedPage) && _main.SelectedPage == AppPage.Creator) { OnPropertyChanged(nameof(LibraryAssets)); NotifyMaterials(); } }
    private void DeviceChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(UsbOutputViewModel.Width) or nameof(UsbOutputViewModel.Height) or nameof(UsbOutputViewModel.Rotation)) OnPropertyChanged(nameof(EffectiveUsbGeometry)); if (e.PropertyName is nameof(UsbOutputViewModel.IsSelectedConnected) or nameof(UsbOutputViewModel.SelectedSerial)) OnPropertyChanged(nameof(DeviceStatus)); }
    private Task ReloadAssetsAsync() => _assets = LoadAssetsCoreAsync();
    private async Task LoadAssetsCoreAsync()
    {
        _load.Cancel(); _load.Dispose(); _load = new(); var token = _load.Token;
        Scene.ClearChildren(); foreach (var player in _players) player.Dispose(); _players.Clear();
        var resolved = new Dictionary<string, ImageScene>(StringComparer.OrdinalIgnoreCase);
        var catalog = LibraryAssets.ToDictionary(i => i.Asset.Id, i => i.Asset);
        foreach (var layer in Project.ActiveLayout.Layers.ToArray())
        {
            try
            {
                if (token.IsCancellationRequested || _disposed) return;
                if (layer.Kind == ThemeElementKind.StockPlugin && _main.Plugins is { Enabled: true } plugin) { Scene.SetChild(layer.Id, plugin.Scene); continue; }
                if (layer.Kind == ThemeElementKind.CameraClock && _main.Plugins is { CameraEnabled: true } camera) { Scene.SetChild(layer.Id, camera.CameraScene); continue; }
                if (layer.Kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper)) continue;
                var asset = layer.AssetId is Guid assetId ? catalog.GetValueOrDefault(assetId) : null;
                var path = asset?.Path ?? layer.Path;
                if (!File.Exists(path)) { Status = "部分素材丢失：" + layer.Name; continue; }
                if (layer.IsVideo)
                {
                    var player = new WindowsVideoPlaybackService(_logger); _players.Add(player); await player.OpenAsync(path, 24, token);
                    if (!token.IsCancellationRequested) Scene.SetChild(layer.Id, new VideoScene(path, player));
                }
                else
                {
                    var info = new FileInfo(path); var key = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                    if (!resolved.TryGetValue(key, out var image) && !_imageCache.TryGetValue(key, out image)) { image = await new ImageSceneLoader().LoadAsync(path, layer.Fill, token); ImageDecodeCount++; }
                    if (token.IsCancellationRequested || _disposed) return;
                    resolved[key] = image; Scene.SetChild(layer.Id, image);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) { _logger.Warning(exception, "Cannot resolve theme layer {Id}", layer.Id); Status = "素材加载失败：" + layer.Name; }
        }
        if (token.IsCancellationRequested || _disposed) return;
        _imageCache.Clear(); long cachedBytes = 0;
        foreach (var entry in resolved)
        {
            var bitmap = ((WpfSceneImage)entry.Value.Image).Bitmap;
            var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * Math.Max(4, bitmap.Format.BitsPerPixel / 8);
            if (cachedBytes + bytes > 128L * 1024 * 1024) continue;
            _imageCache.Add(entry.Key, entry.Value); cachedBytes += bytes;
        }
        OnPropertyChanged(nameof(OutputCheck));
    }
    public async Task SaveAsync()
    {
        CommitPendingEdits?.Invoke(this, EventArgs.Empty);
        if (_saving) throw new InvalidOperationException("正在保存，请稍后再试。");
        _saving = true; SaveFeedback = "正在保存…"; OnPropertyChanged(nameof(IsSaving)); NotifyCommands();
        try
        {
            try { await _draftTask; } catch (Exception exception) { _logger.Warning(exception, "Prior draft save failed; continuing manual source save"); }
            await _assets;
            Project.ModifiedAt = DateTimeOffset.Now;
            await _store.SaveAsync(Project);
            var coverSaved = true;
            try
            {
                var view = new SceneView { Scene = Scene, AspectRatioOverride = Aspect, Width = 960 * Math.Min(1, Aspect), Height = 960 / Math.Max(1, Aspect) };
                view.Measure(new Size(view.Width, view.Height)); view.Arrange(new Rect(0, 0, view.Width, view.Height)); view.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)view.Width, (int)view.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); bitmap.Freeze();
                var path = _store.CoverPath(Project.Id);
                await Task.Run(() =>
                {
                    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(temporary)) encoder.Save(file); File.Move(temporary, path, true); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                });
            }
            catch (Exception exception) { coverSaved = false; _logger.Warning(exception, "Theme source saved but cover update failed"); }
            _dirty = false; await FinishSavedDraftAsync(); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DirtyIndicator)); OnPropertyChanged(nameof(EditorSaveSummary));
            SaveFeedback = coverSaved ? $"已保存 · {DateTime.Now:HH:mm:ss}" : "源项目已保存，封面待更新"; Status = SaveFeedback;
            await RefreshProjectsAsync();
        }
        catch (Exception exception)
        {
            SaveFeedback = "保存失败，编辑内容已保留"; Status = SaveFeedback + "：" + exception.Message;
            throw;
        }
        finally { _saving = false; OnPropertyChanged(nameof(IsSaving)); NotifyCommands(); }
    }
    private Task SaveIfDirtyAsync() => ResolveUnsavedAsync();
    public async Task PrepareForCloseAsync() { CommitPendingEdits?.Invoke(this, EventArgs.Empty); await _pending; await _assets; await SaveIfDirtyAsync(); StopThemePlaylist(); await _themeAdvanceTask; await FlushThemePlaylistAsync(); _draftTimer.Stop(); _clock.Stop(); try { LeavingEditorLayout?.Invoke(this, EventArgs.Empty); await _pending; await _assets; await FlushEditorViewportAsync(); try { await _draftTask; } catch (Exception exception) { _logger.Warning(exception, "Recovery draft failed before close; retaining source save path"); } await SaveIfDirtyAsync(); _load.Cancel(); } catch { if (!_disposed) { _clock.Start(); _draftTimer.Start(); } throw; } }
    public void Dispose() { if (_disposed) return; _disposed = true; if (_materialLibrary is not null) _materialLibrary.PropertyChanged -= MaterialLibraryChanged; DisposeThemePlaylist(); _draftTimer.Stop(); _draftTimer.Tick -= DraftTick; DisposeTemplateLibrary(); TemplateScene.Dispose(); _clock.Stop(); _clock.Tick -= Tick; _load.Cancel(); _load.Dispose(); foreach (var player in _players) player.Dispose(); _imageCache.Clear(); Scene.Dispose(); _main.PropertyChanged -= MainChanged; if (_main.Usb is { } usb) usb.PropertyChanged -= DeviceChanged; }
}


