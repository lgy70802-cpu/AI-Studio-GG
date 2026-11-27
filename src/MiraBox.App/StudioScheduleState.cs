namespace MiraBox.App;

public sealed record StudioScheduleDefinition(string Id, string Title, string DeviceId, bool Portrait,
    string Time, string RepeatMode, int[] Days, string Action, string? ThemeId, string? PlaylistId,
    bool Enabled, int? Brightness = null)
{
    public bool Matches(DateTime day) => RepeatMode switch
    {
        "daily" or "once" => true,
        "weekdays" => day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
        "weekends" => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        "custom" => Days.Contains((int)day.DayOfWeek),
        _ => false
    };
    public TimeSpan? Clock => TimeOnly.TryParseExact(Time, "HH:mm", out var time) ? time.ToTimeSpan() : null;
    public string Fingerprint => string.Join("|", Title, DeviceId, Portrait, Time, RepeatMode,
        string.Join(",", Days.Order()), Action, ThemeId, PlaylistId, Brightness);
}
public sealed record StudioScheduleHistory(string Id, string RuleId, string Title, string Serial,
    DateTime At, string Status, string Reason, bool Manual);
public sealed record StudioScheduleDue(StudioScheduleDefinition Rule, DateTime At, string? SkipReason);

public sealed class StudioScheduleState
{
    public bool Enabled { get; set; }
    public DateTime? LastChecked { get; set; }
    public Dictionary<string, DateTime> OnceAt { get; set; } = new();
    public Dictionary<string, string> Versions { get; set; } = new();
    public HashSet<string> Completed { get; set; } = new();
    public HashSet<string> Receipts { get; set; } = new();
    public List<StudioScheduleHistory> History { get; set; } = new();

    public void Reconcile(IEnumerable<StudioScheduleDefinition> rules, DateTime now)
    {
        foreach (var rule in rules)
        {
            if (Versions.GetValueOrDefault(rule.Id) == rule.Fingerprint) continue;
            Versions[rule.Id] = rule.Fingerprint;
            Completed.Remove(rule.Id);
            OnceAt.Remove(rule.Id);
            if (rule.RepeatMode == "once" && rule.Clock is { } clock)
            {
                var at = now.Date + clock;
                OnceAt[rule.Id] = at >= Minute(now) ? at : at.AddDays(1);
            }
        }
    }
    public void Reset(StudioScheduleDefinition rule, DateTime now)
    {
        Versions.Remove(rule.Id);
        Reconcile([rule], now);
        if (OnceAt.TryGetValue(rule.Id, out var at) && Receipts.Contains(Key(rule, at))) OnceAt[rule.Id] = at.AddDays(1);
    }
    public static DateTime Minute(DateTime at) => at.Date.AddHours(at.Hour).AddMinutes(at.Minute);
    public DateTime? Next(StudioScheduleDefinition rule, DateTime now)
    {
        if (!Enabled || !rule.Enabled || Completed.Contains(rule.Id) || rule.Clock is not { } clock) return null;
        if (rule.RepeatMode == "once") return OnceAt.GetValueOrDefault(rule.Id);
        for (var day = 0; day <= 7; day++)
        {
            var at = now.Date.AddDays(day) + clock;
            if (at >= Minute(now) && rule.Matches(at) && !Receipts.Contains(Key(rule, at))) return at;
        }
        return null;
    }
    static string Key(StudioScheduleDefinition rule, DateTime at) => rule.Id + "/" + at.ToString("yyyy-MM-dd HH:mm");
    public List<StudioScheduleDue> Collect(IReadOnlyList<StudioScheduleDefinition> rules, DateTime now)
    {
        Reconcile(rules, now);
        var result = new List<StudioScheduleDue>();
        var minute = Minute(now);
        var previous = LastChecked;
        LastChecked = now;
        if (!Enabled) return result;
        var winners = new HashSet<string>();
        foreach (var rule in rules.Where(rule => rule.Enabled && !Completed.Contains(rule.Id)))
        {
            if (rule.Clock is not { } clock) continue;
            DateTime? due = null;
            if (rule.RepeatMode == "once")
            {
                if (OnceAt.TryGetValue(rule.Id, out var once) && once <= now) due = once;
            }
            else for (var day = 0; day <= 7; day++)
            {
                var candidate = now.Date.AddDays(-day) + clock;
                if (candidate <= now && rule.Matches(candidate)) { due = candidate; break; }
            }
            if (due is not { } at || at < minute && rule.RepeatMode != "once" && (previous == null || at <= previous) ||
                !Receipts.Add(Key(rule, at))) continue;
            var skipped = at < minute ? "错过执行时间，已跳过" :
                !winners.Add(rule.DeviceId) ? "同设备同一分钟存在冲突，按规则顺序仅执行第一条" : null;
            if (rule.RepeatMode == "once") Completed.Add(rule.Id);
            result.Add(new(rule, at, skipped));
        }
        // Receipts are compact per-rule local-minute keys, retained across restart and clock rollback.
        return result;
    }
    public string Begin(StudioScheduleDefinition rule, DateTime at, bool manual)
    {
        var id = Guid.NewGuid().ToString("N");
        History.Insert(0, new(id, rule.Id, rule.Title, rule.DeviceId, at, "执行中", "等待设备首帧确认", manual));
        if (History.Count > 100) History.RemoveRange(100, History.Count - 100);
        return id;
    }
    public void Finish(string id, string status, string reason)
    {
        var index = History.FindIndex(entry => entry.Id == id);
        if (index >= 0) History[index] = History[index] with { Status = status, Reason = reason };
    }
}
