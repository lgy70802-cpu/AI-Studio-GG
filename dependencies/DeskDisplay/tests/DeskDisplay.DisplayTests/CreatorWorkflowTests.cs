using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorWorkflowTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-creator-workflow-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = new DisplayInfo("WORKFLOW", "Isolated output", "workflow-target", 0, 0, 1920, 462, false);
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            var initial = new AppSettings { TargetDisplay = new() { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = target.Width, LastHeight = target.Height } };
            using var manager = new WorkspaceTests.Manager { Displays = [target] }; using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var image = Path.Combine(root, "landscape.png"); WriteImage(image);
            var portrait = Path.Combine(root, "portrait.png"); WriteImage(portrait);
            var a = new WallpaperAsset { Name = "本地山景", Category = "风景", Subcategory = "山川", Tags = "晴天", Path = image, Width = 800, Height = 200 };
            var b = new WallpaperAsset { Name = "云端竖屏", Category = "动漫", Path = portrait, Width = 200, Height = 800, CloudResourceId = "cloud-owned" };
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog")); await catalog.SaveAsync(new WallpaperLibrary { Assets = [a, b] });
            var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); main.SelectedPage = AppPage.Creator; await creator.InitializeAsync();
            await store.DeleteDraftAsync(Guid.NewGuid());
            Check(!Directory.Exists(Path.Combine(root, "themes", "Drafts")), "Cleaning a never-created draft succeeds without creating directories or reporting an error");
            var freshStore = new ThemeProjectStore(Path.Combine(root, "fresh-delete")); var deletionSource = new ThemeProject(); await freshStore.SaveAsync(deletionSource); await freshStore.DeleteAsync(deletionSource.Id);
            Check((await freshStore.ListAsync()).Count == 0, "Deleting a project succeeds before any draft directory has ever been created");
            creator.NewCommand.Execute(null); await creator.PendingOperation;
            creator.ProjectName = "流程验收"; creator.AddAsset(a); await creator.PendingOperation;
            creator.Add(ThemeElementKind.DigitalClock); await creator.PendingOperation;
            creator.FontFamily = "Segoe UI"; creator.FontSize = 88; creator.Spacing = 4; creator.Shadow = true; creator.Color = "#4C85F6"; creator.Alignment = "居右"; creator.ShowSeconds = true; creator.Hour12 = true;
            creator.X = 123; creator.Y = 56; var clockId = creator.SelectedLayer!.Id;
            creator.Orientation = ThemeOrientation.Portrait; await creator.PendingOperation;
            creator.Add(ThemeElementKind.Text); await creator.PendingOperation; creator.Text = "独立竖屏"; creator.X = 42;
            creator.Orientation = ThemeOrientation.Landscape; await creator.PendingOperation; await creator.SaveAsync();
            var source = (await store.ListAsync()).Single(); var savedClock = source.Landscape.Layers.Single(l => l.Id == clockId);
            Check(savedClock.FontFamily == "Segoe UI" && savedClock.FontSize == 88 && savedClock.Spacing == 4 && savedClock.Shadow && savedClock.Color == "#4C85F6" && savedClock.Alignment == "居右" && savedClock.ShowSeconds && savedClock.Hour12, "Clock typography, spacing, shadow, color and time options survive formal source persistence");
            Check(source.Portrait.Layers.Single().Text == "独立竖屏" && source.Portrait.Layers.Single().X == 42 && savedClock.X == 123, "Workflow preserves separately authored horizontal and vertical layouts");
            await creator.OpenProjectAsync(source.Clone()); creator.SelectedLayer = creator.Layers.Single(l => l.Id == clockId);
            Check(creator.FontSize == 88 && creator.Shadow && creator.Spacing == 4 && creator.X == 123, "Reopening a formal project restores editable properties and coordinates");
            using var renderer = new SceneJpegRenderer(); creator.Scene.SetCanvasSize(1920, 462);
            creator.Scene.Tick(new DateTimeOffset(2026, 10, 20, 12, 34, 56, TimeSpan.FromHours(8))); var frame1 = renderer.Render(creator.Scene, 0);
            creator.Scene.Tick(new DateTimeOffset(2026, 10, 20, 12, 34, 57, TimeSpan.FromHours(8))); var frame2 = renderer.Render(creator.Scene, 0);
            Check(!frame1.SequenceEqual(frame2) && frame1.Length > 1000, "Restored clock produces changing real rendered JPEG frames through the shared output renderer");
            var transfer = new ThemeTransferService(store); var bundle = Path.Combine(root, "workflow.d92theme");
            await transfer.ExportAsync(creator.Project, bundle, true, library.AllItems.ToDictionary(i => i.Asset.Id, i => i.Asset));
            await creator.ImportProjectFileAsync(bundle);
            Check(creator.IsDirty && creator.Project.Id != source.Id && creator.Project.Landscape.Layers.Any(l => l.Kind == ThemeElementKind.DigitalClock && l.Spacing == 4) && creator.Scene.Children.Count == 1, "Full package export and actual creator import restore editable clock and real local media");
            creator.OutputTarget = "Windows 副屏"; creator.ApplyCommand.Execute(null); await creator.PendingOperation;
            Check(ReferenceEquals(output.CurrentScene, creator.Scene) && ReferenceEquals(main.Scene, creator.Scene) && !creator.IsDirty, "Imported project saves and applies through the existing simulated output service without a second render chain");
            creator.OutputTarget = "D92 USB"; creator.ApplyCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Status.Contains("连接 D92"), "Workflow refuses to report D92 output success without a connected device");
            creator.AssetCategory = "风景"; Check(creator.FilteredAssets.Any(i => i.Asset.Id == a.Id) && creator.FilteredAssets.All(i => i.Category == "风景"), "Creator filters existing library categories without creating duplicate assets");
            creator.AssetCategory = "全部分类"; creator.AssetOrigin = "云端已下载"; creator.AssetOrientation = "竖屏 1:4";
            Check(creator.FilteredAssets.Count == 1 && creator.FilteredAssets[0].Asset.Id == b.Id, "Origin and orientation filters combine for downloaded portrait content");
            creator.AssetOrigin = "本地素材"; Check(creator.IsMaterialEmpty, "Combined filters expose a genuine empty state");
            creator.AssetOrigin = "全部来源"; creator.AssetOrientation = "全部方向"; creator.MaterialSearch = "山川";
            Check(creator.FilteredAssets.Count == 1 && creator.FilteredAssets[0].Asset.Id == a.Id, "Material search includes the library's secondary category");
            creator.MaterialSearch = "";

            var edit = new ThemeProject { Name = "多选排版" };
            edit.Landscape.Layers.AddRange([
                new() { Name = "一", Kind = ThemeElementKind.Text, X = 100, Y = 20, Width = 100, Height = 60, ZIndex = 1 },
                new() { Name = "二", Kind = ThemeElementKind.Text, X = 400, Y = 80, Width = 200, Height = 60, ZIndex = 2 },
                new() { Name = "三", Kind = ThemeElementKind.Rectangle, X = 1000, Y = 160, Width = 100, Height = 60, ZIndex = 3 },
                new() { Name = "锁定", Kind = ThemeElementKind.Text, Locked = true, X = 50, Y = 30, Width = 50, Height = 60, ZIndex = 4 },
                new() { Name = "背景", Kind = ThemeElementKind.Wallpaper, AssetId = a.Id, Path = image, Width = 1920, Height = 480, ZIndex = -1 }
            ]);
            await creator.OpenProjectAsync(edit);
            creator.SelectRegion(0, 0, 1200, 400, false); Check(creator.SelectionCount == 3 && creator.IsMultiSelection && !creator.CanEditSelection, "Marquee selects contained visible unlocked foreground layers and hides single-element properties");
            creator.LayerRows.Single(r => r.Layer.Name == "锁定").IsSelected = true;
            Check(creator.SelectionCount == 4, "Layer checkboxes add a locked layer to selection without discarding the other selected layers");
            creator.BeginGesture(); creator.MoveBy(10, 5); creator.MoveBy(20, 10); creator.EndGesture();
            Check(creator.Project.ActiveLayout.Layers[0].X == 130 && creator.Project.ActiveLayout.Layers[1].X == 430 && creator.Project.ActiveLayout.Layers[3].X == 50, "Group dragging preserves spacing and leaves locked layers unchanged");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers[0].X == 100 && creator.Project.ActiveLayout.Layers[1].X == 400 && creator.SelectionCount == 4, "One undo restores a whole multi-layer gesture with selection identities retained");
            creator.FocusSelectedLayer(creator.Project.ActiveLayout.Layers[0]);
            Check(creator.SelectedLayer?.Name == "一" && creator.SelectionCount == 4, "Dragging an unlocked selected layer can make it primary without dropping the group or moving locked layers");
            creator.ArrangeSelection("左对齐"); Check(creator.SelectedLayers.Where(l => !l.Locked).All(l => l.X == 100) && creator.Project.ActiveLayout.Layers[3].X == 50, "Alignment excludes locked layers");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            creator.ArrangeSelection("水平等距");
            Check(creator.Project.ActiveLayout.Layers[1].X == 500, "Equal-gap distribution accounts for unequal element widths and keeps outside edges fixed");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            creator.CopyCommand.Execute(null); var beforePaste = creator.Project.ActiveLayout.Layers.Count;
            creator.PasteCommand.Execute(null); await creator.PendingOperation;
            Check(creator.SelectionCount == 4 && creator.Project.ActiveLayout.Layers.Count == beforePaste + 4 && creator.SelectedLayers.All(l => l.Id != edit.Landscape.Layers[0].Id), "Multi-layer clipboard allocates new identities and selects the pasted set");
            creator.DeleteCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers.Count == beforePaste + 1, "Group deletion preserves the locked pasted layer");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers.Count == beforePaste + 4, "Group deletion is one undo transaction");
            var scaled = ThemeGeometry.ResizeCorner(new ThemeLayer { X = 10, Y = 20, Width = 200, Height = 100, Rotation = 30 }, 3, 500, 200, true);
            Check(Math.Abs(scaled.Width / scaled.Height - 2) < 0.000001, "Shift resize preserves the original aspect ratio even for rotated elements");
            var rotated = new ThemeLayer { X = 40, Y = 80, Width = 120, Height = 60, Rotation = 90 };
            var rb = ThemeGeometry.VisualBounds(rotated);
            Check(Math.Abs(rb.Width - 60) < 0.000001 && Math.Abs(rb.Height - 120) < 0.000001, "Alignment and marquee bounds account for element rotation");
            creator.SelectLayers(creator.Project.ActiveLayout.Layers.Where(l => !l.Locked && l.Kind != ThemeElementKind.Wallpaper).Take(3).Select(l => l.Id));
            var window = new MainWindow { DataContext = main, ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000 }; window.Show();
            try
            {
                await Task.Delay(100); window.UpdateLayout();
                Check(creator.SelectionCount == 3 && creator.LayerRows.Count(r => r.IsSelected) == 3, "Actual WPF list bindings preserve multi-selection instead of resetting it during refresh");
                Visuals<Expander>(window).Single(e => Equals(e.Header, "对齐与分布")).IsExpanded = true; window.UpdateLayout();
                Capture(window, "creator-multiselect.png");
                var controls = Visuals<Button>(window).Where(bn => bn.IsVisible && Equals(bn.Content, "水平等距")).ToArray();
                Check(controls.Length == 1 && controls[0].IsEnabled, "WPF multi-selection exposes an enabled alignment panel");
                creator.SelectedLayer = creator.Project.ActiveLayout.Layers.First(l => l.Kind == ThemeElementKind.Text && !l.Locked); window.UpdateLayout();
                creator.Color = "#31B98D"; creator.Shadow = true; creator.Spacing = 3;
                var colorEditor = Visuals<ComboBox>(window).First(c => c.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding.Path?.Path == nameof(CreatorViewModel.Color));
                colorEditor.Text = "#FFBF69"; colorEditor.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
                Check(creator.Color == "#FFBF69", "The actual WPF color preset editor writes immediately into the selected layer");
                var opacity = Visuals<Slider>(window).First(s => s.GetBindingExpression(Slider.ValueProperty)?.ParentBinding.Path?.Path == nameof(CreatorViewModel.Opacity));
                opacity.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
                creator.Opacity = .25; creator.Opacity = .65;
                opacity.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseUpEvent });
                creator.UndoCommand.Execute(null); await creator.PendingOperation;
                Check(creator.Opacity == 1, "A WPF property slider gesture is one undo transaction instead of one entry per value change");
                var selected = creator.SelectedLayer; creator.FontFamily = "missing font / invalid";
                Check(creator.SelectedLayer == selected && creator.Status.Contains("已安装"), "Font editor rejects unavailable fonts without corrupting the selected layer");
                window.Width = 1100; window.Height = 740; await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-workflow-compact.png");
            }
            finally { window.Close(); }
            await creator.SaveAsync(); creator.BackCommand.Execute(null); await creator.PendingOperation;
            var card = creator.Projects.First(p => p.Project.Id == creator.Project.Id);
            await creator.RenameProjectAsync(card.Project.Id, "AAA 重命名"); creator.ProjectSearch = "AAA"; creator.ProjectSort = "名称升序";
            Check(creator.VisibleProjects.Count == 1 && creator.VisibleProjects[0].Name == "AAA 重命名", "Saved project rename, name search and sorting use real persisted sources");
            await creator.DuplicateSavedProjectAsync(creator.VisibleProjects[0]); await creator.RefreshProjectsAsync();
            var duplicate = creator.Projects.Single(p => p.Name == "AAA 重命名 副本");
            Check(duplicate.Project.Id != card.Project.Id && File.Exists(duplicate.CoverPath), "Project duplication creates an independent source and copies the real cover");
            await creator.DeleteSavedProjectAsync(duplicate.Project.Id);
            Check(!File.Exists(duplicate.CoverPath) && creator.Projects.All(p => p.Project.Id != duplicate.Project.Id) && File.Exists(image) && library.AllItems.Any(i => i.Asset.Id == a.Id), "Project deletion removes source and cover while preserving original media and library records");

            await CreatorAdvancedTests.RunAsync(main, creator, store, a, b);
            await CreatorFinishingTests.RunAsync(main, creator, store, a, logger);
            await CreatorAdvancedTests.RunImageStressAsync(creator, root);
            var heavy = new ThemeProject { Name = "100层压力验收" };
            for (var i = 0; i < 30; i++) heavy.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "共享图片" + i, Path = image, X = i * 3, Width = 800, Height = 200, ZIndex = i });
            for (var i = 30; i < 100; i++) heavy.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Text, Name = "文本" + i, Text = "压力检查 " + i, X = i * 8, Y = i % 10 * 30, Width = 200, Height = 60, FontSize = 24, ZIndex = i });
            var watch = Stopwatch.StartNew(); var decodeStart = creator.ImageDecodeCount;
            await creator.OpenProjectAsync(heavy);
            Check(creator.Scene.Children.Count == 30 && creator.Scene.Children.Values.Distinct().Count() == 1 && creator.ImageDecodeCount - decodeStart <= 1, "Thirty layers sharing one image reuse one decoded scene instead of thirty bitmap allocations");
            creator.SelectedLayer = creator.Project.ActiveLayout.Layers[40];
            for (var i = 0; i < 20; i++) { creator.MoveBy(1, 0); creator.UndoCommand.Execute(null); await creator.PendingOperation; }
            Check(creator.ImageDecodeCount - decodeStart <= 1 && creator.Project.ActiveLayout.Layers.Count == 100, "Repeated full-project undo reuses unchanged decoded media without losing layers");
            var frameTimes = new List<double>();
            for (var i = 0; i < 15; i++) { var frameWatch = Stopwatch.StartNew(); creator.Scene.Tick(DateTimeOffset.Now.AddSeconds(i)); renderer.Render(creator.Scene, 0); frameTimes.Add(frameWatch.Elapsed.TotalMilliseconds); await Task.Yield(); }
            Check(frameTimes.Count == 15 && creator.CanEdit, "A maximum-layer source repeatedly renders through the real JPEG renderer and remains editable");
            var retainedBefore = GC.GetTotalMemory(true); var sustained = Stopwatch.StartNew(); var pulses = 0;
            for (var i = 0; i < 100; i++)
            {
                creator.Scene.Tick(DateTimeOffset.Now.AddSeconds(i)); renderer.Render(creator.Scene, 0);
                main.SelectedPage = AppPage.Library; main.SelectedPage = AppPage.Creator;
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background); pulses++;
            }
            var retainedAfter = GC.GetTotalMemory(true);
            Check(pulses == 100 && creator.Project.ActiveLayout.Layers.Count == 100 && creator.ImageDecodeCount - decodeStart <= 1, "A 100-frame render and navigation loop keeps dispatcher work running and does not repeatedly decode shared media");
            var weakEditors = Enumerable.Range(0, 8).Select(_ => MakeDisposedEditor(main, logger, store)).ToArray();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Check(weakEditors.All(w => !w.IsAlive), "Disposed creator instances release main-viewmodel subscriptions and dispatcher timers for collection");
            var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime; var wall = Stopwatch.StartNew(); await Task.Delay(1100); var cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
            var report = $"100-layer source; 30 repeated-image layers; additional decodes={creator.ImageDecodeCount - decodeStart}; workflow seconds={watch.Elapsed.TotalSeconds:F2}; JPEG median ms={frameTimes.Order().ElementAt(frameTimes.Count / 2):F2}; JPEG max ms={frameTimes.Max():F2}; sustained frames=100 in {sustained.Elapsed.TotalSeconds:F2}s; managed retained bytes before={retainedBefore}, after={retainedAfter}, delta={retainedAfter - retainedBefore}; idle CPU ms={cpu.TotalMilliseconds:F2} over {wall.Elapsed.TotalMilliseconds:F0}ms. These are local short-run measurements, not a long-duration or physical-device guarantee.";
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "creator-performance.txt"), report); Console.WriteLine("METRIC: " + report);
            await creator.PrepareForCloseAsync();
            Console.WriteLine("All creator workflow tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference MakeDisposedEditor(MainViewModel main, ILogger logger, ThemeProjectStore store)
    {
        var editor = new CreatorViewModel(main, logger, store); editor.Dispose(); return new WeakReference(editor);
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is T t) yield return t; foreach (var nested in Visuals<T>(c)) yield return nested; }
    }
    private static void Capture(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file);
    }
    private static void WriteImage(string path)
    {
        var bitmap = BitmapSource.Create(800, 200, 96, 96, PixelFormats.Bgr32, null, Enumerable.Repeat((byte)150, 800 * 200 * 4).ToArray(), 800 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private static void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); Console.WriteLine("PASS: " + description); }
}
