using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    void ShowTrash()
    {
        state.PurgeTrash(DateTimeOffset.UtcNow);
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("回收站", panel, 740, 580);
        dialog.MinWidth = 480;
        dialog.MinHeight = 360;
        panel.Children.Add(HomeText("内容保留 30 天。恢复后可在资源库中查找。", 13, muted: true));
        var rows = new StackPanel();
        panel.Children.Add(rows);
        Draw();
        dialog.ShowDialog();

        void Draw()
        {
            rows.Children.Clear();
            foreach (var item in state.Trash.OrderByDescending(item => item.DeletedAt))
            {
                var resource = item.Entry.Resource;
                var canvas = resource.Layouts.FirstOrDefault(size => size.Width > 0 && size.Height > 0);
                var row = new Grid { Margin = new Thickness(0, 12, 0, 0) };
                row.ColumnDefinitions.Add(new() { Width = new GridLength(100) });
                row.ColumnDefinitions.Add(new());
                row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                var preview = new Border { Width = 88, Height = 64, CornerRadius = new CornerRadius(8),
                    Background = (System.Windows.Media.Brush)FindResource("HomePreviewBrush"), ClipToBounds = true };
                if (canvas.Width > 0 && canvas.Height > 0)
                {
                    var project = item.Theme ?? (item.Entry.Type is "主题" or "时钟"
                        ? new ThemeProject(resource.Id, resource.Layouts.Select(size => DemoAppearance(resource.Id, size)).ToList()) : null);
                    var art = Thumbnail(resource, canvas, 88, 64, previewProject: project);
                    var source = item.Theme?.BackgroundFile ?? library.ThumbnailPath(item.Entry) ?? library.MediaPath(item.Entry);
                    if (item.Theme == null && source != null && System.IO.File.Exists(source))
                    {
                        try { preview.Child = new Image { Source = Load(source, 100), Stretch = System.Windows.Media.Stretch.Uniform }; }
                        catch { preview.Child = art; }
                    }
                    else preview.Child = art;
                }
                row.Children.Add(preview);
                var labels = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                var name = HomeText(resource.Name, 15, bold: true);
                name.TextWrapping = TextWrapping.NoWrap;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.ToolTip = resource.Name;
                labels.Children.Add(name);
                var remaining = Math.Max(0, Math.Ceiling((item.DeletedAt.AddDays(30) - DateTimeOffset.UtcNow).TotalDays));
                labels.Children.Add(HomeText($"{item.Entry.Type} · 删除于 {item.DeletedAt.ToLocalTime():MM-dd HH:mm}", 12, muted: true));
                labels.Children.Add(HomeText($"剩余 {remaining} 天 · {item.References.Count} 个轮播引用", 12, muted: true));
                Grid.SetColumn(labels, 1);
                row.Children.Add(labels);
                var restore = HomeButton("恢复", () =>
                {
                    var before = SessionStore.Capture(state, library, Scenario.SelectedIndex);
                    try
                    {
                        state.RestoreTrash(resource.Id, library, DateTimeOffset.UtcNow);
                        SaveWorkspace();
                    }
                    catch { state = SessionStore.Restore(before, library); throw; }
                    SyncLibrary();
                    Notice.Text = $"已恢复「{resource.Name}」。";
                    Render();
                    Draw();
                }, "SmallPrimaryButton");
                restore.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(restore, 2);
                row.Children.Add(restore);
                rows.Children.Add(row);
            }
            if (rows.Children.Count == 0) rows.Children.Add(HomeText("回收站为空。", 15, muted: true));
        }
    }
}
