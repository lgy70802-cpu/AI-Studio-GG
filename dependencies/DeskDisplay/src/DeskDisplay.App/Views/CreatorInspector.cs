using System.Windows;
using System.Windows.Media;

namespace DeskDisplay.App.Views;

public partial class CreatorView
{
    private string? _inspectorSelection;
    private void SyncInspectorSelection()
    {
        if (Editor is not { } editor) return;
        var selection = editor.Project.Id + ":" + string.Join(",", editor.SelectedLayers.Select(l => l.Id).Order());
        if (selection == _inspectorSelection) return;
        _inspectorSelection = selection;
        SelectInspector(canvas: editor.SelectedLayers.Count == 0);
    }
    private void SelectInspector(bool canvas)
    {
        ElementProperties.Visibility = canvas ? Visibility.Collapsed : Visibility.Visible;
        CanvasProperties.Visibility = canvas ? Visibility.Visible : Visibility.Collapsed;
        var soft = (Brush)FindResource("Brush.PrimarySoft"); var primary = (Brush)FindResource("Brush.Primary");
        var secondary = (Brush)FindResource("Brush.TextSecondary");
        CanvasPropertiesButton.Background = canvas ? soft : Brushes.Transparent; CanvasPropertiesButton.Foreground = canvas ? primary : secondary;
        ElementPropertiesButton.Background = canvas ? Brushes.Transparent : soft; ElementPropertiesButton.Foreground = canvas ? secondary : primary;
    }
    private void ShowCanvasProperties(object sender, RoutedEventArgs e)
    {
        RightPanel.Visibility = Visibility.Visible; RightColumn.Width = new(260); SelectInspector(canvas: true);
    }
    private void ShowElementProperties(object sender, RoutedEventArgs e) => SelectInspector(canvas: false);
    private void ShowAddContent(object sender, RoutedEventArgs e)
    {
        if (Editor is null) return;
        LeftPanel.Visibility = Visibility.Visible; LeftColumn.Width = new(280); Editor.LeftTabIndex = 0;
    }
    private void ShowImageMaterials(object sender, RoutedEventArgs e)
    {
        ShowAddContent(sender, e);
        if (Editor is { } editor) editor.SelectedMaterial = editor.ResolveMaterialChoice("图片");
    }
}
