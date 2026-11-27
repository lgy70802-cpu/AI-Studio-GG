using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskDisplay.App.Views;

public partial class CreatorView
{
    private Guid _displayedProject;
    private DeskDisplay.Core.Models.ThemeOrientation _displayedOrientation;
    private Guid? _displayedLayout;
    private bool _restoringViewport, _spacePanHeld;
    private int _viewportGeneration;
    private Window? _hostWindow;
    private void InitializeViewportNavigation()
    {
        StageScroll.ScrollChanged += (_, _) => CaptureViewport();
        StageScroll.LostMouseCapture += (_, _) => { if (_panning) ResetPan(this, EventArgs.Empty); };
        StageScroll.PreviewMouseWheel += (_, e) => { if (_panning || Mouse.Captured is not null) return; ZoomViewportAt(e.GetPosition(StageScroll), ZoomSlider.Value * Math.Pow(1.1, e.Delta / 120d)); e.Handled = true; };
        PreviewKeyUp += (_, e) => { if (e.Key == Key.Space) { _spacePanHeld = false; if (!_panning) StageScroll.Cursor = Cursors.Arrow; } };
        Loaded += (_, _) => { _hostWindow = Window.GetWindow(this); if (_hostWindow is not null) _hostWindow.Deactivated += ResetPan; };
        Unloaded += (_, _) => { if (_hostWindow is not null) _hostWindow.Deactivated -= ResetPan; _hostWindow = null; ResetPan(this, EventArgs.Empty); };
    }
    private void ResetPan(object? sender, EventArgs e) { _spacePanHeld = false; _panning = false; if (StageScroll.IsMouseCaptured) StageScroll.ReleaseMouseCapture(); StageScroll.Cursor = Cursors.Arrow; }
    private void CaptureViewport()
    {
        if (_restoringViewport || Editor is not { } editor || editor.Project.Id != _displayedProject || editor.Orientation != _displayedOrientation || editor.Project.SelectedLayoutId != _displayedLayout || !editor.IsEditor) return;
        editor.CaptureEditorViewport(ZoomSlider.Value, StageScroll.ScrollableWidth > 0 ? Math.Clamp(StageScroll.HorizontalOffset / StageScroll.ScrollableWidth, 0, 1) : 0, StageScroll.ScrollableHeight > 0 ? Math.Clamp(StageScroll.VerticalOffset / StageScroll.ScrollableHeight, 0, 1) : 0, CaptureWorkbench());
    }
    private void LeavingLayout(object? sender, EventArgs e) { Canvas.CommitInlineTextEdit(); CaptureViewport(); }
    private void FocusSelection(object? sender, EventArgs e)
    {
        if (Editor?.SelectedLayer is not { } layer) return;
        var center = Canvas.TranslatePoint(new Point((layer.X + layer.Width / 2) / Editor.Project.ActiveLayout.Width * Canvas.ActualWidth, (layer.Y + layer.Height / 2) / Editor.Project.ActiveLayout.Height * Canvas.ActualHeight), StageScroll);
        StageScroll.ScrollToHorizontalOffset(StageScroll.HorizontalOffset + center.X - StageScroll.ViewportWidth / 2);
        StageScroll.ScrollToVerticalOffset(StageScroll.VerticalOffset + center.Y - StageScroll.ViewportHeight / 2);
    }
    private void RestoreViewport()
    {
        if (Editor is not { } editor || !editor.IsEditor) return;
        var state = editor.EditorViewport; var generation = ++_viewportGeneration; _restoringViewport = true;
        _displayedProject = editor.Project.Id; _displayedOrientation = editor.Orientation; _displayedLayout = editor.Project.SelectedLayoutId;
        RestoreWorkbench(state?.Workbench);
        ZoomSlider.Value = state?.Zoom ?? 1; FitCanvas();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (generation != _viewportGeneration) return;
            StageScroll.UpdateLayout(); RestoreWorkbench(state?.Workbench); StageScroll.UpdateLayout();
            StageScroll.ScrollToHorizontalOffset((state?.Horizontal ?? 0) * StageScroll.ScrollableWidth);
            StageScroll.ScrollToVerticalOffset((state?.Vertical ?? 0) * StageScroll.ScrollableHeight);
            StageScroll.UpdateLayout(); _restoringViewport = false;
        });
    }
    public void ZoomViewportAt(Point anchor, double zoom)
    {
        Canvas.CommitInlineTextEdit();
        if (Canvas.ActualWidth <= 0 || Canvas.ActualHeight <= 0 || !double.IsFinite(zoom)) return;
        var local = StageScroll.TranslatePoint(anchor, Canvas); var x = local.X / Canvas.ActualWidth; var y = local.Y / Canvas.ActualHeight;
        ZoomSlider.Value = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum); FitCanvas(); StageScroll.UpdateLayout();
        var projected = Canvas.TranslatePoint(new Point(x * Canvas.ActualWidth, y * Canvas.ActualHeight), StageScroll);
        StageScroll.ScrollToHorizontalOffset(StageScroll.HorizontalOffset + projected.X - anchor.X);
        StageScroll.ScrollToVerticalOffset(StageScroll.VerticalOffset + projected.Y - anchor.Y);
        StageScroll.UpdateLayout(); CaptureViewport();
    }
}
