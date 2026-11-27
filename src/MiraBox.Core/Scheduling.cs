namespace MiraBox.Core;

public interface IClock
{
    DateTimeOffset GetLocalNow();
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset GetLocalNow() => TimeProvider.System.GetLocalNow();
}

public enum ScheduleAction { Playlist, Content, Stop }

public sealed record ScheduleRule(Guid Id, string DeviceId, DayOfWeek[] Days, TimeOnly Time,
    ScheduleAction Action, string? TargetId, bool Enabled = true, string Remark = "")
{
    public string Name => string.IsNullOrWhiteSpace(Remark)
        ? $"{string.Join("、", Days.Order().Select(day => "周" + "日一二三四五六"[(int)day]))} {Time:HH:mm}" : Remark;
}

public sealed record ScheduleExecution(DateTimeOffset At, string RuleName, bool Succeeded, string Reason)
{
    public Guid? Receipt { get; init; }
    public string? TransferState { get; init; }
}

public sealed partial class Workspace
{
    readonly IClock scheduleClock;
    readonly HashSet<(Guid Rule, DateTime Minute)> firedSchedules = [];
    DateTime lastScheduleMinute;

    public Workspace(IClock? clock = null, LibraryStore? library = null)
    {
        usageLibrary = library;
        scheduleClock = clock ?? new SystemClock();
        lastScheduleMinute = ScheduleMinute(scheduleClock.GetLocalNow());
    }

    public List<ScheduleRule> Schedules { get; } = [];
    public List<ScheduleExecution> ScheduleHistory { get; } = [];
    public DateTimeOffset LocalTime => scheduleClock.GetLocalNow();

    static DateTime ScheduleMinute(DateTimeOffset now) => now.Date.AddHours(now.Hour).AddMinutes(now.Minute);

    public DateTime? NextSchedule(ScheduleRule rule)
    {
        if (!rule.Enabled) return null;
        var now = scheduleClock.GetLocalNow().DateTime;
        for (var day = 0; day < 8; day++)
        {
            var candidate = now.Date.AddDays(day).Add(rule.Time.ToTimeSpan());
            if (candidate > now && rule.Days.Contains(candidate.DayOfWeek) &&
                !firedSchedules.Contains((rule.Id, candidate))) return candidate;
        }
        return null;
    }

    public bool ScheduleConflict(ScheduleRule rule) => Schedules.Any(other => other.Id != rule.Id &&
        other.DeviceId == rule.DeviceId && other.Time == rule.Time && other.Days.Intersect(rule.Days).Any());

    public void SaveSchedule(ScheduleRule rule, Func<ScheduleRule, string?>? validate = null)
    {
        if (rule.Days.Length == 0 || rule.Days.Any(day => !Enum.IsDefined(day)))
            throw new ArgumentException("请至少选择一天。");
        if (rule.Time.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("请输入 HH:mm 格式的时间。");
        var warning = validate == null ? ScheduleWarning(rule, checkPlayback: false) : validate(rule);
        if (warning != null) throw new ArgumentException(warning);
        var index = Schedules.FindIndex(saved => saved.Id == rule.Id);
        var copy = rule with { Days = rule.Days.Distinct().Order().ToArray() };
        if (index < 0) Schedules.Add(copy); else Schedules[index] = copy;
    }

    public string? ScheduleWarning(ScheduleRule rule, bool checkPlayback = true)
    {
        if (!Devices.TryGetValue(rule.DeviceId, out var device)) return "设备不存在";
        switch (rule.Action)
        {
            case ScheduleAction.Stop: return null;
            case ScheduleAction.Playlist:
                var list = Playlists.FirstOrDefault(p => p.Id == rule.TargetId);
                if (list == null) return "内容已删除（轮播列表）";
                if (list.DeviceId != device.Id) return "轮播列表不属于此设备";
                return checkPlayback && !list.Items.Any(item => Eligible(item.ResourceId, device))
                    ? "轮播列表没有适合此设备的内容" : null;
            case ScheduleAction.Content:
                if (rule.TargetId == null || !Resources.TryGetValue(rule.TargetId, out var resource) || !resource.Local)
                    return "内容已删除";
                return Eligible(rule.TargetId, device) ? null : "内容与设备当前方向不适配";
            default: return "未知定时动作";
        }
    }

    public bool TickSchedules(Func<ScheduleRule, (bool Succeeded, string Reason)?>? route = null, Func<Guid?>? takeReceipt = null)
    {
        var now = scheduleClock.GetLocalNow();
        var minute = ScheduleMinute(now);
        if (minute == lastScheduleMinute) return false;
        lastScheduleMinute = minute;
        firedSchedules.RemoveWhere(item => item.Minute < minute.AddDays(-7));
        var ran = false;
        foreach (var rule in Schedules.Where(rule => rule.Enabled && rule.Days.Contains(minute.DayOfWeek) &&
            rule.Time == TimeOnly.FromDateTime(minute)).ToArray())
        {
            if (!firedSchedules.Add((rule.Id, minute))) continue;
            ExecuteSchedule(rule, now, route, takeReceipt);
            ran = true;
        }
        return ran;
    }

    public ScheduleExecution RunScheduleNow(ScheduleRule rule, Func<ScheduleRule, (bool Succeeded, string Reason)?>? route = null,
        Func<Guid?>? takeReceipt = null) =>
        ExecuteSchedule(rule, scheduleClock.GetLocalNow(), route, takeReceipt);

    ScheduleExecution ExecuteSchedule(ScheduleRule rule, DateTimeOffset now,
        Func<ScheduleRule, (bool Succeeded, string Reason)?>? route, Func<Guid?>? takeReceipt)
    {
        if (route?.Invoke(rule) is { } external)
        {
            var receipt = takeReceipt?.Invoke();
            return RecordScheduleResult(new(now, rule.Name, external.Succeeded, external.Reason)
            {
                Receipt = receipt,
                TransferState = receipt.HasValue ? "已提交发送" : null
            });
        }
        if (Preview?.DeviceId == rule.DeviceId) Preview = null;
        var warning = Devices.TryGetValue(rule.DeviceId, out var device) && !device.Online
            ? "设备离线" : ScheduleWarning(rule);
        var reason = warning ?? (rule.Action switch
        {
            ScheduleAction.Playlist => "已开始轮播",
            ScheduleAction.Content => "已显示内容",
            _ => "已停止输出"
        });
        if (warning == null)
        {
            switch (rule.Action)
            {
                case ScheduleAction.Playlist:
                    Start(Playlists.First(p => p.Id == rule.TargetId));
                    break;
                case ScheduleAction.Content:
                    ApplyDirect(rule.DeviceId, rule.TargetId!, preserveOtherPreview: true);
                    NoteUse(rule.TargetId!);
                    break;
                case ScheduleAction.Stop:
                    device!.Running = false;
                    foreach (var list in Playlists.Where(p => p.DeviceId == rule.DeviceId)) list.Pause();
                    break;
            }
        }
        return RecordScheduleResult(new(now, rule.Name, warning == null, reason));
    }

    ScheduleExecution RecordScheduleResult(ScheduleExecution result)
    {
        ScheduleHistory.Insert(0, result);
        if (ScheduleHistory.Count > 20) ScheduleHistory.RemoveRange(20, ScheduleHistory.Count - 20);
        return result;
    }
}
