using System.Diagnostics;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Display.DataProviders;

public sealed class PerformanceSceneDataProvider : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly ISensorService _sensors;
    private readonly PerformanceScene _scene;
    private readonly ILogger _logger;
    private readonly Func<bool> _isVisible;
    public PerformanceSceneDataProvider(ISensorService sensors, PerformanceScene scene, ILogger logger, Func<bool>? isVisible = null)
    {
        _sensors = sensors; _scene = scene; _logger = logger;
        _isVisible = isVisible ?? (() => true);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1d / 30) };
        _timer.Tick += OnTick;
        _timer.Start();
    }
    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            if (!_isVisible()) return;
            var snapshot = _sensors.Current;
            if (!_scene.IsSettled(snapshot)) _scene.Update(snapshot, _elapsed.Elapsed);
        }
        catch (Exception exception) { _logger.Error(exception, "Performance scene update failed"); }
        finally { _elapsed.Restart(); }
    }
    public void Dispose() { _timer.Stop(); _timer.Tick -= OnTick; }
}
