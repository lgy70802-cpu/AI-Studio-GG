using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDisplay.Library;

namespace DeskDisplay.App.Views;

public partial class CreatorView
{
    private async void PickBackgroundImage(object sender, RoutedEventArgs e)
    {
        if (Editor is not { CanEdit: true } editor) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (picker.ShowDialog() == true) await editor.InsertImageFilesAsync([picker.FileName], 0, 0, true);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T result) yield return result; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    private IEnumerable<Expander> WorkspaceSections => new FrameworkElement[] { LeftPanel, RightPanel, CanvasLayoutBar }.SelectMany(Descendants<Expander>);
    private ThemeWorkbenchState CaptureWorkbench() => new(LeftPanel.Visibility == Visibility.Visible, RightPanel.Visibility == Visibility.Visible, Canvas.ShowGuides, Editor!.LeftTabIndex, Editor.SelectedMaterial.Name, WorkspaceSections.Where(e => e.IsExpanded).Select(e => e.Header?.ToString() ?? "").Distinct().ToArray(), Editor.Project.ActiveLayout.Layers.Where(l => l.GroupId is Guid group && Editor.IsGroupCollapsed(group)).Select(l => l.GroupId!.Value).Distinct().ToArray());
    private void RestoreWorkbench(ThemeWorkbenchState? state)
    {
        LeftPanel.Visibility = state?.LeftOpen == false ? Visibility.Collapsed : Visibility.Visible;
        RightPanel.Visibility = state?.RightOpen == false ? Visibility.Collapsed : Visibility.Visible;
        LeftColumn.Width = new(state?.LeftOpen == false ? 0 : 280); RightColumn.Width = new(state?.RightOpen == false ? 0 : 260);
        Canvas.ShowGuides = state?.Guides ?? true;
        if (FindName("GuideMenu") is MenuItem guides) guides.IsChecked = Canvas.ShowGuides;
        var expanded = state?.ExpandedSections ?? ["文字样式", "时钟设置", "图片设置", "外观"];
        foreach (var section in WorkspaceSections) section.IsExpanded = expanded.Contains(section.Header?.ToString() ?? "");
    }
    private void InitializeWorkbench()
    {
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler((_, _) => CaptureViewport()));
        AddHandler(Expander.CollapsedEvent, new RoutedEventHandler((_, _) => CaptureViewport()));
        Canvas.IsMouseCaptureWithinChanged += (_, _) => PositionSelectionActions();
        StageScroll.IsMouseCaptureWithinChanged += (_, _) => PositionSelectionActions();
        Canvas.InteractionChanged += (_, _) => PositionSelectionActions();
        CanvasWorkspace.AllowDrop = true;
        CanvasWorkspace.DragOver += (_, e) => { e.Effects = e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any(f => System.IO.File.Exists(f) && new[] { ".png", ".jpg", ".jpeg" }.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())) && Editor?.CanEdit == true ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        CanvasWorkspace.Drop += async (_, e) =>
        {
            if (Editor is not { CanEdit: true } editor || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
            var point = e.GetPosition(Canvas);
            e.Handled = true;
            await editor.InsertImageFilesAsync(paths, point.X / Math.Max(1, Canvas.ActualWidth) * editor.CanvasWidth, point.Y / Math.Max(1, Canvas.ActualHeight) * editor.CanvasHeight);
        };
    }
}
