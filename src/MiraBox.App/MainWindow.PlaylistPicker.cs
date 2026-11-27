using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    void AddContents(Playlist playlist) => BuildPlaylistPicker(playlist).ShowDialog();

    Window BuildPlaylistPicker(Playlist playlist)
    {
        var picked = new HashSet<string>(StringComparer.Ordinal);
        var present = playlist.Items.Select(item => item.ResourceId).ToHashSet();
        var content = new Grid { Margin = new Thickness(18, 10, 18, 14) };
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new());
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var height = Math.Min(640, (ActualHeight > 0 ? ActualHeight : Height) * .8);
        var width = Math.Min(880, (ActualWidth > 0 ? ActualWidth : Width) - 36);
        var dialog = Dialog("添加内容到「" + playlist.Name + "」", content, width, height, scrollContent: false);
        dialog.MaxHeight = height;
        var header = new StackPanel();
        var search = new TextBox { Tag = "搜索资源名称", Margin = new Thickness(0, 0, 0, 8) };
        search.SetResourceReference(StyleProperty, "SearchBox");
        AutomationProperties.SetName(search, "搜索资源名称");
        header.Children.Add(search);
        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(filters);
        content.Children.Add(header);
        var type = "全部";
        var typeChoices = new List<RadioButton>();
        foreach (var label in new[] { "全部", "壁纸", "主题", "时钟", "视频" })
        {
            var choice = new RadioButton { Content = label, GroupName = "PlaylistPickerTypes", IsChecked = label == type,
                Margin = new Thickness(0, 3, 8, 3) };
            choice.SetResourceReference(StyleProperty, "PlaylistMoodChoice");
            typeChoices.Add(choice);
            filters.Children.Add(choice);
        }
        var showOther = new CheckBox { Content = "显示其他尺寸", Margin = new Thickness(3, 7, 10, 4) };
        showOther.SetResourceReference(StyleProperty, "Switch");
        filters.Children.Add(showOther);
        var sort = new ComboBox { ItemsSource = new[] { "最近添加", "最常用", "名称" },
            SelectedIndex = 0, MinWidth = 105, Margin = new Thickness(0, 0, 0, 3) };
        AutomationProperties.SetName(sort, "排序");
        filters.Children.Add(sort);

        var gallery = new UniformGrid { Columns = 3 };
        var results = new StackPanel();
        var noResults = HomeText("没有找到符合筛选条件的内容。", 13, muted: true);
        results.Children.Add(noResults);
        results.Children.Add(gallery);
        var scroll = new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        content.Children.Add(scroll);

        var summary = HomeText("", 13, muted: true);
        var actions = new WrapPanel();
        actions.Children.Add(Action("取消", dialog.Close));
        var add = HomeButton("", () =>
        {
            if (picked.Count == 0) return;
            Added(playlist, picked);
            dialog.Close();
            Render();
        }, "PrimaryButton");
        actions.Children.Add(add);
        var footer = HomeHeader(summary, actions);
        footer.Margin = new Thickness(0, 12, 0, 0);
        Grid.SetRow(footer, 2);
        content.Children.Add(footer);

        foreach (var choice in typeChoices)
            choice.Click += (_, _) => { type = (string)choice.Content; Draw(); };
        showOther.Click += (_, _) => Draw();
        sort.SelectionChanged += (_, _) => Draw();
        search.TextChanged += (_, _) => Draw();
        Draw();
        return dialog;

        void UpdateSummary()
        {
            summary.Text = $"已选 {picked.Count} 项 · 约 {PlaylistLength(TimeSpan.FromSeconds(picked.Count * 30))}";
            add.Content = $"添加 {picked.Count} 项";
            add.IsEnabled = picked.Count > 0;
        }

        void Draw()
        {
            gallery.Children.Clear();
            var canvas = PlaylistCanvas(playlist);
            foreach (var entry in library.Query(new(Query: search.Text.Trim(), Type: type, Sort: sort.SelectedItem as string ?? "最近添加")))
            {
                var resource = entry.Resource;
                if (showOther.IsChecked != true && Workspace.Difference(resource, canvas) is not <= .05) continue;
                var exists = present.Contains(resource.Id);
                var thumb = Thumbnail(resource, canvas, Math.Min(248, (width - 80) / 3), 128, "cover");
                var tile = new Grid { Width = thumb.Width, Height = thumb.Height, HorizontalAlignment = HorizontalAlignment.Center };
                tile.Clip = new RectangleGeometry(new Rect(0, 0, thumb.Width, thumb.Height), 12, 12);
                tile.Children.Add(new Border { CornerRadius = new CornerRadius(12), Child = thumb, HorizontalAlignment = HorizontalAlignment.Center });
                var mark = PlaylistBadge(exists ? "已添加" : "✓", exists ? "#E1ECF2" : "#2468E8", exists ? "#53677C" : "#FFFFFF");
                if (!exists) mark.Background = (Brush)FindResource("BrandBrush");
                mark.Margin = new Thickness(5);
                mark.HorizontalAlignment = exists ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                mark.VerticalAlignment = VerticalAlignment.Top;
                mark.Visibility = exists || picked.Contains(resource.Id) ? Visibility.Visible : Visibility.Collapsed;
                tile.Children.Add(mark);
                var button = Action(resource.Name, () =>
                {
                    if (!picked.Add(resource.Id)) picked.Remove(resource.Id);
                    mark.Visibility = picked.Contains(resource.Id) ? Visibility.Visible : Visibility.Collapsed;
                    UpdateSummary();
                });
                button.SetResourceReference(StyleProperty, "PlaylistPickerTile");
                button.Content = tile;
                button.Margin = new Thickness(3, 4, 7, 4);
                button.IsEnabled = !exists;
                button.ToolTip = resource.Name;
                AutomationProperties.SetName(button, exists ? $"{resource.Name}，已添加" : resource.Name);
                gallery.Children.Add(button);
            }
            noResults.Visibility = gallery.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSummary();
        }
    }
}
