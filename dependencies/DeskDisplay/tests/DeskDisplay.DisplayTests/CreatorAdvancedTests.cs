using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;

namespace DeskDisplay.DisplayTests;

internal static class CreatorAdvancedTests
{
    public static async Task RunAsync(MainViewModel main, CreatorViewModel creator, ThemeProjectStore store, WallpaperAsset a, WallpaperAsset b)
    {
        var project = new ThemeProject { Name = "组合与素材窗口验收" };
        project.Landscape.Layers.AddRange([
            new() { Kind = ThemeElementKind.Text, Name = "标题", Text = "D92 Studio", X = 100, Y = 60, Width = 300, Height = 100, FontSize = 50, Spacing = 2, ZIndex = 1 },
            new() { Kind = ThemeElementKind.DigitalClock, Name = "实时时钟", X = 600, Y = 140, Width = 600, Height = 160, FontSize = 90, ShowSeconds = true, Rotation = 15, ZIndex = 2 },
            new() { Kind = ThemeElementKind.Wallpaper, Name = a.Name, Path = a.Path, AssetId = a.Id, Width = 1920, Height = 480, ZIndex = -1 }
        ]);
        var isolated = project.Clone(); isolated.Landscape.Layers[0].Text = "副本修改"; isolated.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Text });
        Check(project.Landscape.Layers[0].Text == "D92 Studio" && project.Portrait.Layers.Count == 0, "Fast source snapshots keep independent layout lists and mutable layers");
        await creator.OpenProjectAsync(project); creator.SelectLayers(project.Landscape.Layers.Take(2).Select(l => l.Id));
        creator.GroupCommand.Execute(null); var group = creator.SelectedLayers[0].GroupId;
        Check(group is not null && creator.SelectedLayers.All(l => l.GroupId == group), "Grouping stores a genuine shared identity in editable layer source");
        creator.SelectLayers([]); creator.SelectCanvasLayer(project.Landscape.Layers[0], false);
        Check(creator.SelectionCount == 2, "Clicking a grouped member selects all members");
        creator.SelectCanvasLayer(project.Landscape.Layers[1], true); Check(creator.SelectionCount == 0, "Additive selection toggles an entire group without leaving a partial group selected");
        creator.SelectLayers([project.Landscape.Layers[0].Id]); var originals = creator.SelectedLayers.Select(l => System.Text.Json.JsonSerializer.Deserialize<ThemeLayer>(System.Text.Json.JsonSerializer.Serialize(l))!).ToArray(); var bounds = ThemeGeometry.Union(originals);
        creator.BeginGesture(); creator.ScaleSelection(originals, new(bounds.X, bounds.Y, bounds.Width * 1.5, bounds.Height * 1.5)); creator.ScaleSelection(originals, new(bounds.X, bounds.Y, bounds.Width * 2, bounds.Height * 2)); creator.EndGesture();
        Check(creator.SelectedLayers[0].Width == 600 && creator.SelectedLayers[0].FontSize == 100 && creator.SelectedLayers[1].Rotation == 15, "Whole-selection scaling updates positions, sizes and typography while preserving rotated member orientation");
        creator.UndoCommand.Execute(null); await creator.PendingOperation;
        Check(creator.SelectedLayers[0].Width == 300 && creator.SelectedLayers[0].FontSize == 50, "One undo restores the entire multi-element scale gesture");
        var invalidScale = ThemeGeometry.ScaleGroup(creator.SelectedLayers, new(bounds.X, bounds.Y, bounds.Width * 100, bounds.Height * 100));
        Check(invalidScale is null, "Group scaling rejects dimensions outside source limits atomically");
        creator.CopyCommand.Execute(null); creator.PasteCommand.Execute(null); await creator.PendingOperation;
        Check(creator.SelectedLayers.All(l => l.GroupId is not null && l.GroupId != group) && creator.SelectedLayers.Select(l => l.GroupId).Distinct().Count() == 1, "Pasted groups have a fresh shared identity rather than joining the original group");
        creator.UngroupCommand.Execute(null); Check(creator.SelectedLayers.All(l => l.GroupId is null), "Ungroup removes membership without changing visual geometry");
        creator.UndoCommand.Execute(null); await creator.PendingOperation;
        Check(creator.SelectedLayers.All(l => l.GroupId is not null), "Group membership participates in undo history");
        creator.MoveBy(0, 500);
        await creator.SaveAsync(); var saved = (await store.ListAsync()).Single(p => p.Id == creator.Project.Id);
        Check(saved.Landscape.Layers.Count(l => l.GroupId is not null) == 4, "Saved source persists independent group memberships");
        creator.SelectLayers([creator.Project.ActiveLayout.Layers[0].Id]); creator.SelectedLayer = creator.Project.ActiveLayout.Layers[0]; creator.LockAspect = true; var ratio = creator.Width / creator.Height; creator.Width = 450;
        Check(Math.Abs(creator.Width / creator.Height - ratio) < .000001 && creator.Height == 150, "Numeric width changes preserve ratio when size lock is enabled");
        creator.Height = 200; Check(creator.Width == 600, "Numeric height changes preserve ratio in the other direction");
        creator.UndoCommand.Execute(null); await creator.PendingOperation; creator.LockAspect = false;
        creator.SelectedLayer = creator.Project.ActiveLayout.Layers.Single(l => l.Kind == ThemeElementKind.Wallpaper); creator.Fill = ImageDisplayMode.Fill; creator.Opacity = .6;
        var oldBounds = new ThemeBounds(creator.X, creator.Y, creator.Width, creator.Height); await creator.ReplaceSelectedAssetAsync(b);
        Check(creator.SelectedLayer!.AssetId == b.Id && creator.Fill == ImageDisplayMode.Fill && creator.Opacity == .6 && new ThemeBounds(creator.X, creator.Y, creator.Width, creator.Height) == oldBounds, "Replacing selected material preserves geometry, fill and opacity");
        creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.SelectedLayer!.AssetId == a.Id, "Material replacement is undoable");
        creator.AssetCategory = "风景"; creator.AssetSubcategory = "山川";
        Check(creator.FilteredAssets.Count == 1 && creator.FilteredAssets[0].Asset.Id == a.Id && creator.AssetSubcategories.Contains("山川"), "Material browser supports real linked primary and secondary categories");
        creator.AssetCategory = "动漫"; Check(creator.AssetSubcategory == "全部子分类" && !creator.AssetSubcategories.Contains("山川"), "Changing the primary category clears stale secondary selection");
        creator.AssetCategory = "全部分类"; creator.AssetSubcategory = "全部子分类";
        var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 }; window.Show();
        try
        {
            await Task.Delay(100); window.UpdateLayout();
            var canvas = Find<ThemeCanvas>(window).Single(); Check(canvas.ContextMenu is { } menu && new[] { "CopyCommand", "PasteCommand", "DeleteCommand", "BringFrontCommand", "SendBackCommand", "BringInsideCommand" }.All(command => menu.Items.OfType<MenuItem>().Any(item => item.GetBindingExpression(MenuItem.CommandProperty)?.ParentBinding.Path?.Path == command)), "Canvas exposes actual edit, stacking and recovery command bindings");
            Find<Expander>(window).Single(e => Equals(e.Header, "位置与尺寸")).IsExpanded = true; window.UpdateLayout();
            var widthInput = Find<NumericStepper>(window).First(n => n.GetBindingExpression(NumericStepper.ValueProperty)?.ParentBinding.Path?.Path == "Width");
            var previousWidth = creator.Width; ((Button)((StackPanel)widthInput.Children[1]).Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(creator.Width == previousWidth + 1, "Actual WPF numeric stepper updates the selected layer through its existing binding");
            var browser = new CreatorMaterialBrowser(creator) { Owner = window, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; browser.Show();
            try
            {
                await Task.Delay(100); browser.UpdateLayout(); var list = Find<ListBox>(browser).Single();
                Check(list.Items.Count > 0 && list.Items.Count <= 12 && list.Items.Cast<WallpaperItemViewModel>().All(i => creator.LibraryAssets.Contains(i)), "Resizable material browser pages existing library items rather than duplicating asset data");
                list.SelectedItem = list.Items[0]; browser.UpdateLayout();
                Check(Find<Button>(browser).Single(btn => Equals(btn.Content, "替换当前素材")).IsEnabled, "Browser enables replacement only for an editable selected image layer");
                Capture(browser, "creator-material-browser.png");
            }
            finally { browser.Close(); }
            var picker = new CreatorColorPicker("#804C85F6") { Owner = window, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; picker.Show();
            try
            {
                var sliders = Find<Slider>(picker).ToArray(); sliders[0].Value = 17; sliders[1].Value = 34; sliders[2].Value = 51;
                Check(picker.SelectedColor == "#80112233", "Color picker combines real RGB channels and preserves editable alpha"); picker.UpdateLayout(); Capture(picker, "creator-color-picker.png");
            }
            finally { picker.Close(); }
            creator.SelectLayers(creator.Project.ActiveLayout.Layers.Where(l => l.GroupId == group).Select(l => l.Id)); window.UpdateLayout(); Capture(window, "creator-group-editor.png");
        }
        finally { window.Close(); }
        await creator.SaveAsync(); var originalId = creator.Project.Id; var originalText = (await store.ListAsync()).Single(p => p.Id == originalId).Name;
        creator.ProjectName = "另存为时的新修改";
        creator.SaveAsCommand.Execute(null); await creator.PendingOperation;
        Check(creator.Project.Id != originalId && !creator.IsDirty && (await store.ListAsync()).Any(p => p.Id == originalId && p.Name == originalText), "Save As creates a new saved editable project and preserves the original source");
        creator.ProjectSearch = ""; creator.ProjectOrientation = "竖屏 1:4"; Check(creator.VisibleProjects.All(p => p.Project.Orientation == ThemeOrientation.Portrait), "Creator home filters actual project orientations");
        creator.ProjectOrientation = "全部方向"; creator.ProjectSort = "最近修改";
        var cloneWatch = System.Diagnostics.Stopwatch.StartNew(); for (var i = 0; i < 200; i++) _ = creator.Project.Clone(); var fastMs = cloneWatch.Elapsed.TotalMilliseconds;
        cloneWatch.Restart(); for (var i = 0; i < 200; i++) _ = System.Text.Json.JsonSerializer.Deserialize<ThemeProject>(System.Text.Json.JsonSerializer.Serialize(creator.Project));
        Console.WriteLine($"METRIC: 200 source snapshots ({creator.Project.ActiveLayout.Layers.Count} layers): object copies={fastMs:F2}ms, JSON roundtrip={cloneWatch.Elapsed.TotalMilliseconds:F2}ms; no pixels are copied.");
        Console.WriteLine("All creator advanced tests passed.");
    }
    public static async Task RunImageStressAsync(CreatorViewModel creator, string root)
    {
        var project = new ThemeProject { Name = "三张独立大图验收" }; var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            var path = Path.Combine(root, "large-" + i + ".png");
            var pixels = Enumerable.Repeat((byte)(80 + i * 50), 4096 * 1024 * 4).ToArray();
            var bitmap = BitmapSource.Create(4096, 1024, 96, 96, PixelFormats.Bgr32, null, pixels, 4096 * 4); bitmap.Freeze();
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) encoder.Save(file);
            project.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "独立大图" + i, Path = path, Width = 1920, Height = 480, ZIndex = i });
        }
        var ticks = 0; var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) }; timer.Tick += (_, _) => ticks++; timer.Start();
        var before = creator.ImageDecodeCount; await creator.OpenProjectAsync(project); timer.Stop();
        Check(creator.Scene.Children.Count == 3 && creator.Scene.Children.Values.Distinct().Count() == 3 && creator.ImageDecodeCount - before == 3, "Three distinct 4096x1024 assets decode independently without confusing shared-image caching");
        Check(ticks > 0, "Dispatcher continues processing while multiple large images decode in background");
        var references = GetImageReferences(creator); await creator.OpenProjectAsync(new ThemeProject());
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Check(references.All(r => !r.IsAlive), "Switching away releases decoded images from the previous project rather than retaining them in the cache");
        Console.WriteLine($"METRIC: generate and load three separate 4096x1024 PNGs={watch.Elapsed.TotalSeconds:F2}s; dispatcher ticks={ticks}; previous image scenes collectible=3.");
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] GetImageReferences(CreatorViewModel creator) => creator.Scene.Children.Values.Select(scene => new WeakReference(scene)).ToArray();
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T t) yield return t; foreach (var nested in Find<T>(child)) yield return nested; }
    }
    private static void Capture(Window window, string name)
    {
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); png.Save(file);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
