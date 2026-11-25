using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using MiraBox.Core;
using CanvasSize = MiraBox.Core.Size;

namespace MiraBox.App;

public partial class MainWindow
{
    const double WidePlaylistSidebarMinWidth = 1140;
    record PlaylistRow(Border Card, Border Badge, ProgressBar Progress);
    readonly List<PlaylistRow> playlistRows = [];
    Border? playlistPlaybackPanel;
    TextBlock? playlistCurrentText, playlistRemainingText, playlistNextText;
    ProgressBar? playlistProgress;
    TimeSpan playlistLastPaint;
    TimeSpan playlistFeedbackUntil;
    string? playlistFocusItem;
    (string List, PlaylistItem Item, int Index, TimeSpan Until)? removedPlaylistItem;
    double playlistWindowWidth = 1280;

    void ResizePlaylist(double width)
    {
        int Layout(double value) => value < 760 ? 0 : value < 900 ? 1 : value < 1000 ? 2 :
            value < 1080 ? 3 : value < WidePlaylistSidebarMinWidth ? 4 : 5;
        var changed = Layout(playlistWindowWidth) != Layout(width);
        playlistWindowWidth = width;
        if (changed && page == "轮播列表" && !initializing) Render();
    }

    bool PlaylistPlaying(Playlist list) => HardwarePlaybackSelected ? IsHardwarePlaylist(list) : list.Playing;
    CanvasSize PlaylistCanvas(Playlist list) => HardwarePlaybackSelected ? HardwareCanvas :
        state.Devices.GetValueOrDefault(list.DeviceId)?.Canvas ?? BrowseCanvas();

    bool PlaylistFits(Playlist list, Resource? resource) => resource is { Local: true } &&
        (HardwarePlaybackSelected ? HardwarePlaylistIssue(resource) == null :
            Workspace.Difference(resource, PlaylistCanvas(list)) is <= .05);

    static string PlaylistLength(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours} 小时 {value.Minutes} 分钟" : value.TotalMinutes >= 1
            ? $"{(int)value.TotalMinutes} 分钟{(value.Seconds == 0 ? "" : $" {value.Seconds} 秒")}" : $"{(int)value.TotalSeconds} 秒";

    static string PlaylistSummary(Playlist list) =>
        $"{list.Items.Count} 项 · {PlaylistLength(TimeSpan.FromSeconds(list.Items.Sum(item => item.Duration.TotalSeconds)))}";

