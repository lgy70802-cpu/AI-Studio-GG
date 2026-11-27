using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    StudioScheduleState scheduleState = new();
    readonly DispatcherTimer scheduleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    string? savedScheduleText;
    bool scheduleTickBusy;
    string ScheduleFile => Path.Combine(dataRoot, "schedule-state.json");
    void LoadSchedules()
    {
        if (File.Exists(ScheduleFile))
            scheduleState = JsonSerializer.Deserialize<StudioScheduleState>(File.ReadAllText(ScheduleFile))
                ?? throw new InvalidDataException("定时状态文件无效。");
        foreach (var entry in scheduleState.History.Where(entry => entry.Status == "执行中").ToArray())
            scheduleState.Finish(entry.Id, "失败", "上次执行中断，已保留触发凭据以避免重复执行");
    }
    void SaveSchedules()
    {
        var text = JsonSerializer.Serialize(scheduleState);
        if (text == savedScheduleText) return;
        StudioAtomicFile.Write(ScheduleFile, text);
        savedScheduleText = text;
    }
    List<StudioScheduleDefinition> ScheduleDefinitions() =>
        librarySnapshot?["schedules"]?.AsArray().Select(node => new StudioScheduleDefinition(
            node!["id"]!.GetValue<string>(), node["title"]?.GetValue<string>() ?? "定时规则",
            node["deviceId"]!.GetValue<string>(), node["orientation"]?.GetValue<string>() == "portrait",
            node["time"]!.GetValue<string>(), node["repeatMode"]!.GetValue<string>(),
            node["customDays"]?.AsArray().Select(day => day!.GetValue<int>()).ToArray() ?? [],
            node["actionType"]!.GetValue<string>(), node["themeId"]?.GetValue<string>(),
            node["playlistId"]?.GetValue<string>(), node["enabled"]?.GetValue<bool>() ?? false,
            node["targetBrightness"]?.GetValue<int>())).ToList() ?? [];
    string? ScheduleWarning(StudioScheduleDefinition rule)
    {
        if (rule.Clock == null) return "时间格式无效，请重新编辑规则";
        if (rule.RepeatMode == "custom" && rule.Days.Length == 0) return "请选择至少一个星期";
        if (!saved.ContainsKey(rule.DeviceId)) return "目标设备不存在，请重新绑定";
        if (rule.Action is not ("theme" or "playlist" or "sleep" or "wake")) return "不支持此定时动作";
        if (rule.Brightness is < 0 or > 100) return "画面亮度应为 0 至 100";
        if (rule.Action == "theme" && (rule.ThemeId == null || PlaylistImagePath(rule.ThemeId) == null))
            return "目标素材已回收、文件缺失或类型不支持，请重新绑定";
        if (rule.Action == "playlist")
        {
            var list = rule.PlaylistId is { } id ? FindPlaylist(id) : null;
            if (list == null) return "轮播列表已删除，请重新绑定";
            if (list.DeviceId != rule.DeviceId || list.Portrait != rule.Portrait) return "列表设备或方向不匹配，请重新绑定";
            if (Available(list).Count == 0) return "轮播列表暂无可播放图片或视频，请恢复素材";
        }
        return null;
    }
    object ScheduleSnapshot()
    {
        var rules = ScheduleDefinitions();
        scheduleState.Reconcile(rules, DateTime.Now);
        return new {
            enabled = scheduleState.Enabled, completed = scheduleState.Completed.ToArray(),
            history = scheduleState.History.Select(entry => new { id = entry.Id, title = entry.Title, ruleId = entry.RuleId, serial = entry.Serial, at = entry.At.ToString("yyyy-MM-ddTHH:mm:sszzz"), status = entry.Status, reason = entry.Reason, manual = entry.Manual }).ToArray(),
            rules = rules.Select(rule => new { id = rule.Id, nextAt = scheduleState.Next(rule, DateTime.Now)?.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                warning = ScheduleWarning(rule), conflict = rules.Any(other => other.Id != rule.Id && other.Enabled && rule.Enabled && !scheduleState.Completed.Contains(other.Id) && !scheduleState.Completed.Contains(rule.Id) &&
                    other.DeviceId == rule.DeviceId && other.Time == rule.Time && Enumerable.Range(0,7).Any(day => rule.Matches(DateTime.Today.AddDays(day)) && other.Matches(DateTime.Today.AddDays(day)))) }).ToArray()
        };
    }
    async Task ExecuteScheduled(StudioScheduleDefinition rule, bool manual, string? skip = null, string? reservedReceipt = null)
    {
        var receipt = reservedReceipt ?? scheduleState.Begin(rule, DateTime.Now, manual);
        SaveSchedules();
        try
        {
            var warning = skip ?? ScheduleWarning(rule);
            if (warning != null) { scheduleState.Finish(receipt, "跳过", warning); return; }
            if (!connectedSerials.Contains(rule.DeviceId))
            { scheduleState.Finish(receipt, "跳过", "设备离线，本次不补发；恢复连接后沿用设备原播放意图"); return; }
            await EndThemePreview(rule.DeviceId, true);
            var previous = saved[rule.DeviceId];
            var target = previous with { Portrait = rule.Portrait, Brightness = rule.Brightness ?? previous.Brightness };
            InvalidatePlaylistWork(rule.DeviceId);
            if (rule.Action is "sleep" or "wake")
            {
                await PowerCommand(rule.DeviceId, JsonSerializer.SerializeToElement(new {
                    action = rule.Action == "sleep" ? "standby" : "wake", brightness = rule.Brightness ?? previous.Brightness }));
            }
            else if (rule.Action == "playlist")
            {
                saved[rule.DeviceId] = target;
                try
                {
                    await PlaylistCommand("playlist-start", JsonSerializer.SerializeToElement(new {
                        serial = rule.DeviceId, playlistId = rule.PlaylistId }), connectedSerials);
                }
                catch { saved[rule.DeviceId] = previous; throw; }
            }
            else
            {
                var next = PreparePlaylistImage(target, rule.ThemeId!);
                await Start(next);
                saved[rule.DeviceId] = next;
                playlistProgress.Remove(rule.DeviceId);
                Save();
            }
            await RefreshDisplayPower();
            scheduleState.Finish(receipt, "成功", rule.Action is "sleep" or "wake" ? "软件黑屏/恢复画面请求已处理；背光保持" : rule.Action == "playlist" ? "已启动指定轮播，USB 首帧已确认" : "已切换内容，USB 首帧已确认");
        }
        catch (Exception ex) { scheduleState.Finish(receipt, "失败", ex.Message); }
        finally { SaveSchedules(); }
    }
    async Task TickSchedules()
    {
        if (closing || systemSuspended || librarySnapshot == null || scheduleTickBusy) return;
        scheduleTickBusy = true;
        await commands.WaitAsync();
        try
        {
            if (closing) return;
            var due = scheduleState.Collect(ScheduleDefinitions(), DateTime.Now);
            var prepared = due.Select(item => (Item: item, Receipt: scheduleState.Begin(item.Rule, DateTime.Now, false))).ToArray();
            // Persist all minute receipts and pending history before the first hardware side effect.
            SaveSchedules();
            foreach (var entry in prepared) await ExecuteScheduled(entry.Item.Rule, false, entry.Item.SkipReason, entry.Receipt);
        }
        catch (Exception ex) { Log(new { at = DateTimeOffset.Now, error = ex.Message, subsystem = "schedule" }); }
        finally { commands.Release(); scheduleTickBusy = false; }
    }
}
