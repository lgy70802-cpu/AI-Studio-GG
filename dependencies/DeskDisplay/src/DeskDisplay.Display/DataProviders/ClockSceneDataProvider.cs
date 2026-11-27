using System.Windows.Threading;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Display.DataProviders;

/// <summary>Application-owned clock sampling. No host or view creates its own timer.</summary>
public sealed class ClockSceneDataProvider : IDisposable
{
    private readonly ClockScene _scene;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    public bool IsRunning => _timer.IsEnabled;

    public ClockSceneDataProvider(ClockScene scene, ILogger logger, TimeProvider? timeProvider = null)
    {
        _scene = scene;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_timer.IsEnabled) return;
        Sample();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) => Sample();

    private void Sample()
    {
        if (_disposed) return;
        try { _scene.UpdateTime(_timeProvider.GetLocalNow()); }
        catch (Exception exception) { _logger.Error(exception, "Clock sampling failed; preserving the last scene timestamp"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
