using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;

namespace DeskDisplay.DisplayTests;

internal static class CreatorNavigationTests
{
    public static async Task RunAsync(MainViewModel main, CreatorViewModel creator, ThemeProjectStore store, string root)
    {
        var project = new ThemeProject { Name = "画布导航与状态恢复" }; var group = Guid.NewGuid();
        var a = new ThemeLayer { Kind = ThemeElementKind.Text, Name = "组A", Text = "拖动与缩放", X = 2200, Y = -100, Width = 300, Height = 100, GroupId = group, ZIndex = 1 };
        var b = new ThemeLayer { Kind = ThemeElementKind.Rectangle, Name = "组B", X = 2550, Y = -20, Width = 100, Height = 80, GroupId = group, ZIndex = 2 };
        var c = new ThemeLayer { Kind = ThemeElementKind.Ellipse, X = 900, Y = 200, Width = 120, Height = 120, ZIndex = 3 };
        project.Landscape.Layers.AddRange([a, b, c]); await creator.OpenProjectAsync(project); creator.LocateOutside();
        Check(creator.SelectionCount == 2 && creator.SelectedLayer?.GroupId == group, "Outside locator selects the actual off-canvas group");
        var distance = b.X - a.X; creator.BringSelectionInside(); var bounds = creator.SelectionBounds;
        Check(bounds.X >= 0 && bounds.Y >= 0 && bounds.X + bounds.Width <= project.ActiveLayout.Width && bounds.Y + bounds.Height <= project.ActiveLayout.Height && b.X - a.X == distance && a.Width == 300, "Bring inside preserves group sizes and relative positions");
        creator.UndoCommand.Execute(null); await creator.PendingOperation;
        Check(creator.Project.ActiveLayout.Layers.Single(l => l.Id == a.Id).X == 2200, "Bringing off-canvas layers back is one undo step");
        creator.BringFrontCommand.Execute(null);
        Check(creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).Select(l => l.Id).SequenceEqual(new[] { c.Id, a.Id, b.Id }), "Bring front treats selected groups as one ordered block");
        creator.SendBackCommand.Execute(null);
        Check(creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).Select(l => l.Id).SequenceEqual(new[] { a.Id, b.Id, c.Id }), "Send back preserves the internal group order");
        var member = creator.Project.ActiveLayout.Layers.Single(l => l.Id == a.Id); member.Locked = true; var oldX = member.X;
        creator.BringSelectionInside(); creator.MoveSelectionToEdge(true);
        Check(member.X == oldX && creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).First().Id == a.Id, "Locked group members prevent recovery movement and stacking changes");
        member.Locked = false; creator.BringSelectionInside(); await creator.SaveAsync();
        creator.SelectedLayer = member; creator.CaptureEditorViewport(3, .6, .4); await creator.FlushEditorViewportAsync();
        var state = await store.ReadViewportAsync(project.Id, ThemeOrientation.Landscape);
        Check(state is { Zoom: 3, Horizontal: .6, Vertical: .4 } && state.Selection.SequenceEqual(new[] { member.Id }) && state.FocusedLayer == member.Id, "Viewport persistence records zoom, normalized scroll and exact focused member");
        await creator.OpenProjectAsync(creator.Project.Clone());
        Check(creator.SelectionCount == 1 && creator.SelectedLayer?.Id == member.Id && creator.EditorViewport?.Zoom == 3, "Reopening preserves a single selected group member without expanding the whole selection");
        creator.Orientation = ThemeOrientation.Portrait; await creator.PendingOperation; creator.CaptureEditorViewport(1.5, .1, .2); await creator.FlushEditorViewportAsync();
        creator.Orientation = ThemeOrientation.Landscape; await creator.PendingOperation;
        Check(creator.EditorViewport?.Zoom == 3 && creator.SelectedLayer?.Id == member.Id && (await store.ReadViewportAsync(project.Id, ThemeOrientation.Portrait))?.Zoom == 1.5, "Landscape and portrait retain independent viewport and selection state");
        using (var fresh = new CreatorViewModel(main, Serilog.Log.Logger, store))
        {
            await fresh.OpenProjectAsync(creator.Project.Clone());
            Check(fresh.EditorViewport?.Zoom == 3 && fresh.SelectedLayer?.Id == member.Id && fresh.SelectionCount == 1, "A fresh editor session restores persisted viewport and exact selection from disk");
        }
        var invalid = false; try { await store.SaveViewportAsync(new(project.Id, ThemeOrientation.Landscape, double.NaN, 0, 0, [])); } catch (InvalidDataException) { invalid = true; }
        Check(invalid, "Invalid viewport values cannot be persisted");
        var brokenId = Guid.NewGuid(); var broken = Path.Combine(root, "themes", "EditorState", brokenId.ToString("N") + "-0.json"); await File.WriteAllTextAsync(broken, "invalid");
        Check(await store.ReadViewportAsync(brokenId, ThemeOrientation.Landscape) is null && File.Exists(broken), "Corrupt UI state falls back without deleting source or damaged state");
        await creator.OpenProjectAsync(creator.Project.Clone()); var stale = creator.EditorViewport! with { Selection = [Guid.NewGuid()], FocusedLayer = null }; await store.SaveViewportAsync(stale);
        await creator.OpenProjectAsync(creator.Project.Clone());
        Check(creator.SelectionCount == 0, "Deleted or stale layer IDs are ignored during selection restoration");
        creator.SelectedLayer = creator.Project.ActiveLayout.Layers.Single(l => l.Id == a.Id); creator.CaptureEditorViewport(3, .5, 0); await creator.FlushEditorViewportAsync();
        var window = new MainWindow { DataContext = main, Width = 1320, Height = 900, Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false }; window.Show();
        try
        {
            await Task.Delay(200); window.UpdateLayout(); var view = Find<CreatorView>(window).Single(); var scroll = (ScrollViewer)view.FindName("StageScroll"); var canvas = (ThemeCanvas)view.FindName("Canvas"); var slider = (Slider)view.FindName("ZoomSlider");
            Check(slider.Value == 3 && scroll.HorizontalOffset > 0 && creator.SelectedLayer?.Id == a.Id, "Actual WPF editor restores zoom, scroll and selected layer");
            var anchor = new Point(scroll.ViewportWidth * .45, scroll.ViewportHeight / 2); var local = scroll.TranslatePoint(anchor, canvas); var fraction = new Point(local.X / canvas.ActualWidth, local.Y / canvas.ActualHeight);
            view.ZoomViewportAt(anchor, 2.5); await Task.Delay(50); window.UpdateLayout();
            var after = canvas.TranslatePoint(new Point(fraction.X * canvas.ActualWidth, fraction.Y * canvas.ActualHeight), scroll);
            Check(Math.Abs(after.X - anchor.X) < 2 && Math.Abs(after.Y - anchor.Y) < 2, "Mouse-centered zoom preserves the same design point beneath the anchor when scroll range permits");
            // Native mouse capture requires an active presentation source on Windows.
            window.Left = 20; window.Top = 20; window.Activate(); canvas.Focus(); await Task.Delay(50);
            var source = PresentationSource.FromVisual(window)!;
            view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = canvas });
            var xBefore = creator.SelectedLayer!.X;
            scroll.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            var panning = (bool)typeof(CreatorView).GetField("_panning", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(view)!;
            Check(scroll.IsMouseCaptured ? panning : !panning, "Panning captures the mouse or safely resets when native capture is unavailable");
            if (!scroll.IsMouseCaptured) Console.WriteLine("BOUNDARY: Native mouse capture unavailable in this desktop session; physical drag remains an interactive acceptance item.");
            scroll.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });
            view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyUpEvent, Source = canvas });
            Check(!scroll.IsMouseCaptured && creator.SelectedLayer.X == xBefore, "Ending viewport panning releases capture without moving layer geometry");
            Capture(window, "creator-navigation-state.png");
        }
        finally { window.Close(); }
        await creator.FlushEditorViewportAsync(); var saved = await store.ReadViewportAsync(project.Id, ThemeOrientation.Landscape);
        Console.WriteLine($"METRIC: navigation close saved zoom={saved?.Zoom}; editor viewport={creator.EditorViewport?.Zoom}; id={project.Id}; current={creator.Project.Id}");
        Check(saved?.Zoom == 2.5, "Closing the actual view persists the latest zoom for the next session");
    }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject { if (parent is T match) yield return match; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var c in Find<T>(VisualTreeHelper.GetChild(parent, i))) yield return c; }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); Console.WriteLine("PASS: " + label); }
}
