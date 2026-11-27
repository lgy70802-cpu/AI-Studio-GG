using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorProductivityTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var background = new ThemeLayer { Kind = ThemeElementKind.Wallpaper };
        Check(!ThemeCanvas.ShouldStartMarquee(background, false) && !ThemeCanvas.ShouldStartMarquee(background, true), "Unlocked wallpaper backgrounds support direct dragging whether previously selected or not");
        Check(!ThemeCanvas.CanStartGuideDrag(new ThemeLayer { Kind = ThemeElementKind.Image }, true, true, false) && !ThemeCanvas.CanStartGuideDrag(background, true, true, false), "Guides crossing selected images and foreground elements do not intercept element dragging");
        background.Locked = true; Check(ThemeCanvas.ShouldStartMarquee(background, true) && ThemeCanvas.CanStartGuideDrag(null, false, true, false), "Locked backgrounds retain marquee behavior and guides remain draggable in empty space");
        var resizeLayer = new ThemeLayer { Kind = ThemeElementKind.Wallpaper, Width = 1920, Height = 480 };
        foreach (var scale in new[] { .25, 1d, 1.5 })
        {
            var corners = new[] { new Point(0, 0), new Point(1920, 0), new Point(0, 480), new Point(1920, 480) };
            Check(corners.Select((p, i) => ThemeCanvas.HitResizeHandle(p, resizeLayer, scale) == i).All(v => v), "All four wallpaper resize corners remain hittable at scale " + scale);
        }
        Check(!ThemeCanvas.CanStartGuideDrag(resizeLayer, true, true, true, true), "Resize handles take priority over overlapping rulers and guides");
        resizeLayer.Rotation = 90;
        Check(ThemeCanvas.HitResizeHandle(new Point(1200, -720), resizeLayer, .5) == 0, "Rotated resize handles use the same design-coordinate rotation as their visible corners");
        resizeLayer.Locked = true;
        Check(ThemeCanvas.HitResizeHandle(new Point(1200, -720), resizeLayer, .5) == -1, "Locked layers do not expose interactive resize handles");
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-six-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); var project = new ThemeProject { Name = "历史初版" };
            project.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Text, Text = "可编辑", Name = "标题" });
            await store.SaveAsync(project);
            Check((await store.HistoryAsync(project.Id)).Count == 0, "First formal save has no invented historical versions");
            for (var i = 0; i < 13; i++) { project.Name = "历史版本" + i; project.ModifiedAt = DateTimeOffset.Now; await store.SaveAsync(project); }
            var history = await store.HistoryAsync(project.Id);
            Check(history.Count == ThemeProjectStore.HistoryLimit && history[0].Name == "历史版本11" && (await store.ListAsync()).Single().Name == "历史版本12", "Formal history preserves ten previous sources without changing current source listing");
            var historyDirectory = Path.Combine(root, "themes", "History", project.Id.ToString("N"));
            var damaged = Path.Combine(historyDirectory, "broken.theme.json"); await File.WriteAllTextAsync(damaged, "invalid");
            Check((await store.HistoryAsync(project.Id)).Count == 10 && File.Exists(damaged), "Damaged history is preserved and skipped without blocking valid versions");
            await store.SaveAsync(project);
            Check((await store.HistoryAsync(project.Id)).Count == 10 && File.Exists(damaged), "History pruning retains ten managed versions and preserves unrelated damaged records");
            var sourcePath = Path.Combine(root, "themes", project.Id.ToString("N") + ".theme.json");
            var sourceBeforeFailure = await File.ReadAllTextAsync(sourcePath);
            using (var held = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var blocked = false; try { await store.SaveAsync(project); } catch (IOException) { blocked = true; }
                Check(blocked, "An unavailable previous source blocks replacement rather than silently discarding history");
            }
            Check(await File.ReadAllTextAsync(sourcePath) == sourceBeforeFailure, "Failed history backup preserves the current formal source bytes");
            var template = await store.SaveTemplateAsync(project); template.Name = "not persisted";
            Check((await store.ListTemplatesAsync()).Single().Id != project.Id && (await store.ListTemplatesAsync()).Single().Name == project.Name && (await store.ListAsync()).Count == 1, "Custom templates persist separate editable source snapshots without duplicating asset catalog");
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); var initial = new AppSettings();
            using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog")); var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            using var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); main.SelectedPage = AppPage.Creator; await creator.InitializeAsync();
            await creator.OpenSourceCopyAsync(history[0]);
            Check(creator.Project.Id != project.Id && creator.IsDirty && creator.Project.ActiveLayout.Layers[0].Text == "可编辑" && (await store.ListAsync()).Single().Name == "历史版本12", "Restoring a historical source creates an editable recovery copy and retains the current formal version");
            var custom = (await creator.CustomTemplatesAsync()).Single(); await creator.OpenSourceCopyAsync(custom);
            Check(creator.Project.Id != custom.Id && creator.Project.Id != project.Id && creator.IsDirty, "Using a custom template allocates an independent editable project");
            foreach (var kind in Enum.GetValues<ThemeElementKind>())
            {
                var transform = new ThemeProject(); var element = new ThemeLayer { Kind = kind, Width = 200, Height = 100, X = 100, Y = 100, FontSize = 30, Spacing = 2 };
                transform.ActiveLayout.Layers.Add(element); await creator.OpenProjectAsync(transform); creator.SelectedLayer = element;
                creator.BeginGesture(); creator.MoveBy(10, 20); creator.EndGesture();
                Check(element.X == 110 && element.Y == 120, "Every layer type supports movement: " + kind);
                var start = element.Clone(); var uniform = ThemeGeometry.ResizeCorner(start, 3, 510, 250, true);
                creator.BeginGesture(); creator.ResizeLayerFromCorner(start, uniform); creator.EndGesture();
                Check(Math.Abs(element.Width / element.Height - 2) < .0001 && element.Width > 200, "Every layer type supports default proportional corner resizing: " + kind);
                if (kind is ThemeElementKind.Text or ThemeElementKind.DigitalClock or ThemeElementKind.Date or ThemeElementKind.Weekday)
                    Check(element.FontSize > 30, "Corner resizing scales real text content: " + kind);
                var freeStart = element.Clone(); var free = ThemeGeometry.ResizeCorner(freeStart, 3, freeStart.X + 550, freeStart.Y + 170, false);
                creator.BeginGesture(); creator.ResizeLayerFromCorner(freeStart, free, false); creator.EndGesture();
                Check(Math.Abs(element.Width / element.Height - 2) > .01 && (kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper) || element.Fill == ImageDisplayMode.Stretch), "Every layer type supports Shift-style free resizing: " + kind);
                creator.UndoCommand.Execute(null); await creator.PendingOperation;
                Check(Math.Abs(creator.SelectedLayer!.Width / creator.SelectedLayer.Height - 2) < .0001, "Free resizing is one undo for layer type: " + kind);
            }
            var groupLayers = new[] { new ThemeLayer { X = 0, Y = 0, Width = 100, Height = 100 }, new ThemeLayer { X = 200, Y = 100, Width = 100, Height = 100 } };
            var freeGroup = ThemeGeometry.ScaleGroup(groupLayers, new ThemeBounds(0, 0, 600, 200), false)!;
            Check(freeGroup[0].Width == 200 && freeGroup[0].Height == 100 && freeGroup[1].X == 400, "Shift-style group resizing scales width and height independently");
            var editing = new ThemeProject { Name = "六项功能验收" }; var group = Guid.NewGuid();
            var a = new ThemeLayer { Name = "组内标题", Kind = ThemeElementKind.Text, Text = "D92", GroupId = group, ZIndex = 1, X = 100, Y = 100, Width = 240, Height = 100 };
            var b = new ThemeLayer { Name = "组内时钟", Kind = ThemeElementKind.DigitalClock, GroupId = group, ZIndex = 2, X = 500, Y = 100, Width = 700, Height = 150 };
            var c = new ThemeLayer { Name = "独立元素", Kind = ThemeElementKind.Rectangle, ZIndex = 3, X = 1400, Y = 100 };
            editing.Landscape.Layers.AddRange([a, b, c]); await creator.OpenProjectAsync(editing);
            creator.Reorder(a.Id, c.Id);
            Check(creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).Select(l => l.Id).SequenceEqual(new[] { c.Id, a.Id, b.Id }), "Reordering a group member moves the entire group as a stable contiguous block");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).Select(l => l.Id).SequenceEqual(new[] { a.Id, b.Id, c.Id }), "Group reorder is one undo operation");
            creator.SelectedLayer = creator.Project.ActiveLayout.Layers.Single(l => l.Id == a.Id); creator.LayerUpCommand.Execute(null);
            Check(creator.Project.ActiveLayout.Layers.OrderBy(l => l.ZIndex).Select(l => l.Id).SequenceEqual(new[] { c.Id, a.Id, b.Id }), "Layer up moves past internal group members as a whole block");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            creator.Project.ActiveLayout.Layers.Single(l => l.Id == b.Id).Locked = true; var order = creator.Layers.Select(l => l.Id).ToArray(); creator.Reorder(a.Id, c.Id);
            Check(creator.Layers.Select(l => l.Id).SequenceEqual(order), "A locked group member prevents whole-group reordering");
            creator.ToggleGroupExpanded(group); creator.LocateRestrictedLayer();
            Check(creator.SelectedLayer?.Id == b.Id && !creator.IsGroupCollapsed(group) && creator.SelectedLayer.Locked, "Locating a restricted layer expands its group without unlocking it");
            creator.SetGuide(true, -1, 300); creator.SetGuide(false, -1, 200);
            Check(creator.Project.ActiveLayout.VerticalGuides.SequenceEqual(new[] { 300d }) && creator.Project.ActiveLayout.HorizontalGuides.SequenceEqual(new[] { 200d }), "Design-coordinate guides can be created independently on both axes");
            var clone = creator.Project.Clone(); clone.ActiveLayout.VerticalGuides[0] = 999;
            Check(creator.Project.ActiveLayout.VerticalGuides[0] == 300, "Guide collections are detached in project and undo snapshots");
            creator.SetGuide(true, 0, 350); creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.VerticalGuides[0] == 300, "Moving a guide is one undo transaction");
            creator.SetGuide(true, -1, double.NaN); Check(creator.Project.ActiveLayout.VerticalGuides.Count == 1, "Invalid guide coordinates are rejected");
            var snapLayer = new ThemeLayer { Width = 100, Height = 50 };
            var snapped = ThemeGeometry.Snap(snapLayer, 298, 199, creator.Project.ActiveLayout, 4);
            Check(snapped.X == 300 && snapped.Y == 200, "Position snapping includes saved custom guides");
            var sized = ThemeGeometry.SnapSize(new ThemeLayer { X = 100, Y = 100, Width = 100, Height = 50 }, new ThemeBounds(100, 100, 198, 99), 3, creator.Project.ActiveLayout, 4);
            Check(sized.X == 100 && sized.Y == 100 && sized.Width == 200 && sized.Height == 100, "Axis-aligned resize snaps to guides while preserving the opposite corner");
            await creator.SaveAsync(); var saved = (await store.ListAsync()).Single(p => p.Id == creator.Project.Id);
            Check(saved.ActiveLayout.VerticalGuides[0] == 300 && saved.Portrait.VerticalGuides.Count == 0, "Guide persistence preserves independent horizontal and portrait layouts");
            var imagePath = Path.Combine(root, "sample.png"); WriteImage(imagePath);
            var reportProject = creator.Project.Clone(); reportProject.Landscape.Layers.AddRange([
                new() { Name = "本地", Kind = ThemeElementKind.Image, Path = imagePath, AssetOrigin = ThemeAssetOrigin.Local },
                new() { Name = "云端", Kind = ThemeElementKind.Image, Path = imagePath, AssetOrigin = ThemeAssetOrigin.Cloud, CloudResourceId = "protected" },
                new() { Name = "缺失", Kind = ThemeElementKind.Image, Path = Path.Combine(root, "gone.png") },
                new() { Name = "插件", Kind = ThemeElementKind.CameraClock },
                new() { Name = "视频", Kind = ThemeElementKind.Wallpaper, Path = Path.Combine(root, "gone.mp4"), IsVideo = true }]);
            var rows = ThemeExportInspection.Inspect(reportProject, new Dictionary<Guid, WallpaperAsset>()); var report = ThemeExportInspection.Markdown(reportProject, rows);
            Check(rows.Any(r => r.Layer == "本地" && r.Status.Contains("可打包")) && report.Contains("云端引用") && report.Contains("文件缺失") && report.Contains("插件依赖") && report.Contains("D92 USB不支持"), "Export inspection reports local, protected, missing, plugin and video transfer limits honestly");
            var guarded = new ThemeProject(); guarded.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = imagePath, AssetOrigin = ThemeAssetOrigin.Local, CloudResourceId = "retained-cloud-id" });
            var guardedResult = await new ThemeTransferService(store).ExportAsync(guarded, Path.Combine(root, "guarded.d92theme"), true, new Dictionary<Guid, WallpaperAsset>());
            Check(guardedResult.Included == 0 && guardedResult.Omitted == 1, "Retained cloud identity prevents packaging even if an older source says Local");
            var formalBefore = (await store.ListAsync()).Count; await creator.ExportInspectionAsync(); Check((await store.ListAsync()).Count == formalBefore, "Export inspection does not publish or modify saved sources");
            var missingPath = Path.Combine(root, "old", "sample.png"); var repairProject = new ThemeProject();
            repairProject.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = missingPath }); repairProject.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = missingPath });
            await creator.OpenProjectAsync(repairProject); var plan = await creator.PreviewFolderRepairAsync(root);
            Check(await creator.ApplyFolderRepairPlanAsync(plan, plan.Items.Take(1).ToArray()) == 1 && creator.MissingAssets.Count == 1 && creator.Project.Portrait.Layers[0].Path == missingPath, "Repair applies only checked references even when both layouts share a candidate");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            var repairWindow = new CreatorRepairPreview(plan) { Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual }; repairWindow.Show();
            try { var choices = (IEnumerable<ThemeRepairChoice>)Find<DataGrid>(repairWindow).Single().ItemsSource; choices.First().Selected = false; Check(repairWindow.SelectedMatches.Count == 1, "Repair checkbox state controls the actual selected match list"); choices.First().Selected = true; var grid = Find<DataGrid>(repairWindow).Single(); grid.SelectedIndex = 0; await Task.Delay(200); repairWindow.UpdateLayout(); Check(Find<Image>(repairWindow).Any(i => i.Source is not null), "Checked repair dialog displays the actual candidate image"); Capture(repairWindow, "creator-six-repair.png"); } finally { repairWindow.Close(); }
            using (var preview = new CreatorViewModel(main, logger, store))
            {
                var browser = new CreatorSourceBrowser(history, preview, "历史版本", "恢复为副本") { Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual }; browser.Show();
                try { await Task.Delay(100); browser.UpdateLayout(); Check(browser.SelectedSource?.Name == history[0].Name && preview.Project.ActiveLayout.Layers.Count > 0, "Historical browser previews actual editable scene data"); Capture(browser, "creator-six-history.png"); } finally { browser.Close(); }
            }
            var reportWindow = new CreatorExportReport(report) { Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual }; reportWindow.Show(); try { reportWindow.UpdateLayout(); Capture(reportWindow, "creator-six-export.png"); } finally { reportWindow.Close(); }
            await creator.OpenProjectAsync(saved); var window = new MainWindow { DataContext = main, Width = 1320, Height = 900, Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false }; window.Show(); try { await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-six-guides.png"); } finally { window.Close(); }
            await CreatorNavigationTests.RunAsync(main, creator, store, root);
        }
        finally { Directory.Delete(root, true); }
    }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject { if (parent is T match) yield return match; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var c in Find<T>(VisualTreeHelper.GetChild(parent, i))) yield return c; }
    private static void WriteImage(string path) { var bitmap = BitmapSource.Create(64, 16, 96, 96, PixelFormats.Bgr32, null, Enumerable.Repeat((byte)180, 64 * 16 * 4).ToArray(), 64 * 4); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream); }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); Console.WriteLine("PASS: " + label); }
}
