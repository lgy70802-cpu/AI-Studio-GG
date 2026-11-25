using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed class ScheduleDemoClock : IClock
    {
        readonly IClock source = new SystemClock();
        public TimeSpan Offset { get; set; }
        public DateTimeOffset GetLocalNow() => source.GetLocalNow() + Offset;
    }

    readonly ScheduleDemoClock demoScheduleClock = new();
    TextBlock? scheduleTimeText, scheduleDayText, scheduleNextText;
    Button? scheduleJump, scheduleReset;

    void RenderScheduleClock()
    {
        var panel = new StackPanel();
        var clock = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        clock.Children.Add(HomeText(IsPlaylistDemo ? "演示时钟" : "当前时间", 13, muted: true));
        scheduleTimeText = HomeText("", 30, bold: true);
        scheduleTimeText.Margin = new Thickness(14, 0, 14, 0);
        clock.Children.Add(scheduleTimeText);
        scheduleDayText = HomeText("", 13, muted: true);
        clock.Children.Add(scheduleDayText);
        panel.Children.Add(clock);
        scheduleNextText = HomeText("", 13, muted: true);
        scheduleNextText.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(scheduleNextText);
        scheduleJump = scheduleReset = null;
        if (IsPlaylistDemo)
        {
            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            actions.Children.Add(HomeButton("+1 小时", () => ShiftScheduleDemo(TimeSpan.FromHours(1)), "SmallButton"));
            scheduleJump = HomeButton("快进到下一次触发", () =>
            {
                var next = NextScheduleOnPage();
                if (next != null) ShiftScheduleDemo(next.Value - state.LocalTime.DateTime);
            }, "SmallButton");
            actions.Children.Add(scheduleJump);
            scheduleReset = HomeButton("回到真实时间", () =>
            {
                demoScheduleClock.Offset = TimeSpan.Zero;
                Render();
            }, "LinkButton");
            actions.Children.Add(scheduleReset);
            panel.Children.Add(actions);
        }
        Body.Children.Add(HomeCard(panel));
        UpdateSchedulePageClock();
    }

    DateTime? NextScheduleOnPage() => state.Schedules.Select(state.NextSchedule).Where(time => time.HasValue)
        .OrderBy(time => time).FirstOrDefault();

    void UpdateSchedulePageClock()
    {
        if (page != "定时切换" || backgroundHardwareContext || scheduleTimeText == null) return;
        var now = state.LocalTime;
        scheduleTimeText.Text = now.ToString("HH:mm");
        scheduleDayText!.Text = $"{now:yyyy-MM-dd} · 周{"日一二三四五六"[(int)now.DayOfWeek]}";
        var next = NextScheduleOnPage();
        scheduleNextText!.Text = next.HasValue ? $"下一次触发 · {next:MM-dd HH:mm}" : "暂无待执行的规则";
        if (scheduleJump != null) scheduleJump.IsEnabled = next.HasValue;
        if (scheduleReset != null) scheduleReset.Visibility = demoScheduleClock.Offset == TimeSpan.Zero ?
            Visibility.Collapsed : Visibility.Visible;
    }

    void ShiftScheduleDemo(TimeSpan offset)
    {
        if (!IsPlaylistDemo) return;
        demoScheduleClock.Offset += offset;
        state.TickSchedules(ExecuteHardwareSchedule, TakeHardwareScheduleReceipt);
        SaveWorkspace();
        Render();
    }
}
