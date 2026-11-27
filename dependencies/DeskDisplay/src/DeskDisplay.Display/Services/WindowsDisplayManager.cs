using System.Windows.Interop;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Interop;
using Serilog;

namespace DeskDisplay.Display.Services;

/// <summary>Create and dispose on the WPF UI thread. Enumeration runs off that thread.</summary>
public sealed class WindowsDisplayManager : IDisplayManager
{
    private readonly ILogger _logger;
    private readonly DisplaySnapshotReader _reader;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private HwndSource? _messageWindow;
    private bool _disposed;

    public event EventHandler? DisplaysChanged;

    public WindowsDisplayManager(ILogger logger)
    {
        _logger = logger;
        _reader = new DisplaySnapshotReader(logger);
        _dispatcher = Dispatcher.CurrentDispatcher;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(400) };
        _debounce.Tick += OnDebounceTick;
        try
        {
            // Hidden top-level window: message-only HWNDs do not receive broadcast display messages.
            _messageWindow = new HwndSource(new HwndSourceParameters("DeskDisplay.DisplayNotifications")
            {
                Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000), ParentWindow = 0
            });
            _messageWindow.AddHook(WindowProc);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Cannot subscribe to display changes; manual refresh remains available");
            _messageWindow?.Dispose();
            _messageWindow = null;
        }
    }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.Run(() =>
        {
            try
            {
                var snapshot = _reader.Read(cancellationToken);
                _logger.Information("Enumerated {DisplayCount} active displays", snapshot.Count);
                return snapshot;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.Error(exception, "Display enumeration failed");
                throw;
            }
        }, cancellationToken);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        try
        {
            if (!_disposed && (message == NativeMethods.DisplayChange ||
                (message == NativeMethods.DeviceChange && wParam.ToInt64() is 7 or 0x8000 or 0x8004)))
            {
                _debounce.Stop();
                _debounce.Start();
            }
        }
        catch (Exception exception) { _logger.Error(exception, "Failed to process display notification {Message}", message); }
        return 0;
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (_disposed) return;
        _logger.Information("Windows display configuration changed");
        try { DisplaysChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception) { _logger.Error(exception, "Display change subscriber failed"); }
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnDebounceTick;
        if (_messageWindow is not null)
        {
            _messageWindow.RemoveHook(WindowProc);
            _messageWindow.Dispose();
            _messageWindow = null;
        }
        DisplaysChanged = null;
    }
}
