using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;

namespace DeskDisplay.App.Views;

/// <summary>WPF editing chrome only. All mutations use portable design coordinates and the shared edit session.</summary>
public sealed class ThemeCanvas : Grid
{
    private readonly SceneView _view = new();
    private readonly Overlay _overlay;
    private CreatorViewModel? _observed;
    private int _handle = -1;
    private bool _dragging, _guideDragging, _guideVertical;
    private int _guideIndex = -1;
    private double _guidePosition;
    private Point _dragStart;
    private ThemeLayer? _startLayer;
    private ThemeLayer[] _startGroup = [];
    private bool _marquee, _additive;
    private Point _marqueeEnd, _lastDesign;
    public bool ShowGuides { get; set; } = true;
    private TextBox? _inlineText;
    private Guid? _inlineLayer;
    public bool IsInlineEditing => _inlineText is not null;
    public event EventHandler? InteractionChanged;
    public bool BeginInlineTextEdit()
    {
        if (Editor is not { CanEdit: true, SelectedLayer: { Kind: ThemeElementKind.Text, Locked: false, Visible: true } layer }) return false;
        CommitInlineTextEdit();
        var (scale, origin) = Mapping();
        if (!double.IsFinite(scale) || scale <= 0) return false;
        _inlineLayer = layer.Id;
        _inlineText = new TextBox { Text = layer.Text, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily(layer.FontFamily), FontSize = Math.Max(8, layer.FontSize * scale), FontWeight = FontWeight.FromOpenTypeWeight(layer.Bold ? Math.Max(700,layer.FontWeightValue) : layer.FontWeightValue), FontStretch = FontStretch.FromOpenTypeStretch(layer.FontStretchValue), Foreground = new SolidColorBrush(Color.FromRgb(32, 44, 67)), Background = Brushes.White, BorderBrush = Brushes.CornflowerBlue, BorderThickness = new Thickness(1), Padding = new Thickness(0), Width = Math.Max(30, layer.Width * scale), Height = Math.Max(26, layer.Height * scale), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Math.Max(0, origin.X + layer.X * scale), Math.Max(0, origin.Y + layer.Y * scale), 0, 0), RenderTransform = new RotateTransform(layer.Rotation), RenderTransformOrigin = new Point(.5, .5) };
        _inlineText.Width = Math.Clamp(layer.Width * scale, Math.Min(120, ActualWidth), ActualWidth); _inlineText.Height = Math.Clamp(layer.Height * scale, Math.Min(70, ActualHeight), ActualHeight);
        _inlineText.Margin = new Thickness(Math.Clamp(origin.X + layer.X * scale, 0, Math.Max(0, ActualWidth - _inlineText.Width)), Math.Clamp(origin.Y + layer.Y * scale, 0, Math.Max(0, ActualHeight - _inlineText.Height)), 0, 0);
        _inlineText.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { CommitInlineTextEdit(false); e.Handled = true; } else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { CommitInlineTextEdit(); e.Handled = true; } };
        _inlineText.LostKeyboardFocus += (sender, _) => { if (ReferenceEquals(sender, _inlineText)) CommitInlineTextEdit(); };
        var input = _inlineText; Children.Add(input); input.Focus(); input.SelectAll(); InteractionChanged?.Invoke(this, EventArgs.Empty); return true;
    }
    public void CommitInlineTextEdit(bool commit = true)
    {
        if (_inlineText is not { } input) return;
        var id = _inlineLayer; _inlineText = null; _inlineLayer = null;
        Children.Remove(input);
        if (commit && Editor is { CanEdit: true, SelectedLayer: { Locked: false } layer } editor && layer.Id == id && layer.Text != input.Text) { editor.BeginGesture(); editor.Text = input.Text; editor.EndGesture(); }
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }
    private CreatorViewModel? Editor => DataContext as CreatorViewModel;
    public ThemeCanvas()
    {
        ClipToBounds = true; Background = Brushes.Transparent; Focusable = true;
        _view.SetBinding(SceneView.SceneProperty, new Binding("Scene")); _view.SetBinding(SceneView.AspectRatioOverrideProperty, new Binding("Aspect"));
        Children.Add(_view); _overlay = new Overlay(this); Children.Add(_overlay);
        Loaded += (_, _) => Observe(); Unloaded += (_, _) => StopObserving(); DataContextChanged += (_, _) => { StopObserving(); if (IsLoaded) Observe(); };
        MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += Up; LostMouseCapture += (_, _) => EndDrag();
        var menu = new ContextMenu(); menu.SetBinding(DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
        foreach (var (title, command) in new[] { ("复制", "CopyCommand"), ("粘贴", "PasteCommand"), ("组合", "GroupCommand"), ("取消组合", "UngroupCommand"), ("置顶（Ctrl+Home）", "BringFrontCommand"), ("置底（Ctrl+End）", "SendBackCommand"), ("移回画布", "BringInsideCommand"), ("上移", "LayerUpCommand"), ("下移", "LayerDownCommand"), ("删除", "DeleteCommand") })
        { var item = new MenuItem { Header = title }; item.SetBinding(MenuItem.CommandProperty, new Binding(command)); menu.Items.Add(item); }
        ContextMenu = menu;
        MouseRightButtonDown += (_, e) => { if (Editor is not { } editor) return; var p = Design(e.GetPosition(this)); var hit = editor.Layers.FirstOrDefault(l => l.Visible && new Rect(l.X, l.Y, l.Width, l.Height).Contains(Unrotate(p, l))); if (hit is not null && !editor.IsLayerSelected(hit.Id)) editor.SelectCanvasLayer(hit, false); };
    }
    private void Observe() { StopObserving(); _observed = Editor; if (_observed is not null) _observed.PropertyChanged += Changed; }
    private void StopObserving() { CommitInlineTextEdit(false); if (_observed is not null) _observed.PropertyChanged -= Changed; _observed = null; }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (_inlineText is not null && (Editor?.SelectedLayer?.Id != _inlineLayer || Editor?.SelectedLayer?.Locked == true || Editor?.IsEditor != true)) CommitInlineTextEdit(false); if (e.PropertyName is nameof(CreatorViewModel.SelectedLayer) or nameof(CreatorViewModel.SelectionCount) or nameof(CreatorViewModel.Aspect) or nameof(CreatorViewModel.X) or nameof(CreatorViewModel.Width)) _overlay.InvalidateVisual(); }
    private (double Scale, Point Origin) Mapping()
    {
        var layout = Editor!.Project.ActiveLayout; var scale = Math.Min(ActualWidth / layout.Width, ActualHeight / layout.Height);
        return (scale, new Point((ActualWidth - layout.Width * scale) / 2, (ActualHeight - layout.Height * scale) / 2));
    }
    private Point Design(Point p) { var (scale, origin) = Mapping(); return new Point((p.X - origin.X) / scale, (p.Y - origin.Y) / scale); }
    private static Point Unrotate(Point p, ThemeLayer l)
    {
        var matrix = Matrix.Identity; matrix.RotateAt(-l.Rotation, l.X + l.Width / 2, l.Y + l.Height / 2); return matrix.Transform(p);
    }
    public static bool ShouldStartMarquee(ThemeLayer? layer, bool selected) => layer is null || layer.Kind == ThemeElementKind.Wallpaper && layer.Locked;
    public static bool CanStartGuideDrag(ThemeLayer? layer, bool selected, bool nearGuide, bool onRuler, bool onResizeHandle = false) => !onResizeHandle && (onRuler || nearGuide && (layer is null || layer.Kind == ThemeElementKind.Wallpaper && !selected));
    public static int HitResizeHandle(Point design, ThemeLayer layer, double scale)
    {
        if (layer.Locked || !layer.Visible || !double.IsFinite(scale) || scale <= 0) return -1;
        var local = Unrotate(design, layer);
        var corners = new[] { new Point(layer.X, layer.Y), new Point(layer.X + layer.Width, layer.Y), new Point(layer.X, layer.Y + layer.Height), new Point(layer.X + layer.Width, layer.Y + layer.Height) };
        var nearest = Enumerable.Range(0, 4).OrderBy(i => (local - corners[i]).Length).First();
        return (local - corners[nearest]).Length * scale <= 10 ? nearest : -1;
    }
    private int ResizeHandleAt(Point design)
    {
        if (Editor is not { CanEdit: true } editor) return -1;
        if (editor.CanScaleSelection)
        {
            var b = editor.SelectionBounds;
            return HitResizeHandle(design, new ThemeLayer { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height }, Mapping().Scale);
        }
        return editor.IsSingleSelection && editor.SelectedLayer is { } selected ? HitResizeHandle(design, selected, Mapping().Scale) : -1;
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        if (_inlineText is not null) return;
        if (Editor is not { CanEdit: true } editor || ActualWidth <= 0 || ActualHeight <= 0) return;
        Focus(); _handle = -1; var p = Design(e.GetPosition(this)); var layer = editor.Project.ActiveLayout.Layers.Where(l => l.Visible).OrderByDescending(l => l.ZIndex).FirstOrDefault(l => new Rect(l.X, l.Y, l.Width, l.Height).Contains(Unrotate(p, l)));
        _handle = ResizeHandleAt(p);
        if (layer is not null || _handle >= 0) editor.LeftTabIndex = 1;
        if (_handle >= 0) layer = editor.SelectedLayer;
        if (e.ClickCount == 2 && layer is { Kind: ThemeElementKind.Text, Locked: false }) { EndDrag(); if (IsMouseCaptured) ReleaseMouseCapture(); editor.SelectedLayer = layer; BeginInlineTextEdit(); e.Handled = true; return; }
        if (e.ClickCount == 2 && _handle < 0 && layer is { Locked: false, IsVideo: false } && layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper)
        { editor.SelectedLayer = layer; editor.CropImageCommand.Execute(null); e.Handled = true; return; }
        if (ShowGuides && _handle < 0)
        {
            var (scale, origin) = Mapping(); var screen = e.GetPosition(this);
            var layout = editor.Project.ActiveLayout;
            var vertical = layout.VerticalGuides.FindIndex(v => Math.Abs(v - p.X) * scale < 4);
            var horizontal = layout.HorizontalGuides.FindIndex(v => Math.Abs(v - p.Y) * scale < 4);
            if (CanStartGuideDrag(layer, editor.IsLayerSelected(layer?.Id ?? Guid.Empty), vertical >= 0 || horizontal >= 0, screen.X < origin.X + 18 || screen.Y < origin.Y + 18))
            {
                _guideVertical = vertical >= 0 || horizontal < 0 && screen.X < origin.X + 18;
                _guideIndex = _guideVertical ? vertical : horizontal;
                if (e.ClickCount == 2 && _guideIndex >= 0) { editor.SetGuide(_guideVertical, _guideIndex, null); e.Handled = true; return; }
                _guideDragging = true; _guidePosition = _guideVertical ? p.X : p.Y; if (!CaptureMouse()) EndDrag(); _overlay.InvalidateVisual(); e.Handled = true; return;
            }
        }
        _additive = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        if (_handle >= 0) _additive = false;
        if (_handle < 0 && ShouldStartMarquee(layer, layer is not null && editor.IsLayerSelected(layer.Id)))
        {
            _marquee = true; _dragStart = _marqueeEnd = p; _startLayer = layer; if (!CaptureMouse()) EndDrag();
        }
        else
        {
            if (_additive || layer is null || !editor.SelectedLayers.Any(l => l.Id == layer.Id)) editor.SelectCanvasLayer(layer, _additive);
            else editor.FocusSelectedLayer(layer);
            if (!_additive && layer is { Locked: false }) { editor.BeginGesture(); _dragging = true; _dragStart = _lastDesign = p; _startGroup = editor.CanScaleSelection ? editor.SelectedLayers.Select(l => l.Clone()).ToArray() : []; var b = editor.SelectionBounds; _startLayer = _startGroup.Length > 0 ? new ThemeLayer { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height } : layer.Clone(); if (!CaptureMouse()) EndDrag(); }
        }
        _overlay.InvalidateVisual();
        e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (_guideDragging) { var point = Design(e.GetPosition(this)); _guidePosition = _guideVertical ? point.X : point.Y; _overlay.InvalidateVisual(); e.Handled = true; return; }
        if (_marquee) { _marqueeEnd = Design(e.GetPosition(this)); _overlay.InvalidateVisual(); e.Handled = true; return; }
        if (!_dragging)
        {
            var handle = Editor is not null && ActualWidth > 0 && ActualHeight > 0 ? ResizeHandleAt(Design(e.GetPosition(this))) : -1;
            var rotation = Editor?.IsSingleSelection == true ? Editor.SelectedLayer?.Rotation ?? 0 : 0;
            var direction = ((int)Math.Round(rotation / 45) + (handle is 0 or 3 ? 0 : 2)) % 4; if (direction < 0) direction += 4;
            Cursor = handle < 0 ? Cursors.Arrow : direction switch { 0 => Cursors.SizeNWSE, 1 => Cursors.SizeNS, 2 => Cursors.SizeNESW, _ => Cursors.SizeWE };
            return;
        }
        if (Editor?.SelectedLayer is not { Locked: false } layer || _startLayer is null) return;
        var p = Design(e.GetPosition(this));
        if (_handle < 0)
        {
            if (Editor.IsMultiSelection)
            {
                var desiredX = _startLayer.X + p.X - _dragStart.X; var desiredY = _startLayer.Y + p.Y - _dragStart.Y;
                var layout = Editor.Project.ActiveLayout.Clone(); layout.Layers.RemoveAll(l => Editor.IsLayerSelected(l.Id));
                var snapped = Editor.SnapEnabled && (Keyboard.Modifiers & ModifierKeys.Alt) == 0 ? ThemeGeometry.Snap(_startLayer, desiredX, desiredY, layout, 6 / Mapping().Scale) : new ThemeBounds(desiredX, desiredY, _startLayer.Width, _startLayer.Height);
                var current = Editor.SelectionBounds; Editor.MoveSelection(snapped.X - current.X, snapped.Y - current.Y); Editor.ReportSnap(Math.Abs(snapped.X - desiredX) > .001 || Math.Abs(snapped.Y - desiredY) > .001);
            }
            else
            {
                var x = _startLayer.X + p.X - _dragStart.X; var y = _startLayer.Y + p.Y - _dragStart.Y;
                var bounds = Editor.SnapEnabled && (Keyboard.Modifiers & ModifierKeys.Alt) == 0 ? ThemeGeometry.Snap(layer, x, y, Editor.Project.ActiveLayout, 6 / Mapping().Scale) : new ThemeBounds(x, y, layer.Width, layer.Height);
                Editor.ReportSnap(Math.Abs(bounds.X - x) > .001 || Math.Abs(bounds.Y - y) > .001);
                Editor.SetGeometry(bounds);
            }
        }
        else
        {
            var preserveAspect = (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
            var bounds = ThemeGeometry.ResizeCorner(_startLayer, _handle, p.X, p.Y, preserveAspect);
            if (!preserveAspect && Editor.SnapEnabled && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
            {
                var snapped = ThemeGeometry.SnapSize(_startLayer, bounds, _handle, Editor.Project.ActiveLayout, 6 / Mapping().Scale);
                Editor.ReportSnap(snapped != bounds); bounds = snapped;
            }
            if (_startGroup.Length > 0) Editor.ScaleSelection(_startGroup, bounds, preserveAspect); else Editor.ResizeLayerFromCorner(_startLayer, bounds, preserveAspect);
        }
        _lastDesign = p;
        _overlay.InvalidateVisual(); e.Handled = true;
    }
    private void Up(object sender, MouseButtonEventArgs e)
    {
        if (_guideDragging && Editor is { } guideEditor)
        {
            var maximum = _guideVertical ? guideEditor.Project.ActiveLayout.Width : guideEditor.Project.ActiveLayout.Height;
            guideEditor.SetGuide(_guideVertical, _guideIndex, _guidePosition >= 0 && _guidePosition <= maximum ? _guidePosition : null);
        }
        if (_marquee && Editor is { } editor)
        {
            var region = new Rect(_dragStart, _marqueeEnd);
            if ((region.Width + region.Height) * Mapping().Scale < 5) editor.SelectCanvasLayer(_startLayer, _additive);
            else editor.SelectRegion(region.X, region.Y, region.Width, region.Height, _additive);
        }
        EndDrag(); if (IsMouseCaptured) ReleaseMouseCapture(); _overlay.InvalidateVisual();
    }
    private void EndDrag() { if (_dragging) { Editor?.EndGesture(); Editor?.ReportSnap(false); } _dragging = _marquee = _guideDragging = false; _handle = -1; _startLayer = null; _startGroup = []; }
    private sealed class Overlay(ThemeCanvas owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            if (owner.Editor is not { } editor || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
            var (scale, origin) = owner.Mapping(); var layout = editor.Project.ActiveLayout;
            if (layout.ShowGrid)
            {
                var increment = layout.GridSpacing * Math.Max(1, Math.Ceiling(8 / (layout.GridSpacing * scale)));
                var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 140, 154, 177)), 1);
                for (double x = 0; x <= layout.Width; x += increment) dc.DrawLine(gridPen, new(origin.X + x * scale, origin.Y), new(origin.X + x * scale, origin.Y + layout.Height * scale));
                for (double y = 0; y <= layout.Height; y += increment) dc.DrawLine(gridPen, new(origin.X, origin.Y + y * scale), new(origin.X + layout.Width * scale, origin.Y + y * scale));
            }
            if (owner.ShowGuides)
            {
                var rulerBrush = new SolidColorBrush(Color.FromArgb(215, 244, 247, 251));
                dc.DrawRectangle(rulerBrush, null, new Rect(origin.X, origin.Y, layout.Width * scale, 18));
                dc.DrawRectangle(rulerBrush, null, new Rect(origin.X, origin.Y, 18, layout.Height * scale));
                var step = Math.Pow(10, Math.Ceiling(Math.Log10(70 / scale)));
                void Label(double value, Point point) => dc.DrawText(new FormattedText(value.ToString("0", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, Brushes.SlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
                for (double x = 0; x <= layout.Width; x += step) Label(x, new Point(origin.X + x * scale + 2, origin.Y + 2));
                for (double y = step; y <= layout.Height; y += step) Label(y, new Point(origin.X + 2, origin.Y + y * scale));
                var custom = new Pen(Brushes.CornflowerBlue, 1);
                foreach (var x in layout.VerticalGuides) dc.DrawLine(custom, new(origin.X + x * scale, origin.Y), new(origin.X + x * scale, origin.Y + layout.Height * scale));
                foreach (var y in layout.HorizontalGuides) dc.DrawLine(custom, new(origin.X, origin.Y + y * scale), new(origin.X + layout.Width * scale, origin.Y + y * scale));
                if (owner._guideDragging)
                {
                    var v = owner._guidePosition;
                    if (owner._guideVertical) dc.DrawLine(custom, new(origin.X + v * scale, origin.Y), new(origin.X + v * scale, origin.Y + layout.Height * scale));
                    else dc.DrawLine(custom, new(origin.X, origin.Y + v * scale), new(origin.X + layout.Width * scale, origin.Y + v * scale));
                }
                var guide = new Pen(new SolidColorBrush(Color.FromArgb(90, 140, 154, 177)), 1) { DashStyle = DashStyles.Dash };
                dc.DrawLine(guide, new Point(origin.X + layout.Width * scale / 2, origin.Y), new Point(origin.X + layout.Width * scale / 2, origin.Y + layout.Height * scale));
                dc.DrawLine(guide, new Point(origin.X, origin.Y + layout.Height * scale / 2), new Point(origin.X + layout.Width * scale, origin.Y + layout.Height * scale / 2));
            }
            if (owner._marquee)
            {
                var a = new Point(origin.X + owner._dragStart.X * scale, origin.Y + owner._dragStart.Y * scale);
                var b = new Point(origin.X + owner._marqueeEnd.X * scale, origin.Y + owner._marqueeEnd.Y * scale);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 76, 133, 246)), new Pen(Brushes.CornflowerBlue, 1), new Rect(a, b));
            }
            foreach (var l in editor.SelectedLayers.Where(l => l.Visible))
            {
            var rect = new Rect(origin.X + l.X * scale, origin.Y + l.Y * scale, l.Width * scale, l.Height * scale);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(76, 133, 246)), 1.5);
            dc.PushTransform(new RotateTransform(l.Rotation, rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
            dc.DrawRectangle(null, pen, rect);
            if (!l.Locked && editor.IsSingleSelection) foreach (var point in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight }) dc.DrawRectangle(Brushes.White, pen, new Rect(point.X - 4, point.Y - 4, 8, 8));
            dc.Pop();
            }
            if (editor.CanScaleSelection)
            {
                var b = editor.SelectionBounds; var r = new Rect(origin.X + b.X * scale, origin.Y + b.Y * scale, b.Width * scale, b.Height * scale); var pen = new Pen(Brushes.CornflowerBlue, 1.5);
                dc.DrawRectangle(null, pen, r); foreach (var p in new[] { r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight }) dc.DrawRectangle(Brushes.White, pen, new Rect(p.X - 4, p.Y - 4, 8, 8));
            }
            if (owner._dragging && editor.HasSelection && ActualWidth > 24 && ActualHeight > 24)
            {
                var bounds = editor.SelectionBounds;
                var pen = new Pen(Brushes.CornflowerBlue, 1) { DashStyle = DashStyles.Dash };
                var others = layout.Layers.Where(l => !editor.IsLayerSelected(l.Id)).ToArray();
                foreach (var gap in ThemeMeasurements.Nearest(bounds, others))
                {
                    var a = new Point(origin.X + gap.X1 * scale, origin.Y + gap.Y1 * scale); var b = new Point(origin.X + gap.X2 * scale, origin.Y + gap.Y2 * scale);
                    dc.DrawLine(pen, a, b);
                    var label = new FormattedText($"{gap.Distance:0.#}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    var at = new Point((a.X + b.X) / 2 + 3, (a.Y + b.Y) / 2 + 3);
                    dc.DrawRoundedRectangle(Brushes.SlateGray, null, new Rect(at.X - 3, at.Y - 1, label.Width + 6, label.Height + 2), 3, 3); dc.DrawText(label, at);
                }
                foreach (var other in others.Where(l => l.Visible))
                {
                    var b = ThemeGeometry.VisualBounds(other);
                    foreach (var x in new[] { bounds.X, bounds.X + bounds.Width / 2, bounds.X + bounds.Width })
                        if (new[] { b.X, b.X + b.Width / 2, b.X + b.Width }.Any(v => Math.Abs(v - x) * scale < .75)) dc.DrawLine(pen, new(origin.X + x * scale, origin.Y), new(origin.X + x * scale, origin.Y + layout.Height * scale));
                    foreach (var y in new[] { bounds.Y, bounds.Y + bounds.Height / 2, bounds.Y + bounds.Height })
                        if (new[] { b.Y, b.Y + b.Height / 2, b.Y + b.Height }.Any(v => Math.Abs(v - y) * scale < .75)) dc.DrawLine(pen, new(origin.X, origin.Y + y * scale), new(origin.X + layout.Width * scale, origin.Y + y * scale));
                }
                var info = editor.GeometryFeedback + (owner._handle >= 0 ? $"   比例 {bounds.Width / bounds.Height:0.###}:1 · Shift 自由缩放" : "");
                var text = new FormattedText(info, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(30, ActualWidth - 24) };
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 32, 44, 67)), null, new Rect(6, 6, Math.Min(ActualWidth - 12, text.Width + 12), text.Height + 10), 5, 5); dc.DrawText(text, new(12, 11));
            }
        }
    }
}

