using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed record AdaptationChoice(string Name, LayoutAdaptation Mode);

/// <summary>A read-only source snapshot; switching devices never calls the editor's orientation or history APIs.</summary>
public sealed class CreatorDevicePreviewViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ManagedDevicesViewModel _devices;
    private readonly ThemeProject _source;
    private readonly ThemeDocument _document;
    private readonly DispatcherTimer _timer;
    private readonly ILogger _logger;
    private CancellationTokenSource _load = new();
    private ThemeDevicePreview? _preview;
    private ManagedDeviceCard? _target;
    private AdaptationChoice _adaptation;
    private Task _pending = Task.CompletedTask, _apply = Task.CompletedTask;
    private Task? _release;
    private bool _closed, _ready, _applying;
    private long _generation;
    private string _status = "请选择目标设备。", _geometry = "", _match = "";
    private IReadOnlyList<string> _issues = [];
    public ObservableCollection<ManagedDeviceCard> Devices => _devices.Cards;
    public IReadOnlyList<AdaptationChoice> Adaptations { get; } = [new("完整显示", LayoutAdaptation.Contain), new("等比裁切", LayoutAdaptation.Cover)];
    public ManagedDeviceCard? Target
    {
        get => _target;
        set
        {
            if (ReferenceEquals(_target, value)) return;
            if (_target is not null) _target.PropertyChanged -= TargetChanged;
            _target = value;
            if (_target is not null) _target.PropertyChanged += TargetChanged;
            OnPropertyChanged(); QueuePreview();
        }
    }
    public AdaptationChoice Adaptation { get => _adaptation; set { if (value is not null && SetProperty(ref _adaptation, value)) QueuePreview(); } }
    private ThemeScene? _sourceScene;
    public ThemeScene? SourceScene { get => _sourceScene; set { _sourceScene=value; OnPropertyChanged(nameof(PreviewScene)); OnPropertyChanged(nameof(PreviewAspect)); } }
    public ThemeScene? PreviewScene => _preview?.Scene ?? (Target is null ? SourceScene : null);
    public double PreviewAspect => PreviewScene?.AspectRatio ?? 4;
    public string ThemeName => _source.Name;
    public string Geometry { get => _geometry; private set => SetProperty(ref _geometry, value); }
    public string Match { get => _match; private set => SetProperty(ref _match, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public IReadOnlyList<string> Issues { get => _issues; private set { _issues = value; OnPropertyChanged(); } }
    public Task PendingPreview => _pending;
    public Task PendingApply => _apply;
    public AsyncCommand ApplyTargetCommand { get; }
    public AsyncCommand ApplySelectedCommand { get; }
    private Func<Task>? _saveSource;
    public Func<Task>? SaveSourceAsync { get => _saveSource; set { _saveSource=value; NotifyCommands(); OnPropertyChanged(nameof(HasSaveAction)); } }
    public bool HasSaveAction => _saveSource is not null;
    public string BatchApplyLabel => HasSaveAction ? "保存并应用到所选设备" : "应用到所选设备";
    public string ReturnLabel => HasSaveAction ? "返回编辑" : "关闭预览";
    public bool CanConfigure => !_closed && !_applying;
    public bool NeedsConfirmation => Target is { Connected: true, Confirmed: false, Mirrored: false };
    private bool HasOwnedOutput => Target?.IsActive==true || _devices.UsesOwnedLegacyOutput(Target);
    public string ApplyLabel => HasSaveAction ? HasOwnedOutput ? "保存并切换到此设备" : "保存并应用到此设备" : HasOwnedOutput ? "切换到此设备" : "应用到此设备";
    public AsyncCommand SaveOnlyCommand { get; }
    private string[] _appliedIds = [];
    public IReadOnlyList<PreviewOutputResult> Results => _appliedIds.Select(id => Devices.FirstOrDefault(d=>d.DeviceId==id) is { } c ? new PreviewOutputResult(c.Alias,_devices.UsesOwnedLegacyOutput(c)?"原有 USB 输出运行中":c.OutputStatus,_devices.UsesOwnedLegacyOutput(c)?"原有 USB 场景":c.AppliedTheme,c.Message) : new PreviewOutputResult(id,"设备已不可用","","请重新连接后重试。")).ToArray();
    public Action<NamedThemeLayout>? EditAdaptedLayout { get; set; }
    public NamedThemeLayout CreateEditableLayout(bool fillBackground = false)
    {
        if (!_ready || Target is null) throw new InvalidOperationException("请先等待有效预览。");
        var size = _devices.GetProfile(Target).SurfaceSize;
        var selection = LayoutSelector.Select(_document, size);
        var resolved = LayoutResolver.Resolve(selection.Layout, size, Adaptation.Mode);
        var name = $"{new string(Target.Alias.Take(50).ToArray())} {size.Width}×{size.Height}" + (fillBackground ? " 铺满" : "");
        return fillBackground ? EditableLayoutAdapter.CaptureWithFilledBackground(_document, resolved, name) : EditableLayoutAdapter.Capture(_document, resolved, name);
    }
    public bool CanAdapt => _ready && !_closed && !_applying && EditAdaptedLayout is not null;
    public string Readiness
    {
        get
        {
            if(_applying || _devices.IsBusy)return "正在保存或处理设备…";
            if(Target is null)return HasSaveAction ? "未选择设备，可以仅保存并预览" : "未选择设备，仅预览";
            if(Target.Mirrored)return "此显示源无法独立输出，请先检查 Windows 扩展模式";
            if(!Target.Connected)return HasSaveAction ? "设备未连接，可以仅保存" : "设备未连接，仅预览";
            if(!_ready)return "请先处理预览问题";
            if(!Target.Confirmed)return Target.IsActive ? "请先停止当前输出，再核对设备参数" : "请先核对上方设备与参数";
            if(HasOwnedOutput)return "将切换本软件在此设备上的当前输出";
            return "预览就绪，可以应用";
        }
    }
    public CreatorDevicePreviewViewModel(ThemeProject source, ManagedDevicesViewModel devices, ILogger logger)
    {
        _source = source.Clone(); _document = LegacyThemeAdapter.Read(_source); _devices = devices; _logger = logger;
        _adaptation = Adaptations[0];
        ApplyTargetCommand = new(() => _apply = ApplyAsync(false), CanApplyTarget, logger);
        SaveOnlyCommand = new(() => _apply = SaveOnlyAsync(), () => HasSaveAction && CanConfigure && !_devices.IsBusy, logger);
        ApplySelectedCommand = new(() => _apply = ApplyAsync(true), () => !_closed && !_applying && !_devices.IsBusy && Devices.Any(d => d.Selected && d.Connected && d.Confirmed && !d.Mirrored), logger);
        _timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            try { _preview?.Tick(); } catch (Exception ex) { _ready = false; Status = ex.Message; }
            NotifyCommands();
        }, Dispatcher.CurrentDispatcher);
        _timer.Stop(); Target = Devices.FirstOrDefault(d => d.Connected) ?? Devices.FirstOrDefault(); _timer.Start();
    }
    private bool CanApplyTarget() => !_closed && !_applying && !_devices.IsBusy && _ready && Target is { Connected: true, Confirmed: true, Mirrored: false };
    private void NotifyCommands() { ApplyTargetCommand.NotifyCanExecuteChanged(); ApplySelectedCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanAdapt)); OnPropertyChanged(nameof(Readiness)); SaveOnlyCommand?.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanConfigure)); OnPropertyChanged(nameof(NeedsConfirmation)); OnPropertyChanged(nameof(ApplyLabel)); OnPropertyChanged(nameof(BatchApplyLabel)); OnPropertyChanged(nameof(ReturnLabel)); OnPropertyChanged(nameof(Results)); }
    public void ReportRenderFailure(Exception error) { _ready = false; Status = "预览渲染失败：" + error.Message; Issues = [error.Message]; NotifyCommands(); }
    private void TargetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ManagedDeviceCard.Width) or nameof(ManagedDeviceCard.Height) or nameof(ManagedDeviceCard.Rotation)) QueuePreview();
        else NotifyCommands();
    }
    private void QueuePreview()
    {
        if (_closed) return;
        _load.Cancel(); _load.Dispose(); _load = new();
        var generation = ++_generation; _ready = false; NotifyCommands();
        var prior = _pending; var target = Target; var mode = Adaptation.Mode; var ct = _load.Token;
        _pending = RebuildAsync(prior, target, mode, generation, ct);
    }
    private async Task RebuildAsync(Task prior, ManagedDeviceCard? target, LayoutAdaptation mode, long generation, CancellationToken ct)
    {
        try
        {
            await prior;
            ct.ThrowIfCancellationRequested();
            var previous = _preview; _preview = null;
            if (previous is not null) await previous.DisposeAsync();
            OnPropertyChanged(nameof(PreviewScene));
            if (target is null) { Status = "当前未连接设备，可先保存主题，连接后再应用。"; Geometry = "原始画布预览"; Match = ""; Issues = []; OnPropertyChanged(nameof(PreviewAspect)); return; }
            var profile = _devices.GetProfile(target);
            var selection = LayoutSelector.Select(_document, profile.SurfaceSize);
            var resolved = LayoutResolver.Resolve(selection.Layout, profile.SurfaceSize, mode);
            Geometry = $"{profile.SurfaceSize.Width} × {profile.SurfaceSize.Height} · {(profile.SurfaceSize.Width >= profile.SurfaceSize.Height ? "横屏" : "竖屏")}";
            Match = selection.Match switch { LayoutMatch.Exact => "画面比例与屏幕一致", LayoutMatch.WithinTolerance => "画面比例接近屏幕", _ => mode == LayoutAdaptation.Contain ? "完整显示，可能留边" : "裁切铺满，请检查画面边缘" };
            var issues = resolved.Diagnostics.Select(d => d.Code switch
            {
                "ContentOutsideSurface" => "有图层超出可见区域，请检查裁切。", "LegacyStretchRequiresReview" => "旧图层使用拉伸，请先在编辑器改为等比显示。",
                "UnsatisfiedMinimumMargin" => "最小边距无法满足。", _ => "布局方向需要调整。"
            }).Distinct().ToList();
            var installed = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in selection.Layout.Layers.Where(l => l.Visible))
            {
                var config = layer.CreateLegacyLayer();
                if (layer.Kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday && !installed.Contains(config.FontFamily))
                    issues.Add($"字体 {config.FontFamily} 未安装，预览将使用系统回退字体。");
            }
            Issues = issues.Distinct().ToArray();
            var factory = _devices.PreviewFactory ?? throw new InvalidOperationException("设备预览服务未启用。");
            var preview = await factory(_document, resolved, ct);
            if (_closed || generation != _generation || ct.IsCancellationRequested) { await preview.DisposeAsync(); return; }
            _preview = preview;
            _ready = !resolved.Diagnostics.Any(d => d.Code is "LegacyStretchRequiresReview" or "OrientationMismatch" or "UnsatisfiedMinimumMargin");
            Status = target.Connected ? "预览不会占用设备；核对画面后应用。" : "设备未连接，仅显示离线预览。";
            OnPropertyChanged(nameof(PreviewScene)); OnPropertyChanged(nameof(PreviewAspect));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { if (generation == _generation) { _ready = false; Status = "无法预览：" + ex.Message; Issues = [ex.Message]; } _logger.Warning(ex, "Device theme preview failed"); }
        finally { NotifyCommands(); }
    }
    private async Task SaveOnlyAsync()
    {
        if (_saveSource is null || !CanConfigure) return;
        _applying=true; NotifyCommands();
        try { await _saveSource(); Status="主题已保存；尚未应用到设备。"; }
        catch(Exception ex) { Status="保存失败，未应用到设备："+ex.Message; _logger.Warning(ex,"Creator save before apply failed"); }
        finally { _applying=false; NotifyCommands(); }
    }
    private async Task ApplyAsync(bool selected)
    {
        if (_closed || _applying || _devices.IsBusy) return;
        var targets = (selected ? Devices.Where(d=>d.Selected) : Target is null ? Enumerable.Empty<ManagedDeviceCard>() : new[]{Target}).ToArray();
        var stamps = targets.Select(c=>(c.DeviceId,c.Device.Endpoint.ConnectionEpoch,c.Width,c.Height,c.Rotation,c.PhysicalGroup)).ToArray();
        var adaptation=Adaptation.Mode; var sourceSaved=false;
        _applying = true; _appliedIds=[]; NotifyCommands();
        try
        {
            if (_saveSource is not null) { Status="正在保存主题…"; await _saveSource(); sourceSaved=true; }
            if((!selected && Target?.DeviceId!=targets.FirstOrDefault()?.DeviceId) || Adaptation.Mode!=adaptation || stamps.Any(stamp=>!Devices.Any(c=>c.DeviceId==stamp.DeviceId && c.Device.Endpoint.ConnectionEpoch==stamp.ConnectionEpoch && c.Width==stamp.Width && c.Height==stamp.Height && c.Rotation==stamp.Rotation && c.PhysicalGroup==stamp.PhysicalGroup)))
                throw new InvalidOperationException("设备或参数已变化，请检查后重新应用。");
            _appliedIds=targets.Select(c=>c.DeviceId).ToArray();
            Status=HasSaveAction ? "主题已保存，正在应用…" : "正在应用…";
            // Explicit apply may replace our own output independently of whether the source needs saving.
            await _devices.ApplySourceAsync(_source, _appliedIds, adaptation, replaceOwned:true);
            Status=(HasSaveAction ? "主题已保存。" : "")+"已处理应用请求，下方显示各设备的软件输出状态。";
        }
        catch(Exception ex) { Status=(sourceSaved ? "主题已保存，应用未完成：" : _saveSource is not null ? "保存失败，未应用到设备：" : "应用未完成：")+ex.Message; _logger.Warning(ex,"Creator save/apply operation failed"); }
        finally { _applying = false; NotifyCommands(); }
    }
    public ValueTask DisposeAsync() => new(_release ??= ReleaseAsync());
    private async Task ReleaseAsync()
    {
        if (_closed) return; _closed = true; _timer.Stop(); _load.Cancel();
        if (_target is not null) _target.PropertyChanged -= TargetChanged;
        await Task.WhenAll(_pending, _apply);
        if (_preview is not null) await _preview.DisposeAsync();
        _preview = null; SourceScene = null; OnPropertyChanged(nameof(PreviewScene)); _load.Dispose();
    }
}

public sealed record PreviewOutputResult(string Device, string State, string Theme, string Message);







