using System.ComponentModel;

namespace DeskDisplay.Core.Models;

public enum DashboardLayout
{
    ClockLeft,
    ClockCenter,
    Split
}

/// <summary>One shared scene combining a bounded image with live clock and sensor models.</summary>
public sealed class DashboardScene : Scene, IDisposable
{
    private ImageScene? _background;
    private DashboardLayout _layout = DashboardLayout.Split;
    private bool _showPerformance = true;
    private bool _disposed;

    public ClockScene Clock { get; }
    public PerformanceScene Performance { get; }
    public ImageScene? Background => _background;
    public DashboardLayout Layout
    {
        get => _layout;
        set { if (_layout == value) return; _layout = value; OnPropertyChanged(); }
    }
    public bool ShowPerformance
    {
        get => _showPerformance;
        set { if (_showPerformance == value) return; _showPerformance = value; OnPropertyChanged(); }
    }

    public DashboardScene(ClockScene clock, PerformanceScene performance)
    {
        Clock = clock;
        Performance = performance;
        Clock.PropertyChanged += OnChildChanged;
        Performance.PropertyChanged += OnChildChanged;
    }

    public void SetBackground(ImageScene background)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_background, background)) return;
        if (_background is not null) _background.PropertyChanged -= OnChildChanged;
        _background = background;
        _background.PropertyChanged += OnChildChanged;
        OnPropertyChanged(nameof(Background));
    }

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (ReferenceEquals(sender, Clock))
        {
            if (e.PropertyName == nameof(ClockScene.CurrentTime)) OnPropertyChanged(nameof(Clock));
            return;
        }
        if (ReferenceEquals(sender, Performance))
        {
            if (e.PropertyName == nameof(PerformanceScene.Values)) OnPropertyChanged(nameof(Performance));
            return;
        }
        OnPropertyChanged(nameof(Background));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clock.PropertyChanged -= OnChildChanged;
        Performance.PropertyChanged -= OnChildChanged;
        if (_background is not null) _background.PropertyChanged -= OnChildChanged;
    }
}