    void RenderPlaylists()
    {
        playlistRows.Clear();
        playlistPlaybackPanel = null;
        playlistLastPaint = TimeSpan.MinValue;
        var heading = new StackPanel();
        heading.Children.Add(HomeText("轮播列表", 26, bold: true));
        heading.Children.Add(HomeText("像歌单一样，为不同心情准备不同的内容。", 14, muted: true));
        var create = HomeButton("＋ 新建轮播列表", () => CreateList(), "PrimaryButton");
        create.HorizontalAlignment = HorizontalAlignment.Right;
        var header = HomeHeader(heading, create);
        header.Margin = new Thickness(0, 0, 0, 20);
        Body.Children.Add(header);

        DrawPlaylistAnnouncement();

        var ordered = state.Playlists.Where(list => !IsHardwareSchedule(list.DeviceId) ||
            HardwarePlaybackSelected && list.DeviceId == HardwareSchedulePrefix + hardwareApplySerial)
            .OrderByDescending(PlaylistPlaying).ThenByDescending(list => list.LastUsed).ToArray();
        var selected = ordered.FirstOrDefault(list => list.Id == selectedList) ?? ordered.FirstOrDefault();
        selectedList = selected?.Id;
        var compact = playlistWindowWidth < 760;
        var choices = new StackPanel { Orientation = compact && ordered.Length != 0 ? Orientation.Horizontal : Orientation.Vertical };
        if (ordered.Length == 0)
            choices.Children.Add(PlaylistEmpty("还没有轮播列表", "新建轮播列表", () => CreateList()));
        else
        {
            foreach (var list in ordered) choices.Children.Add(BuildPlaylistChoice(list, selected, compact));
            choices.Children.Add(PlaylistDashedAction("＋ 新建轮播列表", () => CreateList(), compact ? 52 : 60));
        }

        var layout = new Grid();
        var right = new StackPanel();
        if (compact)
        {
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            layout.Children.Add(new ScrollViewer { Content = choices, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
            Grid.SetRow(right, 1);
            right.Margin = new Thickness(0, 20, 0, 0);
        }
        else
        {
            layout.ColumnDefinitions.Add(new() { Width = new GridLength(playlistWindowWidth < WidePlaylistSidebarMinWidth ? 220 : 280) });
            layout.ColumnDefinitions.Add(new() { Width = new GridLength(20) });
            layout.ColumnDefinitions.Add(new());
            layout.Children.Add(choices);
            Grid.SetColumn(right, 2);
        }
        layout.Children.Add(right);
        Body.Children.Add(layout);

        if (selected == null)
        {
            var guidance = HomeText("新建轮播列表后，就可以添加内容并设定播放顺序。", 14, muted: true);
            guidance.Margin = new Thickness(16);
            right.Children.Add(guidance);
            return;
        }

        BuildPlaylistDetails(right, selected);
        UpdatePlaylistStatus();
    }

    Button BuildPlaylistChoice(Playlist list, Playlist? selected, bool compact)
    {
        var button = Action(list.Name, () => { selectedList = list.Id; Render(); });
        button.SetResourceReference(StyleProperty, "SidebarCardChoice");
        button.Tag = list == selected ? "Selected" : null;
        button.Margin = compact ? new Thickness(0, 2, 10, 4) : new Thickness(0, 2, 2, 8);
        AutomationProperties.SetName(button, $"{list.Name}，{list.Mood}，{PlaylistSummary(list)}");
        if (compact)
        {
            button.SetResourceReference(StyleProperty, list == selected ? "PrimaryButton" : "PillButton");
            return button;
        }
        var details = new StackPanel();
        var normalShadow = ((DropShadowEffect)FindResource("PanelShadow")).Clone();
        button.Effect = normalShadow;
        button.MouseEnter += (_, _) => normalShadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(.20, TimeSpan.FromMilliseconds(180)));
        button.MouseLeave += (_, _) => normalShadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(.10, TimeSpan.FromMilliseconds(180)));
        var title = HomeText(list.Name, 16, bold: true);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        if (PlaylistPlaying(list))
        {
            var playing = PlaylistIcon("M3,1 L14,8 L3,15 Z", 13);
            playing.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "RunningInkBrush");
            var label = new DockPanel();
            DockPanel.SetDock(playing, Dock.Left);
            playing.Margin = new Thickness(0, 0, 6, 0);
            label.Children.Add(playing);
            label.Children.Add(title);
            details.Children.Add(label);
        }
        else details.Children.Add(title);
        var mood = PlaylistIconLabel(list.Mood, PlaylistMoodGeometry(list.Mood));
        TextElement.SetForeground(mood, ColorBrush("#0B78C4"));
        ((TextBlock)mood.Children[1]).FontSize = 12;
        var badge = new Border { Child = mood, Background = list == selected ? Brushes.White : ColorBrush("#E6F6FB"),
            CornerRadius = new CornerRadius(11), Padding = new Thickness(7, 2, 7, 2) };
        var meta = HomeText(PlaylistSummary(list) + (PlaylistPlaying(list) ? " · 播放中" : ""), 13, muted: true);
        meta.Margin = new Thickness(6, 0, 0, 0);
        var row = new DockPanel { Margin = new Thickness(0, 7, 0, 0) };
        DockPanel.SetDock(badge, Dock.Left);
        row.Children.Add(badge);
        row.Children.Add(meta);
        details.Children.Add(row);
        button.Content = details;
        return button;
    }

    void BuildPlaylistDetails(StackPanel panel, Playlist list)
    {
        var physical = HardwarePlaybackSelected;
        var playing = PlaylistPlaying(list);
        var device = state.Devices.GetValueOrDefault(list.DeviceId);

        var title = new TextBox { Text = list.Name, FontSize = 22, FontWeight = FontWeights.Bold, Height = 38,
            MaxLength = 30, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 4) };
        title.SetResourceReference(StyleProperty, "PlaylistSeconds");
        var label = HomeText(list.Name, 22, bold: true);
        var pencil = PlaylistIcon("M2,11 L10,3 L14,7 L6,15 L2,15 Z M9,4 L13,8", 16);
        pencil.Margin = new Thickness(8, 0, 0, 0);
        pencil.Opacity = 0;
        var titleContent = new StackPanel { Orientation = Orientation.Horizontal };
        titleContent.Children.Add(label);
        titleContent.Children.Add(pencil);
        var rename = HomeButton("", () => { }, "LinkButton");
        rename.Click += (_, _) => BeginRename();
        rename.HorizontalAlignment = HorizontalAlignment.Left;
        rename.Padding = new Thickness(0);
        rename.SetResourceReference(Control.ForegroundProperty, "InkBrush");
        rename.Content = titleContent;
        rename.ToolTip = "修改列表名称";
        AutomationProperties.SetName(rename, "修改列表名称 " + list.Name);
        rename.MouseEnter += (_, _) => pencil.Opacity = 1;
        rename.MouseLeave += (_, _) => pencil.Opacity = 0;
        panel.Children.Add(rename);
        panel.Children.Add(title);
        title.LostKeyboardFocus += (_, _) => FinishRename();
        title.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; FinishRename(); }
            else if (e.Key == Key.Escape) { e.Handled = true; title.Text = list.Name; FinishRename(); }
        };
        panel.Children.Add(HomeText($"{(physical ? HardwareTargetLabel : DeviceName(list.DeviceId))} · {PlaylistSummary(list)} · {list.Mood}" +
            (physical && !IsHardwareSchedule(list.DeviceId) ? " · 首次播放后绑定当前 D92" : ""), 13, muted: true));

        var tools = new WrapPanel { Margin = new Thickness(0, 12, 0, 8) };
        var picker = new ComboBox
        {
            ItemsSource = state.Devices.Values.ToArray(), SelectedValuePath = "Id", DisplayMemberPath = "Name",
            SelectedValue = list.DeviceId, MinWidth = 126, IsEnabled = !physical,
            Margin = new Thickness(0, 4, 10, 4)
        };
        if (physical) picker.ItemsSource = new[] { new { Id = list.DeviceId, Name = HardwareTargetLabel } };
        AutomationProperties.SetName(picker, "播放设备");
        picker.SelectionChanged += (_, _) =>
        {
            if (!physical && picker.SelectedValue is string id && id != list.DeviceId) { list.Assign(id); SaveLists(); Render(); }
        };
        tools.Children.Add(picker);
        var resuming = physical && hardwarePlaylistId == list.Id && hardwarePlaylistPaused;
        var playLabel = playing ? (physical ? "暂停轮播切换" : "暂停轮播") : resuming ? "继续轮播切换" : "开始轮播";
        var play = PlaylistAction(playLabel, playing ? "M5,3 V13 M11,3 V13" : "M4,2 L13,8 L4,14 Z",
            () =>
            {
                if (physical) ToggleHardwarePlaylist(list);
                else if (list.Playing) list.Pause(); else state.Start(list);
                SaveLists(); Render();
            }, playing ? "PillButton" : "PrimaryButton");
        play.IsEnabled = playing || (physical || device?.Online == true) && list.Items.Any(item => PlaylistFits(list, state.Resources.GetValueOrDefault(item.ResourceId)));
        tools.Children.Add(play);
        tools.Children.Add(PlaylistAction("添加内容", "M8,2 V14 M2,8 H14", () => AddContents(list)));
        if (playing)
        {
            tools.Children.Add(PlaylistGlyphButton("上一项", "M11,3 L5,8 L11,13", () => Move(-1), 34));
            tools.Children.Add(PlaylistGlyphButton("下一项", "M5,3 L11,8 L5,13", () => Move(1), 34));
        }
        var more = PlaylistGlyphButton("更多列表操作", "M3,8 h1 M7.5,8 h1 M12,8 h1", () => { }, 34);
        var menu = new ContextMenu { PlacementTarget = more, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.SetResourceReference(StyleProperty, "ResourceContextMenu");
        var delete = new MenuItem { Header = "删除列表", Tag = "Destructive" };
        delete.SetResourceReference(StyleProperty, "ResourceMenuItem");
        delete.Click += (_, _) => DeletePlaylist(list);
        menu.Items.Add(delete);
        more.ContextMenu = menu;
        more.Click += (_, _) => menu.IsOpen = true;
        tools.Children.Add(more);
        var info = HomeText("ⓘ", 16);
        info.ToolTip = "暂停时保留当前画面；不适配的内容会被自动跳过。";
        tools.Children.Add(info);
        panel.Children.Add(tools);

        panel.Children.Add(MoodChoices(list.Mood, mood => { list.Mood = mood; SaveLists(); Render(); }));
        panel.Children.Add(BuildPlaylistOptions(list));
        panel.Children.Add(BuildPlaylistPlaybackStatus());

        if (list.Items.Count == 0)
            panel.Children.Add(PlaylistEmpty("这个列表还没有内容", "添加内容", () => AddContents(list),
                "可以按类型、方向筛选，已在列表中的内容不能重复添加"));
        else
        {
            for (var index = 0; index < list.Items.Count; index++) panel.Children.Add(BuildPlaylistRow(list, index));
            panel.Children.Add(PlaylistDashedAction("＋ 添加内容", () => AddContents(list), 96));
        }
        if (playlistFocusItem is { } focus)
        {
            playlistFocusItem = null;
            var card = playlistRows.FirstOrDefault(row => Equals(row.Card.Tag, focus))?.Card;
            if (card != null) _ = Dispatcher.InvokeAsync(() => card.Focus());
        }

        void BeginRename()
        {
            rename.Visibility = Visibility.Collapsed;
            title.Visibility = Visibility.Visible;
            title.SelectAll();
            title.Focus();
        }

        void FinishRename()
        {
            if (title.Visibility != Visibility.Visible) return;
            title.Visibility = Visibility.Collapsed;
            rename.Visibility = Visibility.Visible;
            var name = title.Text.Trim();
            if (name.Length == 0 || name == list.Name) return;
            list.Name = name;
            SaveLists();
            Render();
        }
        void Move(int direction)
        {
            if (physical) StepHardwarePlaylist(direction);
            else state.Step(list, direction);
            SaveLists();
            Render();
        }
    }

    void DeletePlaylist(Playlist list)
    {
        var references = state.Schedules.Count(rule => rule.Action == ScheduleAction.Playlist && rule.TargetId == list.Id);
        var warning = references == 0 ? "" : $"\n引用此列表的 {references} 条定时规则将保留，并标为内容已删除。";
        if (MessageBox.Show(this, $"列表「{list.Name}」将被删除，包含的 {list.Items.Count} 项内容不会被删除。{warning}",
            "删除列表", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        list.Pause();
        if (hardwarePlaylistId == list.Id) PauseHardwarePlaylist();
        state.Playlists.Remove(list);
        if (undo?.List == list.Id) undo = null;
        selectedList = null;
        SaveLists();
        Render();
    }

    FrameworkElement BuildPlaylistOptions(Playlist list)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 12) };
        var shuffle = new CheckBox { Content = "随机播放", IsChecked = list.Shuffle, Margin = new Thickness(0, 10, 16, 0),
            VerticalAlignment = VerticalAlignment.Top };
        shuffle.SetResourceReference(StyleProperty, "Switch");
        shuffle.Click += (_, _) => { list.Shuffle = shuffle.IsChecked == true; SaveLists(); UpdatePlaylistStatus(); };
        row.Children.Add(shuffle);
        var duration = new StackPanel { Orientation = Orientation.Horizontal };
        var durationLabel = new Border { Height = 36, VerticalAlignment = VerticalAlignment.Top, Child = PlaylistFieldLabel("每项停留") };
        duration.Children.Add(durationLabel);
        var editor = BuildPlaylistSeconds(list.Items.FirstOrDefault()?.Duration.TotalSeconds ?? 30, null, showUnit: true);
        duration.Children.Add(editor.Layout);
        row.Children.Add(duration);
        var apply = HomeButton("应用到全部", () =>
        {
            if (!editor.TryRead(out var seconds)) return;
            for (var i = 0; i < list.Items.Count; i++) SetPlaylistEntrySeconds(list, i, seconds);
            SaveLists();
            ShowPlaylistFeedback("已更新全部停留时间");
            Render();
        }, "LinkButton");
        apply.Margin = new Thickness(4, 3, 0, 0);
        apply.VerticalAlignment = VerticalAlignment.Top;
        apply.IsEnabled = list.Items.Count > 0;
        row.Children.Add(apply);
        return row;
    }

    void SetPlaylistEntrySeconds(Playlist list, int index, double seconds)
    {
        var item = list.Items[index];
        if (list.Id == hardwarePlaylistId && item.ResourceId == hardwarePlaylistItem)
        {
            var elapsed = Math.Max(0, (item.Duration - hardwarePlaylistRemaining).TotalSeconds);
            hardwarePlaylistRemaining = TimeSpan.FromSeconds(Math.Max(0, seconds - elapsed));
        }
        list.SetSeconds(index, seconds);
        playlistLastPaint = TimeSpan.MinValue;
        UpdatePlaylistStatus();
    }

    Border BuildPlaylistPlaybackStatus()
    {
        playlistCurrentText = HomeText("", 14, bold: true);
        playlistRemainingText = HomeText("", 13, muted: true);
        playlistNextText = HomeText("", 13, muted: true);
        var labels = new WrapPanel();
        foreach (var item in new[] { playlistCurrentText, playlistRemainingText, playlistNextText })
        {
            item.Margin = new Thickness(0, 0, 14, 0);
            labels.Children.Add(item);
        }
        playlistProgress = PlaylistProgress(6);
        playlistProgress.Margin = new Thickness(0, 9, 0, 0);
        var contents = new StackPanel();
        contents.Children.Add(labels);
        contents.Children.Add(playlistProgress);
        return playlistPlaybackPanel = new Border { Child = contents, Background = ColorBrush("#F3FAFD"),
            CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed };
    }

    void UpdatePlaylistStatus()
    {
        if (page != "轮播列表") return;
        DrawPlaylistAnnouncement();
        if (playlistPlaybackPanel == null) return;
        var list = state.Playlists.Find(item => item.Id == selectedList);
        if (list == null) return;
        var playing = PlaylistPlaying(list);
        playlistPlaybackPanel.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        if (playlistLastPaint != TimeSpan.MinValue && watch.Elapsed - playlistLastPaint < TimeSpan.FromMilliseconds(500)) return;
        playlistLastPaint = watch.Elapsed;
        var physical = HardwarePlaybackSelected;
        var id = physical ? hardwarePlaylistItem : list.Items.ElementAtOrDefault(list.Index)?.ResourceId;
        var remaining = physical ? hardwarePlaylistRemaining : list.Remaining;
        var duration = list.Items.FirstOrDefault(item => item.ResourceId == id)?.Duration.TotalSeconds ?? 1;
        var percentage = Math.Clamp(1 - remaining.TotalSeconds / Math.Max(1, duration), 0, 1);
        var order = list.Items.Where(item => PlaylistFits(list, state.Resources.GetValueOrDefault(item.ResourceId))).ToArray();
        var index = Array.FindIndex(order, item => item.ResourceId == id);
        var next = order.Length == 0 ? "无可用内容" : list.Shuffle && order.Length > 1 ? "随机播放" : ResourceName(order[(index + 1) % order.Length].ResourceId);
        if (playing)
        {
            playlistCurrentText!.Text = $"当前：{ResourceName(id)}";
            playlistRemainingText!.Text = $"剩余 {Math.Max(0, Math.Ceiling(remaining.TotalSeconds))} 秒";
            playlistNextText!.Text = $"下一项：{next}";
            AnimatePlaylistProgress(playlistProgress!, percentage);
        }
        foreach (var item in playlistRows)
        {
            var current = playing && Equals(item.Card.Tag, id);
            if (item.Badge.Visibility != (current ? Visibility.Visible : Visibility.Collapsed))
            {
                item.Card.SetResourceReference(StyleProperty, current ? "PlaylistPlayingEntryCard" : "PlaylistEntryCard");
                item.Progress.BeginAnimation(ProgressBar.ValueProperty, null);
                item.Progress.Value = percentage;
            }
            item.Badge.Visibility = item.Progress.Visibility = current ? Visibility.Visible : Visibility.Collapsed;
            if (current)
            {
                if (item.Badge.Child is TextBlock badge)
                    badge.Text = physical && HardwareVideoPaused ? "视频已暂停" : "▶ 正在播放";
                AnimatePlaylistProgress(item.Progress, percentage);
            }
        }
    }

    static void AnimatePlaylistProgress(ProgressBar bar, double value)
    {
        if (Math.Abs(bar.Value - value) > .001)
            bar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(bar.Value, value, TimeSpan.FromMilliseconds(350)));
    }
}
