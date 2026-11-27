using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    static readonly string[] ScheduleActions = ["播放轮播列表", "显示单个内容", "停止输出"];

    void RenderSchedules()
    {
        var create = HomeButton("＋ 新建规则", () => EditSchedule(null), "PrimaryButton");
        AddServicePageHeading("定时切换", "按时间自动切换设备内容。手动操作保留到下一次触发，错过的触发不会补执行。", create);
        RenderScheduleClock();
        if (state.Schedules.Count == 0)
        {
            var empty = PlaylistEmpty("还没有定时规则", "新建规则", () => EditSchedule(null));
            empty.Margin = new Thickness(0, 16, 0, 0);
            Body.Children.Add(empty);
        }
        foreach (var rule in state.Schedules)
        {
            var panel = new StackPanel();
            var toggle = new CheckBox { Content = rule.Enabled ? "已启用" : "已停用", IsChecked = rule.Enabled };
            toggle.SetResourceReference(StyleProperty, "Switch");
            AutomationProperties.SetName(toggle, $"启用定时规则 {rule.Name}");
            toggle.Click += (_, _) =>
            {
                try
                {
                    state.Schedules[state.Schedules.FindIndex(saved => saved.Id == rule.Id)] =
                        rule with { Enabled = toggle.IsChecked == true };
                    SaveWorkspace();
                    Render();
                }
                catch (Exception ex) { Notice.Text = ex.Message; }
            };
            var name = rule.Action == ScheduleAction.Playlist
                ? state.Playlists.FirstOrDefault(p => p.Id == rule.TargetId)?.Name
                : rule.Action == ScheduleAction.Content && rule.TargetId != null &&
                  state.Resources.GetValueOrDefault(rule.TargetId) is { Local: true } resource ? resource.Name : null;
            var targetName = rule.Action == ScheduleAction.Stop ? "" : $" · {name ?? "内容已删除"}";
            var actionLabel = ScheduleActions[(int)rule.Action] + targetName;
            var hasRemark = !string.IsNullOrWhiteSpace(rule.Remark);
            panel.Children.Add(HomeHeader(HomeText(hasRemark ? rule.Name : actionLabel, 16, bold: true), toggle));
            panel.Children.Add(HomeText($"{ScheduleDays(rule.Days)} {rule.Time:HH:mm} · {ScheduleDeviceLabel(rule.DeviceId)}", 13, muted: true));
            if (hasRemark) panel.Children.Add(HomeText(actionLabel));
            var next = state.NextSchedule(rule);
            panel.Children.Add(HomeText(next.HasValue ? $"下一次 {next:MM-dd HH:mm}" : "无下一次触发", 13, muted: true));
            var warnings = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
            if (state.Devices.GetValueOrDefault(rule.DeviceId) is { Online: false })
                warnings.Children.Add(PlaylistBadge("设备离线 · 触发时跳过", "#FFF4E5", "#B54708"));
            if (HardwareScheduleWarning(rule) is { } warning)
                warnings.Children.Add(PlaylistBadge(warning, "#FFF4E5", "#B54708"));
            if (rule.Enabled && state.ScheduleConflict(rule))
                warnings.Children.Add(PlaylistBadge("时间冲突", "#FFF4E5", "#B54708"));
            if (warnings.Children.Count > 0) panel.Children.Add(warnings);
            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            actions.Children.Add(HomeButton("立即执行", () =>
            {
                state.RunScheduleNow(rule, ExecuteHardwareSchedule, TakeHardwareScheduleReceipt);
                SaveWorkspace();
                Render();
            }, "PillButton"));
            actions.Children.Add(HomeButton("编辑", () => EditSchedule(rule), "LinkButton"));
            actions.Children.Add(HomeButton("删除", () =>
            {
                if (MessageBox.Show(this, $"删除定时规则「{rule.Name}」？", "删除定时规则", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                state.Schedules.RemoveAll(saved => saved.Id == rule.Id);
                SaveWorkspace();
                Render();
            }, "LinkButton"));
            panel.Children.Add(actions);
            var card = HomeCard(panel);
            card.Margin = new Thickness(0, 10, 0, 0);
            Body.Children.Add(card);
        }
        var history = new StackPanel();
        history.Children.Add(HomeText("最近执行", 18, bold: true));
        if (state.ScheduleHistory.Count == 0) history.Children.Add(HomeText("还没有执行记录。", 13, muted: true));
        foreach (var item in state.ScheduleHistory)
        {
            var result = item.TransferState ?? (item.Succeeded ? "成功" : item.Reason.StartsWith("发送失败") ? "发送失败" : "跳过");
            var line = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            line.Children.Add(PlaylistBadge(result, item.Succeeded ? "#E2F7EF" : "#FFF4E5", item.Succeeded ? "#067647" : "#B54708"));
            line.Children.Add(HomeText($"{item.At:MM-dd HH:mm} · {item.RuleName} · {item.Reason}", 13));
            history.Children.Add(line);
        }
        var historyCard = HomeCard(history);
        historyCard.Margin = new Thickness(0, 12, 0, 0);
        Body.Children.Add(historyCard);
        var note = HomeText("规则按设备独立执行。同一时间按创建顺序依次执行，后者生效；触发时取消该设备的临时预览。", 12, muted: true);
        note.Margin = new Thickness(0, 12, 0, 0);
        Body.Children.Add(note);
    }

    static string ScheduleDays(DayOfWeek[] days) => days.Length == 7 ? "每天" :
        days.Length == 5 && days.All(day => day is >= DayOfWeek.Monday and <= DayOfWeek.Friday) ? "工作日" :
        days.Length == 2 && days.All(day => day is DayOfWeek.Saturday or DayOfWeek.Sunday) ? "周末" :
        string.Join("、", days.Select(day => "周" + "日一二三四五六"[(int)day]));

    void EditSchedule(ScheduleRule? original)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog(original == null ? "新建定时规则" : "编辑定时规则", panel, 660, 720);
        dialog.MinWidth = 500;
        dialog.MinHeight = 480;
        var options = state.Devices.Values.Select(device => KeyValuePair.Create(device.Id, device.Name)).ToList();
        if (hardwareApplySerial != null) options.Add(KeyValuePair.Create(HardwareSchedulePrefix + hardwareApplySerial, HardwareTargetLabel));
        foreach (var serial in hardwareDeviceNames.Keys.Where(serial => serial != hardwareApplySerial))
            options.Add(KeyValuePair.Create(HardwareSchedulePrefix + serial, HardwareDeviceName(serial) + " · " + serial));
        if (original is { } saved && IsHardwareSchedule(saved.DeviceId) && options.All(option => option.Key != saved.DeviceId))
            options.Add(KeyValuePair.Create(saved.DeviceId, ScheduleDeviceLabel(saved.DeviceId)));
        var devices = new ComboBox
        {
            ItemsSource = options, DisplayMemberPath = "Value", SelectedValuePath = "Key",
            SelectedValue = original?.DeviceId ?? (HardwarePlaybackSelected ? HardwareSchedulePrefix + hardwareApplySerial : target),
            Margin = new Thickness(4)
        };
        panel.Children.Add(Text("设备")); panel.Children.Add(devices);
        var activeDays = original?.Days ?? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        var days = Enumerable.Range(0, 7).Select(day => new CheckBox
        {
            Content = "周" + "日一二三四五六"[day], IsChecked = activeDays.Contains((DayOfWeek)day), Margin = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center
        }).ToArray();
        AutomationProperties.SetName(devices, "设备");
        var dayRow = new WrapPanel();
        foreach (var day in days)
        {
            day.SetResourceReference(StyleProperty, "ScheduleDayChoice");
            day.Margin = new Thickness(0, 4, 6, 4);
            dayRow.Children.Add(day);
        }
        var presets = new WrapPanel();
        foreach (var preset in new[] { "每天", "工作日", "周末" })
            presets.Children.Add(HomeButton(preset, () =>
            {
                for (var day = 0; day < days.Length; day++)
                    days[day].IsChecked = preset == "每天" || (preset == "工作日") == (day is >= 1 and <= 5);
            }, "PillButton"));
        panel.Children.Add(Text("重复日期")); panel.Children.Add(dayRow);
        panel.Children.Add(presets);
        var time = new TextBox { Text = original?.Time.ToString("HH:mm") ?? "09:00", Margin = new Thickness(4) };
        time.Height = 36;
        time.MaxLength = 5;
        time.SetResourceReference(StyleProperty, "PlaylistSeconds");
        AutomationProperties.SetName(time, "时间（HH:mm）");
        panel.Children.Add(Text("时间（HH:mm）")); panel.Children.Add(time);
        var action = new ComboBox { ItemsSource = ScheduleActions, SelectedIndex = (int)(original?.Action ?? ScheduleAction.Playlist), Margin = new Thickness(4) };
        AutomationProperties.SetName(action, "动作");
        panel.Children.Add(Text("动作")); panel.Children.Add(action);
        var targets = new ComboBox { DisplayMemberPath = "Value", SelectedValuePath = "Key", Margin = new Thickness(4) };
        AutomationProperties.SetName(targets, "目标");
        var targetLabel = Text("目标");
        panel.Children.Add(targetLabel); panel.Children.Add(targets);
        var remark = new TextBox { Text = original?.Remark ?? "", Margin = new Thickness(4) };
        remark.Height = 36;
        remark.SetResourceReference(StyleProperty, "PlaylistSeconds");
        AutomationProperties.SetName(remark, "备注");
        panel.Children.Add(Text("备注")); panel.Children.Add(remark);
        var hint = Text("");
        var conflictHint = HomeText("", 13);
        conflictHint.Foreground = ColorBrush("#B54708");
        AutomationProperties.SetLiveSetting(conflictHint, AutomationLiveSetting.Polite);
        panel.Children.Add(hint);
        panel.Children.Add(conflictHint);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(HomeButton("取消", dialog.Close, "PillButton"));
        buttons.Children.Add(HomeButton("保存", () =>
        {
            if (devices.SelectedValue is not string device || !TimeOnly.TryParseExact(time.Text.Trim(), "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            { hint.Text = "请选择设备并输入 HH:mm 时间。"; return; }
            var rule = new ScheduleRule(original?.Id ?? Guid.NewGuid(), device,
                days.Select((check, index) => (check, index)).Where(item => item.check.IsChecked == true)
                    .Select(item => (DayOfWeek)item.index).ToArray(), at, (ScheduleAction)action.SelectedIndex,
                action.SelectedIndex == (int)ScheduleAction.Stop ? null : targets.SelectedValue as string,
                original?.Enabled ?? true, remark.Text.Trim());
            var conflict = state.ScheduleConflict(rule);
            try { state.SaveSchedule(rule, IsHardwareSchedule(rule.DeviceId) ? HardwareScheduleWarning : null); }
            catch (ArgumentException ex) { hint.Text = ex.Message; return; }
            SaveWorkspace();
            dialog.Close();
            Render();
            Notice.Text = conflict ? "规则已保存。同设备同时间的规则按创建顺序执行，后者生效。" : "定时规则已保存。";
        }, "PrimaryButton"));
        panel.Children.Add(buttons);
        devices.SelectionChanged += (_, _) => { RefreshTargets(null); RefreshConflict(); };
        action.SelectionChanged += (_, _) => RefreshTargets(null);
        time.TextChanged += (_, _) => RefreshConflict();
        foreach (var day in days)
        {
            day.Checked += (_, _) => RefreshConflict();
            day.Unchecked += (_, _) => RefreshConflict();
        }
        RefreshTargets(original?.TargetId);
        RefreshConflict();
        dialog.ShowDialog();

        void RefreshConflict()
        {
            var conflict = devices.SelectedValue is string id &&
                TimeOnly.TryParseExact(time.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) &&
                state.ScheduleConflict(new(original?.Id ?? Guid.Empty, id,
                    days.Select((day, index) => (day, index)).Where(pair => pair.day.IsChecked == true)
                        .Select(pair => (DayOfWeek)pair.index).ToArray(), at, ScheduleAction.Stop, null));
            conflictHint.Text = conflict ? "已有同设备、同日期、同时间的规则。可以保存，按创建顺序执行。" : "";
        }

        void RefreshTargets(string? initial)
        {
            var visible = action.SelectedIndex != (int)ScheduleAction.Stop;
            targets.Visibility = targetLabel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (devices.SelectedValue is not string id) return;
            var physical = IsHardwareSchedule(id);
            var device = state.Devices.GetValueOrDefault(id);
            if (!physical && device == null) return;
            KeyValuePair<string, string>[] Choices() => (action.SelectedIndex == (int)ScheduleAction.Playlist
                ? state.Playlists.Where(p => physical
                    ? (!IsHardwareSchedule(p.DeviceId) || p.DeviceId == id) && HardwarePlaylistItems(p).Length > 0
                    : p.DeviceId == id).Select(p => KeyValuePair.Create(p.Id, p.Name))
                : state.Resources.Values.Where(r => r.Local && (physical ? HardwarePlaylistIssue(r) == null :
                    Workspace.Difference(r, device!.Canvas) <= .05))
                    .Select(r => KeyValuePair.Create(r.Id, r.Name))).ToArray();
            var items = physical ? InHardwareSession(id[HardwareSchedulePrefix.Length..], Choices) : Choices();
            targets.ItemsSource = items;
            targets.SelectedValue = items.Any(item => item.Key == initial) ? initial : items.FirstOrDefault().Key;
            hint.Text = visible && items.Length == 0 ? "此设备没有可选目标。" :
                visible && initial != null && !items.Any(item => item.Key == initial) ? "原目标已删除或不再适配，请重新选择。" : "";
            if (visible && initial != null && !items.Any(item => item.Key == initial)) targets.SelectedIndex = -1;
        }
    }
}
