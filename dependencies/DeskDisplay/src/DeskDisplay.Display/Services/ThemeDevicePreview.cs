using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.Services;

/// <summary>Owns preview resources only: no device lease, driver or hardware output is created.</summary>
public sealed class ThemeDevicePreview(ThemeScene scene, Action tick, Func<ValueTask> release) : IAsyncDisposable
{
    private bool _disposed;
    private Task? _release;
    public ThemeScene Scene { get; } = scene;
    public void Tick() { if (!_disposed) tick(); }
    public ValueTask DisposeAsync() { _disposed = true; return new(_release ??= ReleaseAsync()); }
    private async Task ReleaseAsync() => await release();
}
