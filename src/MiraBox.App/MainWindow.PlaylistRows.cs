using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    const string PlaylistDragType = "MiraBoxPlaylistIndex";

    Border BuildPlaylistRow(Playlist list, int index)
    {
        var item = list.Items[index];
        var resource = state.Resources.GetValueOrDefault(item.ResourceId);
        var fit = PlaylistFits(list, resource);
        var name = ResourceName(item.ResourceId);
        var canvas = PlaylistCanvas(list);
        var narrow = playlistWindowWidth < 1080;
        var maxWidth = playlistWindowWidth < 900 ? 200d : narrow ? 220d : 280d;
        var height = Math.Min(160, maxWidth * canvas.Height / canvas.Width);
        var preview = new Grid { Width = maxWidth, Height = height, Margin = new Thickness(0, 0, 12, 0) };
        preview.Clip = new RectangleGeometry(new Rect(0, 0, maxWidth, height), 12, 12);
        preview.Background = ColorBrush("#E6F6FB");
        if (resource != null) preview.Children.Add(Thumbnail(resource, canvas, maxWidth, height));
        if (!fit) preview.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(89, 255, 255, 255)) });
        preview.Children.Add(new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(7),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = ColorBrush("#99000000"), Child = new TextBlock { Text = (index + 1).ToString(), FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
        });
        var active = PlaylistBadge("▶ 正在播放", "#2468E8", "#FFFFFF");
        active.Background = (Brush)FindResource("BrandBrush");
        active.HorizontalAlignment = HorizontalAlignment.Right;
        active.VerticalAlignment = VerticalAlignment.Bottom;
        active.Margin = new Thickness(0, 0, 6, 6);
        active.Visibility = Visibility.Collapsed;
        preview.Children.Add(active);

        var stackedDetails = playlistWindowWidth is >= 760 and < 1000;
        var row = new Grid();
        if (stackedDetails)
        {
            row.RowDefinitions.Add(new() { Height = GridLength.Auto });
            row.RowDefinitions.Add(new() { Height = GridLength.Auto });
        }
        row.ColumnDefinitions.Add(new() { Width = new GridLength(24) });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(maxWidth + 12) });
        row.ColumnDefinitions.Add(new());
        var grip = PlaylistIcon("M5,3 h1 M10,3 h1 M5,8 h1 M10,8 h1 M5,13 h1 M10,13 h1", 16);
        var handle = new Border { Child = grip, Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = "拖动排序 · Alt+↑ / Alt+↓" };
        row.Children.Add(handle);
        Grid.SetColumn(preview, 1);
        row.Children.Add(preview);
        var info = new Grid();
        info.RowDefinitions.Add(new() { Height = new GridLength(24) });
        info.RowDefinitions.Add(new() { Height = GridLength.Auto, MinHeight = 36 });
        info.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var title = HomeText(name, 16, bold: true);
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.ToolTip = name;
        var type = HomeText(resource == null ? "" : Entry(item.ResourceId).Type, 12, muted: true);
        type.Margin = new Thickness(6, 0, 0, 0);
        var titleRow = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        titleRow.ColumnDefinitions.Add(new());
        titleRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        titleRow.Children.Add(title);
        Grid.SetColumn(type, 1);
        titleRow.Children.Add(type);
        info.Children.Add(titleRow);
        var compatibility = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
        if (!fit)
        {
            var issue = HardwarePlaybackSelected && resource != null ? HardwarePlaylistIssue(resource) : null;
            var mismatch = issue == null || issue == HardwareMismatch;
            compatibility.Children.Add(PlaylistBadge(mismatch ? "需适配" : issue!, "#FFF4E5", "#B54708"));
            var skipped = HomeText(mismatch ? "当前设备方向下将被跳过" : "播放时自动跳过", 12, muted: true);
            skipped.Margin = new Thickness(0, 0, 0, 0);
            compatibility.Children.Add(skipped);
        }
        Grid.SetRow(compatibility, 2);
        info.Children.Add(compatibility);

        var options = new Grid();
        options.ColumnDefinitions.Add(new());
        options.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var field = BuildPlaylistSeconds(item.Duration.TotalSeconds, seconds =>
        {
            var current = list.Items.ToList().FindIndex(entry => entry.ResourceId == item.ResourceId);
            if (current >= 0) { SetPlaylistEntrySeconds(list, current, seconds); SaveLists(); }
        }, name + " 停留秒数", showUnit: true);
        options.Children.Add(field.Layout);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var inspect = PlaylistGlyphButton("预览 " + name, "M1,8 Q8,0 15,8 Q8,16 1,8 Z M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8", () =>
        {
            if (resource == null) return;
            if (Entry(resource.Id).Type == "视频") ResourceDetail(resource, startVideoPreview: true);
            else if (HardwarePlaybackSelected) PreviewHardwareStill(resource);
            else
            {
                if (state.Devices.ContainsKey(list.DeviceId)) Target.SelectedValue = list.DeviceId;
                Preview(resource);
            }
        }, 32);
        inspect.IsEnabled = resource is { Local: true };
        buttons.Children.Add(inspect);
        buttons.Children.Add(PlaylistGlyphButton("移除 " + name, "M2,4 H14 M6,4 V2 H10 V4 M4,4 L5,14 H11 L12,4 M7,7 V11 M9,7 V11",
            () => RemovePlaylistEntry(list, index), 32));
        Grid.SetColumn(buttons, 1);
        options.Children.Add(buttons);
        Grid.SetRow(options, 1);
        info.Children.Add(options);
        if (stackedDetails)
        {
            Grid.SetRow(info, 1);
            Grid.SetColumn(info, 1);
            Grid.SetColumnSpan(info, 2);
            info.Margin = new Thickness(0, 8, 0, 0);
        }
        else Grid.SetColumn(info, 2);
        row.Children.Add(info);

        var progress = PlaylistProgress(4);
        progress.VerticalAlignment = VerticalAlignment.Bottom;
        progress.Visibility = Visibility.Collapsed;
        var marker = new Border { Height = 3, CornerRadius = new CornerRadius(2),
            Background = ColorBrush("#58C4DC"), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        var chrome = new Grid();
        chrome.Children.Add(row);
        chrome.Children.Add(progress);
        chrome.Children.Add(marker);
        var card = new Border { Tag = item.ResourceId, Child = chrome, CornerRadius = new CornerRadius(18),
            Margin = new Thickness(0, 0, 0, 12), Focusable = true, AllowDrop = true };
        card.SetResourceReference(StyleProperty, "PlaylistEntryCard");
        AutomationProperties.SetName(card, $"第 {index + 1} 项，{name}，按 Alt 加方向键排序");
        card.MouseLeftButtonDown += (_, _) => card.Focus();
        card.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Alt || e.Key is not (Key.Up or Key.Down or Key.System)) return;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is not (Key.Up or Key.Down)) return;
            e.Handled = true;
            MovePlaylistEntry(list, index, index + (key == Key.Up ? -1 : 1));
        };
        Point? dragStart = null;
        handle.PreviewMouseLeftButtonDown += (_, e) => dragStart = e.GetPosition(handle);
        handle.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragStart is not { } origin) { dragStart = null; return; }
            var delta = e.GetPosition(handle) - origin;
            if (Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance &&
                Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance) return;
            dragStart = null;
            card.RenderTransformOrigin = new Point(.5, .5);
            card.RenderTransform = new ScaleTransform(1.02, 1.02);
            card.Effect = (Effect)FindResource("ResourceMenuShadow");
            try { DragDrop.DoDragDrop(handle, new DataObject(PlaylistDragType, index), DragDropEffects.Move); }
            finally { card.RenderTransform = Transform.Identity; card.ClearValue(EffectProperty); }
        };
        card.DragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent(PlaylistDragType)) return;
            marker.VerticalAlignment = e.GetPosition(card).Y < card.ActualHeight / 2 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            marker.Visibility = Visibility.Visible;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        };
        card.DragLeave += (_, _) => marker.Visibility = Visibility.Collapsed;
        card.Drop += (_, e) =>
        {
            marker.Visibility = Visibility.Collapsed;
            if (e.Data.GetData(PlaylistDragType) is not int from) return;
            e.Handled = true;
            var insertion = index + (e.GetPosition(card).Y >= card.ActualHeight / 2 ? 1 : 0);
            MovePlaylistEntry(list, from, insertion > from ? insertion - 1 : insertion);
        };
        playlistRows.Add(new(card, active, progress));
        return card;
    }

    void MovePlaylistEntry(Playlist list, int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= list.Items.Count || to >= list.Items.Count) return;
        playlistFocusItem = list.Items[from].ResourceId;
        list.Move(from, to);
        ShowPlaylistFeedback($"已将「{ResourceName(playlistFocusItem)}」移动到第 {to + 1} 项");
        SaveLists();
        Render();
    }

    void RemovePlaylistEntry(Playlist list, int index)
    {
        if (index < 0 || index >= list.Items.Count) return;
        var item = list.Items[index];
        list.Remove(item.ResourceId);
        removedPlaylistItem = (list.Id, item, index, watch.Elapsed + TimeSpan.FromSeconds(5));
        SaveLists();
        Render();
    }

    void DrawPlaylistAnnouncement()
    {
        var removal = removedPlaylistItem is { } item && watch.Elapsed < item.Until;
        var addition = !removal && undo is { } add && add.Until > DateTimeOffset.UtcNow;
        DrawPlaylistUndo(removal || addition, removal && removedPlaylistItem is { } removed
            ? $"已移除「{ResourceName(removed.Item.ResourceId)}」" : "已添加内容", removal);
        if (watch.Elapsed >= playlistFeedbackUntil) PlaylistToast.Visibility = Visibility.Collapsed;
    }

    void ShowPlaylistFeedback(string message)
    {
        playlistFeedbackUntil = watch.Elapsed + TimeSpan.FromSeconds(3);
        PlaylistToastText.Text = message;
        PlaylistToast.Visibility = Visibility.Visible;
        if (UIElementAutomationPeer.CreatePeerForElement(PlaylistToastText) is { } peer)
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    sealed record SecondsEditor(StackPanel Layout, TextBox Input, TryPlaylistSeconds TryRead);
    delegate bool TryPlaylistSeconds(out double value);

    SecondsEditor BuildPlaylistSeconds(double initial, Action<double>? save, string label = "每项停留秒数", bool showUnit = false)
    {
        var last = initial;
        var box = new TextBox { Text = initial.ToString(CultureInfo.CurrentCulture), Width = 72, Height = 36 };
        box.SetResourceReference(StyleProperty, "PlaylistSeconds");
        AutomationProperties.SetName(box, label);
        var feedback = HomeText("", 12);
        feedback.Visibility = Visibility.Collapsed;
        var success = HomeText("✓ 已保存", 12);
        success.Foreground = ColorBrush("#067647");
        success.Visibility = Visibility.Collapsed;
        var field = new StackPanel { Orientation = Orientation.Horizontal };
        field.Children.Add(box);
        if (showUnit) field.Children.Add(PlaylistFieldLabel("秒"));
        var row = new WrapPanel();
        row.Children.Add(field);
        success.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(success);
        var container = new StackPanel();
        container.Children.Add(row);
        container.Children.Add(feedback);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) => { timer.Stop(); success.Visibility = Visibility.Collapsed; };
        container.Unloaded += (_, _) => timer.Stop();

        bool Read(out double value)
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                double.IsFinite(value) && value is >= 10 and <= 86400)
            {
                feedback.Visibility = Visibility.Collapsed;
                box.Tag = null;
                return true;
            }
            timer.Stop();
            success.Visibility = Visibility.Collapsed;
            box.Text = last.ToString(CultureInfo.CurrentCulture);
            feedback.Text = "10–86400 秒";
            feedback.Foreground = ColorBrush("#B42318");
            feedback.Visibility = Visibility.Visible;
            box.Tag = "Error";
            return false;
        }
        void Commit()
        {
            if (box.Text == last.ToString(CultureInfo.CurrentCulture) || !Read(out var value) || value == last) return;
            try
            {
                save?.Invoke(value);
                last = value;
                if (save == null) return;
                success.Visibility = Visibility.Visible;
                timer.Stop();
                timer.Start();
            }
            catch (Exception ex) { Notice.Text = ex.Message; }
        }
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(); } };
        return new(container, box, Read);
    }
}
