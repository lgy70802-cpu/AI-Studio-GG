using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;

namespace DeskDisplay.App.Views;

public partial class CreatorView : UserControl
{
    private CreatorViewModel? Editor => (DataContext as MainViewModel)?.Creator;
    private CreatorViewModel? _observed;
    private Point _layerStart, _panStart;
    private Guid? _layerDrag;
    private bool _panning, _propertyDrag;
    private double _panX, _panY;
    private ListBoxItem? _dropTarget;
    public CreatorView()
    {
        InitializeComponent();
        InitializeViewportNavigation();
        InitializeWorkbench();
        Loaded += (_, _) => { Observe(); RestoreViewport(); };
        Unloaded += (_, _) => StopObserving();
        DataContextChanged += (_, _) => { StopObserving(); if (IsLoaded) Observe(); };
        StageScroll.SizeChanged += (_, _) => { FitCanvas(); PositionSelectionActions(); };
        StageScroll.ScrollChanged += (_, _) => PositionSelectionActions();
        Canvas.LayoutUpdated += (_, _) => PositionSelectionActions();
        StageScroll.LostMouseCapture += (_, _) => { _panning = false; StageScroll.Cursor = _spacePanHeld ? Cursors.Hand : Cursors.Arrow; };
        IsVisibleChanged += (_, _) => { if (IsVisible) Dispatcher.BeginInvoke(RestoreViewport); };
    }
    private void Observe() { StopObserving(); _observed = Editor; if (_observed is not null) { _observed.CommitPendingEdits += CommitCanvasText; _observed.PropertyChanged += Changed; _observed.LeavingEditorLayout += LeavingLayout; _observed.FocusCanvasSelection += FocusSelection; SyncInspectorSelection(); } }
    private void CommitCanvasText(object? sender, EventArgs e) => Canvas.CommitInlineTextEdit();
    private void StopObserving() { CaptureViewport(); if (_observed is not null) { _observed.CommitPendingEdits -= CommitCanvasText; _observed.PropertyChanged -= Changed; _observed.LeavingEditorLayout -= LeavingLayout; _observed.FocusCanvasSelection -= FocusSelection; } _observed = null; }
    private void Changed(object? sender, PropertyChangedEventArgs e) { QueueSelectionActions(); if (e.PropertyName is nameof(CreatorViewModel.SelectedLayer) or nameof(CreatorViewModel.IsEditor)) SyncInspectorSelection(); if (e.PropertyName == nameof(CreatorViewModel.MaterialImportRevision)) Dispatcher.BeginInvoke(() => { if (MaterialList.Items.Count > 0) MaterialList.ScrollIntoView(MaterialList.Items[0]); }); if (e.PropertyName is nameof(CreatorViewModel.LeftTabIndex) or nameof(CreatorViewModel.SelectedMaterial) or nameof(CreatorViewModel.LayerRows)) CaptureViewport(); if (e.PropertyName == nameof(CreatorViewModel.SelectedTemplateCard) && Editor?.IsTemplates == true) Dispatcher.BeginInvoke(() => { if (TemplateList.SelectedItem is { } item) TemplateList.ScrollIntoView(item); }); if (e.PropertyName == nameof(CreatorViewModel.ViewportRevision)) RestoreViewport(); if (e.PropertyName == nameof(CreatorViewModel.SelectedLayerRow)) CaptureViewport(); if (e.PropertyName == nameof(CreatorViewModel.SelectedLayerRow)) Dispatcher.BeginInvoke(() => { if (LayerList.SelectedItem is { } selected) LayerList.ScrollIntoView(selected); }); if (e.PropertyName is nameof(CreatorViewModel.Aspect) or nameof(CreatorViewModel.IsEditor)) Dispatcher.BeginInvoke(FitCanvas); }
    private void FitCanvas()
    {
        if (Editor is null || StageScroll.ActualWidth < 10 || StageScroll.ActualHeight < 10) return;
        var ratio = Editor.Aspect; var width = Math.Max(40, StageScroll.ActualWidth - 32); var height = Math.Max(40, StageScroll.ActualHeight - 32);
        var fitWidth = Math.Min(width, height * ratio); var zoom = ZoomSlider.Value;
        Canvas.Width = fitWidth * zoom; Canvas.Height = fitWidth / ratio * zoom;
        StageHost.MinWidth = width; StageHost.MinHeight = height;
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Canvas is not null) { FitCanvas(); CaptureViewport(); } }
    private void Fit(object sender, RoutedEventArgs e) { ZoomSlider.Value = 1; FitCanvas(); StageScroll.ScrollToHorizontalOffset(0); StageScroll.ScrollToVerticalOffset(0); CaptureViewport(); }
    private void ToggleGuideMenu(object sender, RoutedEventArgs e) { if (sender is MenuItem item) { Canvas.ShowGuides = item.IsChecked; Canvas.InvalidateArrange(); foreach (UIElement child in Canvas.Children) child.InvalidateVisual(); CaptureViewport(); } }
    private void Guides(object sender, RoutedEventArgs e) { if (Canvas is not null) { Canvas.ShowGuides = (sender as CheckBox)?.IsChecked == true; Canvas.InvalidateArrange(); foreach (UIElement child in Canvas.Children) child.InvalidateVisual(); CaptureViewport(); } }
    private void Landscape(object sender, RoutedEventArgs e) { if (Editor is { } editor) editor.Orientation = ThemeOrientation.Landscape; }
    private void Portrait(object sender, RoutedEventArgs e) { if (Editor is { } editor) editor.Orientation = ThemeOrientation.Portrait; }
    private bool _selectionActionsQueued;
    private void QueueSelectionActions()
    {
        if (_selectionActionsQueued) return;
        _selectionActionsQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => { _selectionActionsQueued = false; PositionSelectionActions(); });
    }
    private void PositionSelectionActions()
    {
        if (Editor is not { IsEditor: true } editor || !CanvasWorkspace.IsVisible) return;
        if (!editor.HasSelection || Canvas.IsMouseCaptured || StageScroll.IsMouseCaptured || Canvas.IsInlineEditing) { SelectionActions.Visibility = Visibility.Collapsed; return; }
        SelectionActions.Visibility = Visibility.Visible;
        var bounds = editor.SelectionBounds;
        var point = Canvas.TranslatePoint(new Point(bounds.X / editor.CanvasWidth * Canvas.ActualWidth, bounds.Y / editor.CanvasHeight * Canvas.ActualHeight), CanvasWorkspace);
        // DesiredSize includes the positioning margin; use the arranged body to avoid feedback.
        var size = new Size(SelectionActions.ActualWidth > 0 ? SelectionActions.ActualWidth : 200, SelectionActions.ActualHeight > 0 ? SelectionActions.ActualHeight : 36);
        var frame = Canvas.TranslatePoint(new Point(0, 0), CanvasWorkspace);
        var below = Canvas.TranslatePoint(new Point(Canvas.ActualWidth, Canvas.ActualHeight), CanvasWorkspace);
        var x = Math.Clamp(point.X, 4, Math.Max(4, CanvasWorkspace.ActualWidth - size.Width - 4));
        // Prefer the empty area outside the whole rendered frame, so clocks and neighbors remain visible.
        var y = frame.Y - size.Height - 10;
        if (y < 4) y = below.Y + 10;
        if (y + size.Height > CanvasWorkspace.ActualHeight - 4) {
            x = below.X + 10;
            y = Math.Clamp(point.Y, 4, Math.Max(4, CanvasWorkspace.ActualHeight - size.Height - 4));
            if (x + size.Width > CanvasWorkspace.ActualWidth - 4) { x = frame.X - size.Width - 10; if (x < 4) { SelectionActions.Visibility = Visibility.Collapsed; return; } }
        }
        SelectionActions.Margin = new Thickness(x, y, 0, 0);
    }
    private void ChooseEditorMaterial(object sender, SelectionChangedEventArgs e) { if (Editor is { } editor && e.AddedItems.Count > 0) editor.LeftTabIndex = 0; }
    private void ReturnToMaterials(object sender, MouseButtonEventArgs e) { if (Editor is { } editor) editor.LeftTabIndex = 0; }
    private void ChooseEditorLayers(object sender, RoutedEventArgs e) { if (Editor is { } editor) editor.LeftTabIndex = 1; }
    private void ToggleLeft(object sender, RoutedEventArgs e) { var show = LeftPanel.Visibility != Visibility.Visible; LeftPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed; LeftColumn.Width = new GridLength(show ? 280 : 0); Dispatcher.BeginInvoke(() => Fit(this, new RoutedEventArgs())); }
    private void ToggleRight(object sender, RoutedEventArgs e) { var show = RightPanel.Visibility != Visibility.Visible; RightPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed; RightColumn.Width = new GridLength(show ? 260 : 0); Dispatcher.BeginInvoke(() => Fit(this, new RoutedEventArgs())); }
    private void PreviewForApply(object sender, RoutedEventArgs e)
    {
        Canvas.CommitInlineTextEdit();
        if (DataContext is MainViewModel { Devices.Managed: { PreviewFactory: not null } }) OpenDevicePreview(sender, e);
        else Editor?.PreviewCommand.Execute(null);
    }
    private void CheckBeforeApply(object sender, RoutedEventArgs e) { Canvas.CommitInlineTextEdit(); if (Editor is { } editor && new CreatorPreflightDialog(editor, true) { Owner = Window.GetWindow(this) }.ShowDialog() == true) PreviewForApply(sender, e); }
    private void RenameContextLayer(object sender, RoutedEventArgs e) { if (LayerList.SelectedItem is { } row && LayerList.ItemContainerGenerator.ContainerFromItem(row) is ListBoxItem item) { var input = Descendants<TextBox>(item).FirstOrDefault(t => !t.IsReadOnly); input?.Focus(); input?.SelectAll(); } }
    private void DeviceSettings(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.SelectedPage = AppPage.Devices; }
    private void OpenMore(object sender, RoutedEventArgs e) { if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.IsOpen = true; } }
    private static T? Ancestor<T>(DependencyObject? item) where T : DependencyObject
    {
        while (item is not null) { if (item is T found) return found; item = item is Visual ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item); } return null;
    }
    private void SelectContextLayer(object sender, MouseButtonEventArgs e)
    {
        if (Editor is not { } editor || Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not CreatorLayerRow row || editor.IsLayerSelected(row.Layer.Id)) return;
        if (row.IsGroup) editor.SelectCanvasLayer(row.Layer, false); else editor.SelectedLayer = row.Layer;
    }
    private void LayerDragStart(object sender, MouseButtonEventArgs e)
    {
        _layerDrag = null;
        if (Ancestor<TextBox>(e.OriginalSource as DependencyObject) is not null || Ancestor<Button>(e.OriginalSource as DependencyObject) is not null) return;
        _layerStart = e.GetPosition(LayerList); _layerDrag = (Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as CreatorLayerRow)?.Layer.Id;
    }
    private void LayerDragMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _layerDrag is not Guid id) return;
        var delta = e.GetPosition(LayerList) - _layerStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _layerDrag = null;
        try { DragDrop.DoDragDrop(LayerList, new DataObject("StudioDisplay.ThemeLayer", id), DragDropEffects.Move); }
        finally { ClearDropTarget(); }
    }
    private void ClearDropTarget() { if (_dropTarget is not null) _dropTarget.Tag = null; _dropTarget = null; }
    private void LayerDragOver(object sender, DragEventArgs e)
    {
        ClearDropTarget(); e.Effects = DragDropEffects.None;
        if (e.Data.GetData("StudioDisplay.ThemeLayer") is Guid id && Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: CreatorLayerRow row } item && row.Layer.Id != id)
        { _dropTarget = item; item.Tag = "drop"; e.Effects = DragDropEffects.Move; }
        e.Handled = true;
    }
    private void LayerDragLeave(object sender, DragEventArgs e) => ClearDropTarget();
    private void LayerDrop(object sender, DragEventArgs e) { ClearDropTarget(); if (e.Data.GetData("StudioDisplay.ThemeLayer") is Guid id && Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is CreatorLayerRow target) { Editor?.Reorder(id, target.Layer.Id); e.Handled = true; } }
    private void LayerNameKey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox text || e.Key is not (Key.Enter or Key.Escape)) return;
        var binding = text.GetBindingExpression(TextBox.TextProperty);
        if (e.Key == Key.Enter) binding?.UpdateSource(); else binding?.UpdateTarget();
        Canvas.Focus(); e.Handled = true;
    }
    private void SelectLayerName(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox { DataContext: CreatorLayerRow row } && Editor is { } editor) if (row.IsGroup) editor.SelectCanvasLayer(row.Layer, false); else editor.SelectedLayer = row.Layer;
    }
    private void StageDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Middle && !(e.ChangedButton == MouseButton.Left && (_spacePanHeld || Keyboard.IsKeyDown(Key.Space)))) return; _panning = true; StageScroll.Cursor = Cursors.Hand; _panStart = e.GetPosition(StageScroll); _panX = StageScroll.HorizontalOffset; _panY = StageScroll.VerticalOffset; if (!StageScroll.CaptureMouse()) { ResetPan(this, EventArgs.Empty); return; } e.Handled = true; }
    private void StageMove(object sender, MouseEventArgs e) { if (!_panning) return; var delta = e.GetPosition(StageScroll) - _panStart; StageScroll.ScrollToHorizontalOffset(_panX - delta.X); StageScroll.ScrollToVerticalOffset(_panY - delta.Y); e.Handled = true; }
    private void StageUp(object sender, MouseButtonEventArgs e) { if (!_panning) return; _panning = false; StageScroll.Cursor = _spacePanHeld ? Cursors.Hand : Cursors.Arrow; StageScroll.ReleaseMouseCapture(); CaptureViewport(); e.Handled = true; }
    private void PropertyDragStart(object sender, MouseButtonEventArgs e)
    {
        if (Editor is { CanEditSelection: true } editor && Ancestor<Slider>(e.OriginalSource as DependencyObject) is not null) { _propertyDrag = true; editor.BeginGesture(); }
    }
    private void PropertyDragEnd(object sender, RoutedEventArgs e) { if (_propertyDrag) { _propertyDrag = false; Editor?.EndGesture(); } }
    private void BrowseMaterials(object sender, RoutedEventArgs e) { if (Editor is { CanEdit: true } editor) new CreatorMaterialBrowser(editor) { Owner = Window.GetWindow(this) }.ShowDialog(); }
    private void PickColor(object sender, RoutedEventArgs e) { if (Editor is not { CanEditSelection: true } editor) return; var picker = new CreatorColorPicker(editor.Color) { Owner = Window.GetWindow(this) }; if (picker.ShowDialog() == true) editor.Color = picker.SelectedColor; }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Editor is not { IsEditor: true } editor) return;
        // Platform adapter boundary: Avalonia maps macOS Command/Meta to its native modifier.
        var primary = OperatingSystem.IsMacOS() ? ModifierKeys.Windows : ModifierKeys.Control;
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & primary) != 0 && e.Key == Key.S) { Canvas.CommitInlineTextEdit(); if (Keyboard.FocusedElement is TextBox focused) focused.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); editor.SaveCommand.Execute(null); e.Handled = true; return; }
        if (e.OriginalSource is TextBox or ComboBox || Ancestor<TextBox>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.Key == Key.Space && modifiers == ModifierKeys.None) { _spacePanHeld = true; StageScroll.Cursor = Cursors.Hand; e.Handled = true; return; }
        if ((modifiers & primary) != 0)
        {
            switch (e.Key) { case Key.Home: editor.BringFrontCommand.Execute(null); break; case Key.End: editor.SendBackCommand.Execute(null); break; case Key.Z: if ((modifiers & ModifierKeys.Shift) != 0) editor.RedoCommand.Execute(null); else editor.UndoCommand.Execute(null); break; case Key.G: if ((modifiers & ModifierKeys.Shift) != 0) editor.UngroupCommand.Execute(null); else editor.GroupCommand.Execute(null); break; case Key.A: editor.SelectLayers(editor.Project.ActiveLayout.Layers.Where(l => l.Visible && !l.Locked && l.Kind != ThemeElementKind.Wallpaper).Select(l => l.Id)); break; case Key.D: editor.CopyCommand.Execute(null); editor.PasteCommand.Execute(null); break; case Key.C: editor.CopyCommand.Execute(null); break; case Key.V: editor.PasteCommand.Execute(null); break; default: return; }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape) { editor.SelectLayers([]); e.Handled = true; }
        else if (e.Key == Key.F2) { var selected = LayerList.ItemContainerGenerator.ContainerFromItem(LayerList.SelectedItem) as ListBoxItem; if (selected is not null) { var name = Descendants<TextBox>(selected).FirstOrDefault(t => !t.IsReadOnly); name?.Focus(); name?.SelectAll(); } e.Handled = true; }
        else if (e.Key == Key.Delete) { editor.DeleteCommand.Execute(null); e.Handled = true; }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) { var step = (modifiers & ModifierKeys.Shift) != 0 ? 10 : 1; editor.MoveBy(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0); e.Handled = true; }
    }
}

