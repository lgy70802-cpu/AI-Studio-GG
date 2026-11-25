using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    object? pendingHardwareScheduleStop;
    Guid? hardwareScheduleReceipt;

    const string HardwareSchedulePrefix = "usb:d92:";

    static bool IsHardwareSchedule(string id) => id.StartsWith(HardwareSchedulePrefix, StringComparison.Ordinal);

    string ScheduleDeviceLabel(string id) => IsHardwareSchedule(id)
        ? $"{HardwareModelFor(id[HardwareSchedulePrefix.Length..])} 实机 · {id[HardwareSchedulePrefix.Length..]}" : DeviceName(id);

    string? HardwareScheduleWarning(ScheduleRule rule)
    {
        if (!IsHardwareSchedule(rule.DeviceId)) return state.ScheduleWarning(rule);
        if (rule.DeviceId.Length == HardwareSchedulePrefix.Length) return "D92 序列号无效";
        var serial = rule.DeviceId[HardwareSchedulePrefix.Length..];
        if (serial != hardwareApplySerial)
            return InHardwareSession(serial, () => HardwareScheduleWarning(rule));
        return rule.Action switch
        {
            ScheduleAction.Stop => null,
            ScheduleAction.Playlist => state.Playlists.Find(list => list.Id == rule.TargetId) is not { } playlist
                ? "内容已删除（轮播列表）" : HardwarePlaylistWarning(playlist),
            ScheduleAction.Content when rule.TargetId == null ||
                state.Resources.GetValueOrDefault(rule.TargetId) is not { Local: true } => "目标已删除",
            ScheduleAction.Content => HardwarePlaylistIssue(state.Resources[rule.TargetId!]),
            _ => "未知定时动作"
        };
    }

    string? HardwarePlaylistWarning(Playlist playlist)
    {
        if (IsHardwareSchedule(playlist.DeviceId) && playlist.DeviceId != HardwareSchedulePrefix + hardwareApplySerial)
            return "轮播属于另一台 D92";
        if (HardwarePlaylistItems(playlist).Length != 0) return null;
        return playlist.Items.Any(item => state.Resources.TryGetValue(item.ResourceId, out var resource) &&
            HardwarePlaylistIssue(resource) == "视频文件已丢失")
            ? "轮播视频文件已丢失，列表没有其他可用内容"
            : "轮播没有适合 D92 当前方向且可用的本地内容";
    }

    (bool Succeeded, string Reason)? ExecuteHardwareSchedule(ScheduleRule rule)
    {
        hardwareScheduleReceipt = null;
        if (!IsHardwareSchedule(rule.DeviceId)) return null;
        if (hardwareApplySerial == null || rule.DeviceId != HardwareSchedulePrefix + hardwareApplySerial)
        {
            var serial = rule.DeviceId[HardwareSchedulePrefix.Length..];
            if (serial.Length == 0 || !hardwareSessions.ContainsKey(serial) && !hardwareDeviceNames.ContainsKey(serial))
                return (false, "规则指定的实机尚未添加");
            return InHardwareSession(serial, () => ExecuteHardwareSchedule(rule));
        }
        try
        {
            if (hardwarePreviewEnds != null) RestoreHardwareStill();
            if (!HardwarePresent(hardwareApplySerial))
                return (false, "D92 实机离线");
            if (HardwareScheduleWarning(rule) is { } warning) return (false, warning);
            if (rule.Action == ScheduleAction.Stop)
            {
                var request = pendingHardwareScheduleStop = new object();
                var serial = hardwareApplySerial;
                _ = Dispatcher.InvokeAsync(() => InHardwareSession(serial, () =>
                {
                    if (!ReferenceEquals(pendingHardwareScheduleStop, request)) return;
                    pendingHardwareScheduleStop = null;
                    PauseHardwarePlaylist();
                    hardwarePreviewEnds = null;
                    _ = StopHardwareOutput();
                }));
                return (true, "已请求停止 D92 实机输出");
            }
            if (rule.Action == ScheduleAction.Content)
            {
                UseHardwareContent(state.Resources[rule.TargetId!]);
                hardwareScheduleReceipt = hardwareOutput?.RequestedSubmissionId;
                pendingHardwareScheduleStop = null;
                return (true, "内容已交给 D92 实机发送");
            }
            StartHardwarePlaylist(state.Playlists.First(list => list.Id == rule.TargetId));
            hardwareScheduleReceipt = hardwareOutput?.RequestedSubmissionId;
            pendingHardwareScheduleStop = null;
            return (true, "轮播已交给 D92 实机发送");
        }
        catch (Exception ex)
        {
            return (false, "发送失败：" + ex.Message);
        }
    }
    Guid? TakeHardwareScheduleReceipt()
    {
        var receipt = hardwareScheduleReceipt;
        hardwareScheduleReceipt = null;
        return receipt;
    }

    void TrackHardwareSchedule(D92Diagnostic entry)
    {
        if (entry.SubmissionId is not { } id) return;
        var changed = false;
        for (var i = 0; i < state.ScheduleHistory.Count; i++)
        {
            var row = state.ScheduleHistory[i];
            if (row.Receipt == null) continue;
            if (row.Receipt == id)
            {
                var phase = entry.Event switch
                {
                    "usb-first-write" => "USB 已写入首帧",
                    "Failed" or "BlockedByCraft" or "video-failed" => "发送失败",
                    "WaitingForDevice" when row.TransferState != "USB 已写入首帧" => "等待同一设备重连",
                    "Stopped" when row.TransferState is "已提交发送" or "等待同一设备重连" => "首帧前已停止",
                    _ => null
                };
                if (phase == null || phase == row.TransferState) continue;
                state.ScheduleHistory[i] = row with
                {
                    TransferState = phase,
                    Succeeded = phase is not ("发送失败" or "首帧前已停止"),
                    Reason = entry.Detail ?? row.Reason
                };
                changed = true;
            }
            else if (entry.Event == "submitted" && hardwareOutput?.SubmissionId == id &&
                     row.TransferState is "已提交发送" or "等待同一设备重连")
            {
                state.ScheduleHistory[i] = row with { TransferState = "被后续操作替代", Succeeded = false };
                changed = true;
            }
        }

        if (!changed) return;
        SaveWorkspace();
        if (page == "定时切换") Render();
    }
}
