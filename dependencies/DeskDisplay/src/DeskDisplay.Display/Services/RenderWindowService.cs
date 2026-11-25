using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Rendering;
using DeskDisplay.Display.Views;
using Serilog;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;

namespace DeskDisplay.Display.Services;

public sealed class RenderWindowService : IRenderWindowService
{
    private readonly ILogger _logger;
    private Scene _scene;
    private readonly Scene _initialScene;
    private readonly Func<IRenderSurface> _createSurface;
    private IRenderSurface? _surface;
    private DisplayInfo? _target;
    private bool _suspended;
    private bool _disposed;
    private readonly IDeviceLease? _ownership;
    private IDeviceLease? _lease;

    public bool IsOpen => _surface is not null;
    public Scene? CurrentScene => IsOpen ? _scene : null;
    public string Status { get; private set; } = "副屏显示已关闭。";
    public event EventHandler? StateChanged;

    public RenderWindowService(ILogger logger, Scene scene) : this(logger, scene, () => new RenderWindow(logger)) { }

    public RenderWindowService(ILogger logger, Scene scene, IDeviceLease ownership)
        : this(logger, scene, () => new RenderWindow(logger), ownership) { }

    internal RenderWindowService(ILogger logger, Scene scene, Func<IRenderSurface> createSurface, IDeviceLease? ownership = null)
    {
        _logger = logger;
        _scene = scene;
        _initialScene = scene;
        _createSurface = createSurface;
        _ownership = ownership;
    }

    public void ShowTest(DisplayInfo target)
    {
        if (_disposed) return;
        try
        {
            if (target.IsPrimary || target.Width <= 0 || target.Height <= 0)
                throw new InvalidOperationException("A valid secondary target is required.");
            if (_surface is not null) { SynchronizeTarget(target); return; }
            var key = OutputDeviceKeys.WindowsDisplay(target.DeviceId ?? target.DeviceName);
            _lease = _ownership is null ? WindowsDeviceLeaseProvider.Shared.TryAcquire(key) : null;
            var lease = _ownership ?? _lease;
            if (lease is null || lease.IsReleased || lease.DeviceId != key)
                throw new InvalidOperationException("设备已有输出会话或无法取得设备锁。");
            _target = target;
            _surface = _createSurface();
            _surface.Closed += OnClosed;
            _surface.Faulted += OnFaulted;
            _surface.TopologyInvalidated += OnTopologyInvalidated;
            _scene.SetCanvasSize(target.Width, target.Height);
            _surface.Render(_scene);
            _surface.ShowOnDisplay(target);
            SetStatus("副屏场景已打开。");
        }
        catch (Exception exception) { OnFaulted(exception); }
    }

    public void ShowScene(DisplayInfo target, Scene scene)
    {
        if (_disposed) return;
        try
        {
            if (target.IsPrimary || target.Width <= 0 || target.Height <= 0)
                throw new InvalidOperationException("A valid secondary target is required.");
            SynchronizeTarget(target);
            _scene = scene;
            scene.SetCanvasSize(target.Width, target.Height);
            if (_surface is not null) _surface.Render(scene);
            ShowTest(target);
        }
        catch (Exception exception) { OnFaulted(exception); }
    }

    public void SynchronizeTarget(DisplayInfo? target)
    {
        if (_surface is null) return;
        if (target is null || target.IsPrimary || _target is null || !SameDevice(_target, target))
        {
            Close();
            return;
        }
        if (!_suspended && _target == target) return;
        try
        {
            _target = target;
            _scene.SetCanvasSize(target.Width, target.Height);
            _surface.ShowOnDisplay(target);
            _suspended = false;
            SetStatus("副屏场景已打开。");
        }
        catch (Exception exception) { OnFaulted(exception); }
    }

    private static bool SameDevice(DisplayInfo first, DisplayInfo second) =>
        !string.IsNullOrWhiteSpace(first.DeviceId) && !string.IsNullOrWhiteSpace(second.DeviceId)
            ? string.Equals(first.DeviceId, second.DeviceId, StringComparison.OrdinalIgnoreCase)
            : first.DeviceName == second.DeviceName && first.FriendlyName == second.FriendlyName;

    private void OnTopologyInvalidated(object? sender, EventArgs e)
    {
        _suspended = true;
        SetStatus("显示配置已变化，正在确认目标副屏。");
    }

    private void OnClosed(object? sender, EventArgs e) => Close();

    private void OnFaulted(Exception exception)
    {
        _logger.Error(exception, "External render output failed; closing only the render window");
        ReleaseSurface();
        SetStatus("副屏显示发生错误，已关闭。请刷新显示器后重试。");
    }

    public void Close()
    {
        ReleaseSurface();
        // A closed video session must not be reopened by the generic test button after leaving it.
        if (_scene is VideoScene) _scene = _initialScene;
        SetStatus("副屏显示已关闭。");
    }

    private void ReleaseSurface()
    {
        var surface = _surface;
        _surface = null;
        _target = null;
        _suspended = false;
        if (surface is null) { _lease?.Dispose(); _lease = null; return; }
        surface.Closed -= OnClosed;
        surface.Faulted -= OnFaulted;
        surface.TopologyInvalidated -= OnTopologyInvalidated;
        try { surface.Dispose(); }
        catch (Exception exception) { _logger.Error(exception, "Render surface disposal failed"); }
        finally { _lease?.Dispose(); _lease = null; }
        _logger.Information("Render surface released");
    }

    private void SetStatus(string status)
    {
        Status = status;
        try { StateChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception) { _logger.Error(exception, "Render state subscriber failed"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
        StateChanged = null;
    }
}
