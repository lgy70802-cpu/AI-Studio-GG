using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    static System.Windows.Shapes.Path PlaylistIcon(string shape, double size = 16)
    {
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(shape), Width = size, Height = size, Stretch = Stretch.Uniform,
            StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
        };
        icon.SetBinding(Shape.StrokeProperty, new Binding("(TextElement.Foreground)") { Source = icon });
        return icon;
    }

    StackPanel PlaylistIconLabel(string title, string shape)
    {
        var box = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = PlaylistIcon(shape);
        icon.Margin = new Thickness(0, 0, 6, 0);
        box.Children.Add(icon);
        box.Children.Add(HomeText(title));
        return box;
    }

    static string PlaylistMoodGeometry(string mood) => mood switch
    {
        "专注" => "M14,8 A6,6 0 1 1 2,8 A6,6 0 1 1 14,8 M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8",
        "清晨" => "M2,13 H14 M4,11 A4,4 0 0 1 12,11 M8,2 V4 M3,5 L4,6 M12,6 L13,5",
        "夜晚" => "M13,10 A6,6 0 1 1 7,2 A5,5 0 0 0 13,10",
        "放松" => "M2,6 Q5,3 8,6 T14,6 M2,11 Q5,8 8,11 T14,11",
        "喜爱" => "M8,14 L2,8 A3.5,3.5 0 0 1 8,4 A3.5,3.5 0 0 1 14,8 Z",
        _ => "M8,1 Q8,7 15,8 Q8,9 8,15 Q8,9 1,8 Q8,7 8,1 Z"
    };

    Button PlaylistAction(string label, string icon, Action action, string style = "PillButton")
    {
        var button = HomeButton("", action, style);
        button.Content = PlaylistIconLabel(label, icon);
        button.Margin = new Thickness(0, 4, 10, 4);
        AutomationProperties.SetName(button, label);
        return button;
    }

    Button PlaylistGlyphButton(string label, string icon, Action action, double size)
    {
        var button = HomeButton("", action, "SmallButton");
        button.Width = button.Height = size;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0, 2, 6, 2);
        button.Content = PlaylistIcon(icon);
        button.ToolTip = label;
        AutomationProperties.SetName(button, label);
        return button;
    }

    Border PlaylistBadge(string label, string background, string foreground)
    {
        var text = HomeText(label, 12);
        text.Foreground = ColorBrush(foreground);
        return new Border { Background = ColorBrush(background), Child = text, CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    }

    TextBlock PlaylistFieldLabel(string label)
    {
        var text = HomeText(label, 13, muted: true);
        text.Margin = new Thickness(4, 0, 8, 0);
        return text;
    }

    ProgressBar PlaylistProgress(double height)
    {
        var bar = new ProgressBar { Height = height, Maximum = 1 };
        bar.SetResourceReference(StyleProperty, "PlaylistProgress");
        return bar;
    }

    Button PlaylistDashedAction(string label, Action action, double height)
    {
        var button = HomeButton(label, action, "PlaylistDashedButton");
        button.Height = height;
        button.Margin = new Thickness(0, 0, 2, 10);
        return button;
    }

    Border PlaylistEmpty(string label, string actionLabel, Action action, string? help = null)
    {
        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        body.Children.Add(PlaylistIcon("M2,4 H14 M2,8 H10 M2,12 H8 M12,10 V15 M9.5,12.5 H14.5", 22));
        var text = HomeText(label, 16, bold: true);
        text.TextAlignment = TextAlignment.Center;
        text.Margin = new Thickness(0, 10, 0, 6);
        body.Children.Add(text);
        var create = HomeButton(actionLabel, action, "PrimaryButton");
        create.HorizontalAlignment = HorizontalAlignment.Center;
        body.Children.Add(create);
        if (help != null)
        {
            var note = HomeText(help, 13, muted: true);
            note.TextAlignment = TextAlignment.Center;
            note.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(note);
        }
        return new Border { Child = body, Background = ColorBrush("#F3FAFD"),
            CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 25, 12, 25) };
    }

    void DrawPlaylistUndo(bool active, string message, bool removal = false)
    {
        if (!active || page != "轮播列表")
        {
            PlaylistUndoBar.Content = null;
            PlaylistUndoBar.Visibility = Visibility.Collapsed;
            return;
        }
        PlaylistUndoBar.Visibility = Visibility.Visible;
        if (PlaylistUndoBar.Tag as string == message && PlaylistUndoBar.Content != null) return;
        PlaylistUndoBar.Tag = message;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(HomeText(message, 13));
        row.Children.Add(HomeButton("撤销", () =>
        {
            if (removal)
            {
                if (removedPlaylistItem is not { } item || watch.Elapsed >= item.Until) return;
                state.Playlists.Find(list => list.Id == item.List)?.Insert(item.Index, item.Item);
                removedPlaylistItem = null;
            }
            else
            {
                if (undo is not { } change || DateTimeOffset.UtcNow >= change.Until) return;
                var list = state.Playlists.Find(playlist => playlist.Id == change.List);
                if (list != null) foreach (var id in change.Ids) list.Remove(id);
                undo = null;
            }
            SaveLists();
            Render();
        }, "LinkButton"));
        PlaylistUndoBar.Content = row;
    }
}
