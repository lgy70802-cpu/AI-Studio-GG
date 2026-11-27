using System.Diagnostics;

namespace DeskDisplay.Core.Models;

public sealed class RetroCameraClockScene : Scene
{
    private DateTimeOffset _currentTime = DateTimeOffset.Now;
    private DateTimeOffset _previousTime = DateTimeOffset.Now;
    private long _transitionStarted = Stopwatch.GetTimestamp();
    private double _progress = 1;
    private bool _animate = true;
    private bool _hour12;
    private int _durationMs = 440;
    private int _cropY = 145;

    public DateTimeOffset CurrentTime => _currentTime;
    public DateTimeOffset PreviousTime => _previousTime;
    public double AnimationProgress => _progress;
    public bool Animate { get => _animate; set { if (_animate == value) return; _animate = value; OnPropertyChanged(); } }
    public bool Hour12 { get => _hour12; set { if (_hour12 == value) return; _hour12 = value; OnPropertyChanged(); } }
    public int DurationMs { get => _durationMs; set { var next = Math.Clamp(value, 180, 1200); if (_durationMs == next) return; _durationMs = next; OnPropertyChanged(); } }
    public int CropY { get => _cropY; set { var next = Math.Clamp(value, 80, 230); if (_cropY == next) return; _cropY = next; OnPropertyChanged(); } }

    public RetroCameraClockScene() => SetCanvasSize(1920, 462);

    public void Tick(DateTimeOffset now, long timestamp)
    {
        if (now.ToUnixTimeSeconds() != _currentTime.ToUnixTimeSeconds())
        {
            _previousTime = _currentTime;
            _currentTime = now;
            _transitionStarted = timestamp;
            OnPropertyChanged(nameof(CurrentTime));
        }
        var elapsedMs = (timestamp - _transitionStarted) * 1000d / Stopwatch.Frequency;
        var next = Animate ? Math.Clamp(elapsedMs / DurationMs, 0, 1) : 1;
        if (Math.Abs(_progress - next) < 0.001) return;
        _progress = next;
        OnPropertyChanged(nameof(AnimationProgress));
    }

    public void Demonstrate(long timestamp)
    {
        _previousTime = _currentTime.AddSeconds(-1);
        _transitionStarted = timestamp;
        _progress = 0;
        OnPropertyChanged(nameof(PreviousTime));
        OnPropertyChanged(nameof(AnimationProgress));
    }
}
