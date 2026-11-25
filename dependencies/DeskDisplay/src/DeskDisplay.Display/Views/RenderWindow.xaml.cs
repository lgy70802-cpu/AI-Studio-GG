using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Interop;
using DeskDisplay.Display.Rendering;
using Serilog;

namespace DeskDisplay.Display.Views;

public partial class RenderWindow : Window, IRenderSurface
{
    private readonly ILogger _logger;
    private readonly Func<DisplayInfo, NativeMethods.Rect> _resolveBounds;
    private HwndSource? _source;
    private DisplayInfo? _target;
    private NativeMethods.Rect _bounds;
    private DispatcherOperation? _pendingPlacement;
    private bool _topologyInvalid;
    private bool _failed;
    private bool _closed;

    public event Action<Exception>? Faulted;
    public event EventHandler? TopologyInvalidated;

    public RenderWindow(ILogger logger) : this(logger, RenderWindowNative.ResolveActiveBounds) { }

    // Test seam substitutes only monitor validation, retaining real HWND placement and WPF DPI behavior.
    internal RenderWindow(ILogger logger, Func<DisplayInfo, NativeMethods.Rect> resolveBounds)
    {
        _logger = logger;
        _resolveBounds = resolveBounds;
        InitializeComponent();
        SceneContent.RenderFailed += Fail;
    }

    public void ShowOnDisplay(DisplayInfo target)
    {
        Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_failed) throw new InvalidOperationException("Cannot reuse a failed render window.");
        _bounds = _resolveBounds(target);
        _target = target;
        _topologyInvalid = false;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        // Move the invisible HWND to the destination first; WPF can adopt its DPI before first presentation.
        RenderWindowNative.PlacePixels(handle, _bounds);
        if (!IsVisible) Show();
        if (_topologyInvalid) throw new InvalidOperationException("Display configuration changed while opening the render window.");
        _bounds = _resolveBounds(target);
        RenderWindowNative.PlacePixels(handle, _bounds);
        _logger.Information("Render window covers {DeviceName}: {X},{Y} {Width}x{Height} pixels at {Dpi} DPI",
            target.DeviceName, target.X, target.Y, target.Width, target.Height, RenderWindowNative.GetDpiForWindow(handle));
    }

    public void Render(Scene scene)
    {
        SceneContent.DataContext = scene;
        SceneContent.Scene = scene;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)
            ?? throw new InvalidOperationException("Render window HWND source is unavailable.");
        _source.AddHook(WindowProc);
        RenderWindowNative.DisableRoundedCorners(_source.Handle, _logger);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        try
        {
            if (_closed || _failed) return 0;
            if (message == NativeMethods.DisplayChange)
            {
                _topologyInvalid = true;
                // Prevent Windows from visibly relocating the topmost output onto the primary screen on unplug.
                Hide();
                TopologyInvalidated?.Invoke(this, EventArgs.Empty);
            }
            else if (message == RenderWindowNative.DpiChanged && !_topologyInvalid)
            {
                // Leave WM_DPICHANGED to WPF for content DPI, then restore fullscreen pixel geometry.
                QueuePlacement();
            }
            else if (message == RenderWindowNative.WindowPosChanging && !_topologyInvalid && _target is not null && lParam != 0)
            {
                var position = Marshal.PtrToStructure<RenderWindowNative.WindowPos>(lParam);
                if ((position.Flags & RenderWindowNative.NoMove) == 0) { position.X = _bounds.Left; position.Y = _bounds.Top; }
                if ((position.Flags & RenderWindowNative.NoSize) == 0)
                {
                    position.Width = _bounds.Right - _bounds.Left;
                    position.Height = _bounds.Bottom - _bounds.Top;
                }
                Marshal.StructureToPtr(position, lParam, false);
            }
        }
        catch (Exception exception) { Fail(exception); }
        return 0;
    }

    private void QueuePlacement()
    {
        if (_pendingPlacement is { Status: DispatcherOperationStatus.Pending }) return;
        _pendingPlacement = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            try
            {
                if (_closed || _failed || _topologyInvalid || _target is null || _source is null) return;
                _bounds = _resolveBounds(_target);
                RenderWindowNative.PlacePixels(_source.Handle, _bounds);
                SceneContent.InvalidateVisual();
            }
            catch (Exception exception) { Fail(exception); }
        }));
    }

    private void Fail(Exception exception)
    {
        _logger.Error(exception, "Render window callback failed");
        if (_failed || _closed) return;
        _failed = true;
        if (_source is not null) RenderWindowNative.ShowWindow(_source.Handle, 0);
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            try { Faulted?.Invoke(exception); }
            catch (Exception subscriberException) { _logger.Error(subscriberException, "Render fault subscriber failed"); }
            finally { Dispose(); }
        }));
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _pendingPlacement?.Abort();
        SceneContent.RenderFailed -= Fail;
        SceneContent.Scene = null;
        SceneContent.DataContext = null;
        _source?.RemoveHook(WindowProc);
        _source = null;
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (_closed) return;
        try { Close(); }
        catch (Exception exception)
        {
            _logger.Error(exception, "Failed to close render window");
            if (_source is not null) RenderWindowNative.ShowWindow(_source.Handle, 0);
        }
    }
}
