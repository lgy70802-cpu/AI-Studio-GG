using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class DevicesViewModel : ObservableObject, IDisposable
{
    public ManagedDevicesViewModel? Managed { get; private set; }
    public void AttachManaged(ManagedDevicesViewModel managed) { Managed = managed; OnPropertyChanged(nameof(Managed)); }
    private readonly IDisplayManager _displayManager;
    private readonly ITargetDisplayService _targetService;
    private readonly ILogger _logger;
    private readonly IRenderWindowService? _renderService;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private DisplayInfo? _selectedDisplay;
    private bool _isRefreshing;
    private bool _isSaving;
    private bool _refreshPending;
    private bool _snapshotValid;
    private bool _closing;
    private bool _closeRequested;
    private bool _disposed;
    private bool _isTesting;
    private int _renderRequestVersion;
    private string _status = "正在读取显示器…";
    private string _targetActionStatus = string.Empty;

    public IReadOnlyList<DisplayInfo> Displays { get => _displays; private set => SetProperty(ref _displays, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string TargetActionStatus { get => _targetActionStatus; private set => SetProperty(ref _targetActionStatus, value); }
    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set { if (SetProperty(ref _selectedDisplay, value)) NotifyCommands(); }
    }
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set { if (SetProperty(ref _isRefreshing, value)) NotifyCommands(); }
    }
    public bool IsSaving
    {
        get => _isSaving;
        private set { if (SetProperty(ref _isSaving, value)) NotifyCommands(); }
    }
    public DisplayInfo? TargetDisplay => _targetService.GetTargetDisplay();
    public bool HasSavedTarget => _targetService.SavedTarget is not null;
    public int TargetPixelWidth => TargetDisplay?.Width ?? _targetService.SavedTarget?.LastWidth ?? 0;
    public int TargetPixelHeight => TargetDisplay?.Height ?? _targetService.SavedTarget?.LastHeight ?? 0;
    public bool IsRenderOpen => _renderService?.IsOpen == true;
    public string RenderStatus => _renderService?.Status ?? "副屏显示未启用。";
    public string TargetName => TargetDisplay?.FriendlyName is { Length: > 0 } name ? name
        : _targetService.SavedTarget?.FriendlyName is { Length: > 0 } savedName ? savedName
        : _targetService.SavedTarget?.DeviceName is { Length: > 0 } deviceName ? deviceName : "尚未设置目标副屏";
    public string TargetResolution
    {
        get
        {
            var width = TargetDisplay?.Width ?? _targetService.SavedTarget?.LastWidth ?? 0;
            var height = TargetDisplay?.Height ?? _targetService.SavedTarget?.LastHeight ?? 0;
            return width > 0 && height > 0
                ? $"{(TargetDisplay is null ? "最近一次分辨率" : "分辨率")}：{width} × {height} px" : "分辨率：暂无记录";
        }
    }
    public string TargetConnectionStatus => !HasSavedTarget ? "状态：未设置"
        : TargetDisplay is not null ? "状态：已连接" : "状态：未连接";
    public string TargetNotice => !HasSavedTarget ? "请选择一块非主显示器，设为目标副屏。"
        : TargetDisplay is not null ? "目标副屏已连接。"
        : _snapshotValid ? "目标副屏未连接" : "目标副屏未连接，等待刷新确认显示器状态。";

    // The close handler awaits a write that has already started; no write is left fire-and-forget.
    public Task PendingSave { get; private set; } = Task.CompletedTask;
    public Task PendingTargetChange { get; private set; } = Task.CompletedTask;
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand SetTargetCommand { get; }
    public AsyncCommand ClearTargetCommand { get; }
    public AsyncCommand TestDisplayCommand { get; }
    public AsyncCommand CloseRenderCommand { get; }

    public DevicesViewModel(IDisplayManager displayManager, ITargetDisplayService targetService, ILogger logger,
        IRenderWindowService? renderService = null)
    {
        _displayManager = displayManager;
        _targetService = targetService;
        _logger = logger;
        _renderService = renderService;
        RefreshCommand = new AsyncCommand(RefreshAsync, () => CanInteract, logger);
        SetTargetCommand = new AsyncCommand(SetSelectedTargetAsync,
            () => CanInteract && _snapshotValid && SelectedDisplay is { IsPrimary: false }, logger);
        ClearTargetCommand = new AsyncCommand(ClearTargetAsync, () => CanInteract && HasSavedTarget, logger);
        TestDisplayCommand = new AsyncCommand(TestDisplayAsync,
            () => CanInteract && _renderService is not null && _snapshotValid && TargetDisplay is { IsPrimary: false }, logger);
        CloseRenderCommand = new AsyncCommand(() => { CloseRender(); return Task.CompletedTask; },
            () => !_disposed && !_closing && (_renderService?.IsOpen == true || _isTesting), logger);
        _displayManager.DisplaysChanged += OnDisplaysChanged;
        if (_renderService is not null) _renderService.StateChanged += OnRenderStateChanged;
    }

    private bool CanInteract => !_disposed && !_closeRequested && !_closing && !IsRefreshing && !IsSaving && !_isTesting;

    public async Task RefreshAsync()
    {
        if (_disposed || _closing || _closeRequested) return;
        if (IsRefreshing || IsSaving || _isTesting)
        {
            _refreshPending = true;
            return;
        }
        IsRefreshing = true;
        var token = _lifetime.Token;
        try
        {
            do
            {
                _refreshPending = false;
                Status = "正在刷新显示器…";
                try
                {
                    var snapshot = await _displayManager.GetDisplaysAsync(token);
                    if (_disposed || _closing || _closeRequested) return;
                    ApplySnapshot(snapshot);
                    if (_targetService.NeedsSave && !await SaveTargetAsync())
                        TargetActionStatus = "目标信息保存失败，已保留原有配置。请检查日志后重试。";
                    UpdateTargetProperties();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Cannot refresh devices page; keeping last successful list");
                    if (_disposed || _closing) return;
                    MarkSnapshotUnavailable();
                    Status = "刷新失败，已保留上次列表，目标连接状态待确认。请稍后重试。";
                }
            } while (_refreshPending && !_disposed && !_closing && !_closeRequested);
        }
        finally { IsRefreshing = false; }
    }

    private void ApplySnapshot(IReadOnlyList<DisplayInfo> snapshot)
    {
        var selected = SelectedDisplay;
        Displays = snapshot;
        _snapshotValid = true;
        _targetService.UpdateDisplays(snapshot);
        SelectedDisplay = selected is null ? null : FindSelected(snapshot, selected);
        Status = snapshot.Count == 0 ? "未检测到活动显示器，可稍后刷新。" : $"已检测到 {snapshot.Count} 块活动显示器 · 坐标与分辨率单位：像素";
        UpdateTargetProperties();
    }

    private static DisplayInfo? FindSelected(IReadOnlyList<DisplayInfo> displays, DisplayInfo selected)
    {
        var matches = displays.Where(item => !string.IsNullOrWhiteSpace(selected.DeviceId)
            ? string.Equals(item.DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase)
            : item.DeviceName == selected.DeviceName && item.FriendlyName == selected.FriendlyName).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public Task SetSelectedTargetAsync() => PendingTargetChange = ChangeTargetAsync(clear: false);
    public Task ClearTargetAsync() => PendingTargetChange = ChangeTargetAsync(clear: true);

    private async Task ChangeTargetAsync(bool clear)
    {
        if (!(clear ? ClearTargetCommand : SetTargetCommand).CanExecute(null)) return;
        IsSaving = true;
        var token = _lifetime.Token;
        try
        {
            TargetActionStatus = "正在保存目标副屏…";
            if (clear) _targetService.ClearTargetDisplay();
            else
            {
                var selected = SelectedDisplay!;
                // Re-enumerate: a screen might have been unplugged or made primary since the last refresh.
                var snapshot = await _displayManager.GetDisplaysAsync(token);
                if (_disposed || _closing) return;
                ApplySnapshot(snapshot);
                var current = FindSelected(snapshot, selected);
                if (current is null || current.IsPrimary)
                {
                    TargetActionStatus = "所选设备已断开或变为主显示器，请重新选择。";
                    return;
                }
                _targetService.SetTargetDisplay(current);
            }
            UpdateTargetProperties();
            var saved = await SaveTargetAsync();
            if (_disposed) return;
            TargetActionStatus = saved ? clear ? "已取消目标副屏，并保存到本地配置。" : "已保存目标副屏，下次启动将自动识别。"
                : "保存失败，已恢复原设置。请检查日志后重试。";
            UpdateTargetProperties();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Error(exception, "Failed to change target display; clear: {Clear}", clear);
            if (!_disposed)
            {
                MarkSnapshotUnavailable();
                TargetActionStatus = "无法确认显示器状态，未保存选择。请刷新后重试。";
            }
        }
        finally
        {
            IsSaving = false;
            if (_refreshPending && !_disposed && !_closing && !_closeRequested) await RefreshAsync();
        }
    }

    private async Task<bool> SaveTargetAsync()
    {
        var save = _targetService.SaveAsync();
        PendingSave = save;
        return await save;
    }

    public Task TestDisplayAsync() => OpenSceneAsync(null);

    public Task ApplySceneAsync(Scene scene) => OpenSceneAsync(scene);
    public Func<Task>? BeforeOpenOutputAsync { get; set; }

    private async Task OpenSceneAsync(Scene? scene)
    {
        if (!TestDisplayCommand.CanExecute(null)) return;
        _isTesting = true;
        var requestVersion = ++_renderRequestVersion;
        var token = _lifetime.Token;
        NotifyCommands();
        try
        {
            // Always enumerate again; the visible list may predate a disconnect notification.
            var snapshot = await _displayManager.GetDisplaysAsync(token);
            if (_disposed || _closing || _closeRequested || requestVersion != _renderRequestVersion) return;
            ApplySnapshot(snapshot);
            if (_targetService.NeedsSave && !await SaveTargetAsync())
                TargetActionStatus = "目标信息保存失败，保留原配置。";
            if (_disposed || _closing || _closeRequested || requestVersion != _renderRequestVersion) return;
            UpdateTargetProperties();
            if (TargetDisplay is not { IsPrimary: false } target || _refreshPending)
            {
                TargetActionStatus = "目标副屏未连接或显示配置已变化，请刷新后重试。";
                return;
            }
            if (BeforeOpenOutputAsync is not null) await BeforeOpenOutputAsync();
            if (_disposed || _closing || _closeRequested || requestVersion != _renderRequestVersion || _refreshPending) return;
            if (scene is null) _renderService!.ShowTest(target);
            else _renderService!.ShowScene(target, scene);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Error(exception, "Cannot start external display test");
            if (!_disposed)
            {
                MarkSnapshotUnavailable();
                TargetActionStatus = "无法确认目标副屏，未打开测试窗口。请刷新后重试。";
            }
        }
        finally
        {
            _isTesting = false;
            NotifyCommands();
            if (_refreshPending && !_disposed && !_closing && !_closeRequested) await RefreshAsync();
        }
    }

    public void CloseRender()
    {
        ++_renderRequestVersion; // Cancel a pending open as well as an already visible window.
        _renderService?.Close();
        NotifyCommands();
    }

    private void OnRenderStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RenderStatus));
        NotifyCommands();
    }

    private void MarkSnapshotUnavailable()
    {
        _snapshotValid = false;
        _targetService.MarkDisplaysUnavailable();
        UpdateTargetProperties();
    }

    private void UpdateTargetProperties()
    {
        _renderService?.SynchronizeTarget(TargetDisplay);
        OnPropertyChanged(nameof(TargetDisplay));
        OnPropertyChanged(nameof(TargetPixelWidth));
        OnPropertyChanged(nameof(TargetPixelHeight));
        OnPropertyChanged(nameof(HasSavedTarget));
        OnPropertyChanged(nameof(TargetName));
        OnPropertyChanged(nameof(TargetResolution));
        OnPropertyChanged(nameof(TargetConnectionStatus));
        OnPropertyChanged(nameof(TargetNotice));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SetTargetCommand.NotifyCanExecuteChanged();
        ClearTargetCommand.NotifyCanExecuteChanged();
        TestDisplayCommand.NotifyCanExecuteChanged();
        CloseRenderCommand.NotifyCanExecuteChanged();
    }

    private async void OnDisplaysChanged(object? sender, EventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception exception) { _logger.Error(exception, "Automatic display refresh failed"); }
    }

    public async Task PrepareForCloseAsync()
    {
        if (Managed is not null) await Managed.PrepareForCloseAsync();
        _closeRequested = true;
        CloseRender();
        NotifyCommands();
        // Finish an accepted Set/Clear command, including its pre-save device revalidation.
        await PendingTargetChange;
        BeginClosing();
        await PendingSave;
    }

    public void BeginClosing()
    {
        if (_closing) return;
        _closing = true;
        CloseRender();
        if (_renderService is not null) _renderService.StateChanged -= OnRenderStateChanged;
        _displayManager.DisplaysChanged -= OnDisplaysChanged;
        _lifetime.Cancel();
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        BeginClosing();
        _disposed = true;
        Managed?.Dispose();
        _lifetime.Dispose();
    }
}
