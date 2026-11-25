using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Sensors;

internal interface ISensorReader : IDisposable
{
    SensorSnapshot Read();
}

public sealed class SensorService : ISensorService, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Func<ISensorReader> _factory;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _stop;
    private Task? _worker;
    private SensorSnapshot _current = new();
    private bool _disposed;
    public SensorSnapshot Current => Volatile.Read(ref _current);

    public SensorService(ILogger logger) : this(logger, () => new LibreHardwareSensorReader(logger), TimeSpan.FromMilliseconds(1000)) { }
    internal SensorService(ILogger logger, Func<ISensorReader> factory, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        _logger = logger; _factory = factory; _interval = interval;
    }
    public async Task StartAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker is not null) return;
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _worker = Task.Run(() => SampleAsync(token));
        }
        finally { _gate.Release(); }
    }
    private async Task SampleAsync(CancellationToken token)
    {
        ISensorReader? reader = null;
        var faulted = false;
        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    reader ??= _factory();
                    Volatile.Write(ref _current, reader.Read());
                    faulted = false;
                }
                catch (Exception exception)
                {
                    if (!faulted) _logger.Error(exception, "Sensor sampling failed; publishing unavailable values");
                    faulted = true;
                    Volatile.Write(ref _current, new SensorSnapshot());
                }
            } while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            try { reader?.Dispose(); }
            catch (Exception exception) { _logger.Error(exception, "Sensor reader shutdown failed"); }
            Volatile.Write(ref _current, new SensorSnapshot());
        }
    }
    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task StopCoreAsync()
    {
        _stop?.Cancel();
        if (_worker is not null) await _worker.ConfigureAwait(false);
        _worker = null;
        _stop?.Dispose(); _stop = null;
    }
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { _disposed = true; await StopCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}
