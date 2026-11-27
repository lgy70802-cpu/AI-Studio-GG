using System.Globalization;

namespace DeskDisplay.Core.Models;

/// <summary>The single ClockScene instance is also the view model bound by both SceneViews.</summary>
public sealed class ClockScene : Scene
{
    private DateTimeOffset _currentTime;
    private static readonly string[] Weekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];
    public DateTimeOffset CurrentTime => _currentTime;
    public string TimeText => _currentTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public string DateText => _currentTime.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture);
    public string WeekdayText => Weekdays[(int)_currentTime.DayOfWeek];

    public void UpdateTime(DateTimeOffset time)
    {
        var wholeSecond = time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));
        if (_currentTime.EqualsExact(wholeSecond)) return;
        // Commit one timestamp before notifying: time, date and weekday always refer to the same sample.
        _currentTime = wholeSecond;
        OnPropertyChanged(nameof(CurrentTime));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(WeekdayText));
    }
}
