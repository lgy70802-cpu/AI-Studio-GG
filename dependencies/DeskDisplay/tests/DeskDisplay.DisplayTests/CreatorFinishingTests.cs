using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorFinishingTests
{
    public static async Task RunAsync(MainViewModel main, CreatorViewModel creator, ThemeProjectStore store, WallpaperAsset asset, ILogger logger)
    {
        await CheckOutputMapping(logger);
        var project = new ThemeProject { Name = "组合管理与交互验收" }; var first = new ThemeLayer { Kind = ThemeElementKind.Text, Name = "标题", Text = "D92 Studio", Width = 300, Height = 100, X = 100, Y = 80, Color = "#4C85F6", FontSize = 60, Spacing = 2, Shadow = true, ZIndex = 2 };
        var second = new ThemeLayer { Kind = ThemeElementKind.DigitalClock, Name = "实时时钟", X = 700, Y = 150, Width = 600, Height = 120, ShowSeconds = true, ZIndex = 1 };
        project.Landscape.Layers.AddRange([first, second]); await creator.OpenProjectAsync(project); creator.SelectLayers([first.Id, second.Id]); creator.GroupSelection(); var group = first.GroupId!.Value;
        Check(creator.LayerRows.Count(r => r.IsGroup) == 1 && creator.LayerRows.Count(r => r.IsMember) == 2, "Layer list exposes a real group header and its members");
        creator.RenameGroup(group, "标题与时钟"); Check(project.Landscape.Layers.All(l => l.GroupName == "标题与时钟") && creator.LayerRows.First(r => r.IsGroup).Name == "标题与时钟", "Group rename updates all source members and the visible header");
        creator.ToggleGroupExpanded(group); Check(creator.LayerRows.Count == 1 && creator.SelectionCount == 2, "Collapsing a group hides child rows without deleting or deselecting layers");
        creator.ToggleGroupState(group, true); Check(project.Landscape.Layers.All(l => l.Locked) && !creator.RotateGroupCommand.CanExecute(null), "Whole-group lock prevents rotation and geometry edits");
        creator.ToggleGroupState(group, true); creator.ToggleGroupState(group, false); Check(project.Landscape.Layers.All(l => !l.Visible), "Whole-group visibility updates all source members");
        creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.Project.ActiveLayout.Layers.All(l => l.Visible), "Group visibility is one undo transaction");
        creator.ToggleGroupExpanded(group); creator.SelectLayers([first.Id]); var before = creator.SelectedLayers.Select(l => l.Clone()).ToArray(); var b = ThemeGeometry.Union(before);
        creator.GroupAngle = 90; creator.RotateGroupCommand.Execute(null);
        var rotated = creator.SelectedLayers; var a0 = before[0]; var a1 = rotated.First(l => l.Id == a0.Id); var cx = b.X + b.Width / 2; var cy = b.Y + b.Height / 2;
        Check(Math.Abs(a1.X + a1.Width / 2 - (cx - (a0.Y + a0.Height / 2 - cy))) < .00001 && a1.Rotation == a0.Rotation + 90, "Group rotation transforms each center around the selection center and rotates member content");
        creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.SelectedLayers.First(l => l.Id == a0.Id).X == a0.X, "Group rotation can be undone without changing membership");
        creator.AlignCanvasCommand.Execute("居中"); var centered = ThemeGeometry.Union(creator.SelectedLayers);
        Check(Math.Abs(centered.X + centered.Width / 2 - creator.Project.ActiveLayout.Width / 2) < .00001 && Math.Abs(centered.Y + centered.Height / 2 - creator.Project.ActiveLayout.Height / 2) < .00001, "Align-to-canvas centers the group as a unit");
        creator.SelectedLayer = creator.Project.ActiveLayout.Layers.First(l => l.Kind == ThemeElementKind.Text); creator.CopyStyleCommand.Execute(null);
        creator.SelectedLayer = creator.Project.ActiveLayout.Layers.First(l => l.Kind == ThemeElementKind.DigitalClock); var clockId = creator.SelectedLayer!.Id; var clockBounds = new ThemeBounds(creator.X, creator.Y, creator.Width, creator.Height); creator.PasteStyleCommand.Execute(null);
        Check(creator.Color == "#4C85F6" && creator.FontSize == 60 && creator.Shadow && creator.ShowSeconds && creator.SelectedLayer.Id == clockId && clockBounds == new ThemeBounds(creator.X, creator.Y, creator.Width, creator.Height), "Style paste changes appearance while preserving clock behavior, identity and geometry");
        await creator.SaveAsync(); Check((await store.ListAsync()).Single(p => p.Id == creator.Project.Id).Landscape.Layers.All(l => l.GroupName == "标题与时钟"), "Group names survive formal source save");
        creator.ReportSnap(true); Check(creator.SnapFeedback.Contains("吸附"), "Canvas snapping exposes visual feedback"); creator.ReportSnap(false);
        creator.AssetCategory = "全部分类"; creator.AssetSubcategory = "全部子分类"; creator.AssetOrientation = "全部方向"; creator.AssetOrigin = "全部来源"; creator.MaterialSearch = "";
        creator.MaterialBrowserSavedFilter = creator.MaterialBrowserFilterKey; creator.MaterialBrowserSelection = asset.Id; creator.MaterialBrowserOffset = 30;
        var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 }; window.Show();
        try
        {
            await Task.Delay(100); window.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(window); Console.WriteLine($"METRIC: Actual WPF test window DPI={dpi.PixelsPerInchX}x{dpi.PixelsPerInchY}; scaled captures below are simulations, not system DPI changes.");
            creator.SelectLayers([clockId]); window.UpdateLayout(); Capture(window, "creator-group-management.png", 1);
            window.Width = 1100; window.Height = 740; await Task.Delay(100); window.UpdateLayout();
            foreach (var scale in new[] { 1.25, 1.5 })
            {
                Capture(window, $"creator-scaled-{scale * 100:0}.png", scale);
                var canvas = Find<ThemeCanvas>(window).Single(); var p = new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2); var hit = canvas.InputHitTest(p);
                Check(canvas.ActualWidth > 0 && hit is not null, $"Compact creator remains laid out and hit-testable during {scale:P0} simulated render capture");
            }
            using (var displays = new WindowsDisplayManager(logger))
            {
                var found = await displays.GetDisplaysAsync(); var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle; window.Opacity = 0;
                foreach (var display in found)
                {
                    if (!SetWindowPos(hwnd, IntPtr.Zero, display.X + 20, display.Y + 20, 0, 0, 0x15)) throw new System.ComponentModel.Win32Exception();
                    await Task.Delay(150); window.UpdateLayout(); var actual = VisualTreeHelper.GetDpi(window);
                    Console.WriteLine($"METRIC: Existing monitor {display.Width}x{display.Height}: test window DPI={actual.PixelsPerInchX}x{actual.PixelsPerInchY}; no system scaling settings changed.");
                    Check(Find<ThemeCanvas>(window).Single().ActualWidth > 0 && actual.PixelsPerInchX > 0, "Creator window remains laid out after moving to an enumerated real display");
                }
                window.Left = -30000; window.Top = -30000; window.Opacity = 1;
            }
            var browser = new CreatorMaterialBrowser(creator) { Owner = window, ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; browser.Show(); await Task.Delay(100); browser.UpdateLayout();
            var list = Find<ListBox>(browser).Single(); Check(list.SelectedItem is WallpaperItemViewModel selected && selected.Asset.Id == asset.Id, "Material selection is restored when reopening the browser");
            var scroll = Find<ScrollViewer>(list).First(); scroll.ScrollToVerticalOffset(20); browser.UpdateLayout(); var offset = scroll.VerticalOffset; browser.Close();
            var reopened = new CreatorMaterialBrowser(creator) { Owner = window, ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; reopened.Show(); await Task.Delay(100); reopened.UpdateLayout();
            try
            {
                Check(Math.Abs(Find<ScrollViewer>(Find<ListBox>(reopened).Single()).First().VerticalOffset - offset) < 1, "Browser restores its actual scroll position without resetting filters");
                var previewButton = Find<Button>(reopened).Single(btn => Equals(btn.Content, "大图预览"));
                var closePreview = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; var opened = false;
                closePreview.Tick += (_, _) => { var popup = reopened.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.Title == "素材大图预览"); if (popup is not null) { opened = true; popup.Close(); closePreview.Stop(); } }; closePreview.Start();
                try { previewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); } finally { closePreview.Stop(); }
                Check(opened, "Large preview opens a real resizable image window and returns to the browser");
                Capture(reopened, "creator-material-browser-retained.png", 1);
            }
            finally { reopened.Close(); }
        }
        finally { window.Close(); }
        await CheckBrowserCache(main, asset, logger);
        Console.WriteLine("All creator finishing tests passed.");
    }
    private static async Task CheckBrowserCache(MainViewModel parent, WallpaperAsset original, ILogger logger)
    {
        var root = Path.Combine(Path.GetDirectoryName(original.Path)!, "browser-cache"); Directory.CreateDirectory(root);
        var assets = new List<WallpaperAsset>();
        for (var i = 0; i < 100; i++) { var path = Path.Combine(root, i + ".png"); File.Copy(original.Path, path); assets.Add(new() { Name = "缓存素材" + i.ToString("D3"), Path = path, Width = 800, Height = 200 }); }
        assets.Add(new() { Name = "缺失素材", Path = Path.Combine(root, "missing.png"), Width = 800, Height = 200 });
        var portraitPath = Path.Combine(root, "portrait.png");
        var pixels = Enumerable.Repeat((byte)150, 256 * 1024 * 4).ToArray(); var portrait = BitmapSource.Create(256, 1024, 96, 96, PixelFormats.Bgr32, null, pixels, 256 * 4); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(portrait)); using (var file = File.Create(portraitPath)) png.Save(file);
        var portraitItem = new WallpaperItemViewModel(new() { Name = "竖图缩略图", Path = portraitPath, Width = 256, Height = 1024 }, portraitPath); var thumbnail = await portraitItem.EnsureThumbnailAsync();
        Check(thumbnail is { PixelHeight: 640, PixelWidth: 160 }, "Portrait thumbnails bound their longest edge instead of allocating a tall 640-pixel-wide bitmap"); portraitItem.ReleaseThumbnail();
        var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog")); await catalog.SaveAsync(new() { Assets = assets });
        var settings = new Settings(); using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(settings.Value, settings, logger), logger, output);
        var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
        main.SetImageEditor(new ImageSceneViewModel(devices, settings, settings.Value, clock, main.PreviewScene, logger));
        main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
        main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), settings.Value, logger, false));
        await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
        var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
        var creator = new CreatorViewModel(main, logger, new ThemeProjectStore(Path.Combine(root, "themes"))); main.SetCreator(creator); await creator.OpenProjectAsync(new ThemeProject());
        await Task.WhenAll(library.AllItems.Select(i => i.EnsureThumbnailAsync()));
        var active = library.AllItems.Where(i => i.IsAvailable).Take(2).ToArray(); library.TrimTransientImages(active);
        var protectedItems = library.VisibleItems.Concat(library.RelatedItems).Concat(library.RecentItems).Concat(active).ToHashSet(); if (library.Selected is not null) protectedItems.Add(library.Selected);
        Check(library.AllItems.Count(i => i.HasCachedThumbnail) <= 72 + protectedItems.Count && active.All(i => i.HasCachedThumbnail) && library.AllItems.Count(i => !i.HasCachedThumbnail) > 1, "Transient browser cache retains active items and evicts old thumbnails after browsing a large library");
        CreatorMaterialBrowser Open() { var browser = new CreatorMaterialBrowser(creator) { ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; browser.Show(); browser.UpdateLayout(); return browser; }
        var first = Open(); await Task.Delay(100);
        Find<Button>(first).Single(b => Equals(b.Content, "下一页")).Command.Execute(null); await Task.Delay(50); first.UpdateLayout(); var list = Find<ListBox>(first).Single(); list.SelectedItem = list.Items[2]; var selected = (WallpaperItemViewModel)list.SelectedItem; Find<ScrollViewer>(list).First().ScrollToVerticalOffset(35); first.UpdateLayout(); var offset = Find<ScrollViewer>(list).First().VerticalOffset; first.Close();
        var restored = Open(); await Task.Delay(100); restored.UpdateLayout();
        var restoredList = Find<ListBox>(restored).Single();
        Check(creator.MaterialBrowserPage == 2 && restoredList.SelectedItem is WallpaperItemViewModel remembered && remembered.Asset.Id == selected.Asset.Id && Math.Abs(Find<ScrollViewer>(restoredList).First().VerticalOffset - offset) < 1, "Material browser restores page, selected asset and scroll position in a multi-page library");
        restoredList.SelectedItem = restoredList.Items[0]; var addedId = ((WallpaperItemViewModel)restoredList.SelectedItem).Asset.Id; var container = (ListBoxItem)restoredList.ItemContainerGenerator.ContainerFromIndex(0); var count = creator.Project.ActiveLayout.Layers.Count;
        restoredList.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = container });
        await creator.PendingOperation; for (var i = 0; i < 30 && restored.IsVisible; i++) await Task.Delay(20);
        Check(!restored.IsVisible && creator.Project.ActiveLayout.Layers.Count == count + 1 && creator.Project.ActiveLayout.Layers.Last().AssetId == addedId, "Double-click adds the selected real asset and closes the browser after loading");
        if (restored.IsVisible) restored.Close();
        creator.MaterialSearch = "缺失"; var missing = Open(); await Task.Delay(100); missing.UpdateLayout(); var missingList = Find<ListBox>(missing).Single(); missingList.SelectedItem = missingList.Items[0]; missing.UpdateLayout();
        Check(!Find<Button>(missing).Single(b => Equals(b.Content, "添加到画布")).IsEnabled && Find<TextBlock>(missing).Any(t => t.IsVisible && t.Text.Contains("文件失效")), "Missing assets show an explicit warning and disable add/preview actions"); missing.Close();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    private static async Task CheckOutputMapping(ILogger logger)
    {
        foreach (var rotation in new[] { 0, 90, 180, 270 }) foreach (var size in new[] { (800, 240), (240, 800) }) foreach (var orientation in Enum.GetValues<ThemeOrientation>())
        {
            var g = ThemeOutputGeometry.Resolve(size.Item1, size.Item2, rotation, orientation);
            var originalRaster = rotation % 180 == 0 ? size : (size.Item2, size.Item1);
            var raster = g.Rotation % 180 == 0 ? (g.Width, g.Height) : (g.Height, g.Width);
            if (originalRaster != raster || (g.Height > g.Width) != (orientation == ThemeOrientation.Portrait)) throw new InvalidOperationException("Output orientation changed native raster footprint");
        }
        Check(true, "All quarter-turns and both design orientations preserve the configured encoded raster footprint");
        var settings = new Settings(); using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(settings.Value, settings, logger), logger, output);
        var source = new ThemeProject(); using var scene = new ThemeScene(source); using var main = new MainViewModel(devices, scene); var target = new Target();
        using var usb = new UsbOutputViewModel(main, settings, settings.Value, logger, target, () => [new("test", "orientation-test", 1025)]); main.SetUsb(usb); await usb.RefreshAsync(); await usb.StartAsync();
        var landscape = Decode(await target.Capture()); source.Orientation = ThemeOrientation.Portrait; scene.UpdateProject(source); var portrait = Decode(await target.Capture());
        Check(landscape == (240, 800) && portrait == landscape && scene.PixelWidth == 240 && scene.PixelHeight == 800, "Actual USB viewmodel maps portrait frames without changing encoded dimensions");
        Check(settings.Value.UsbOutput is { Width: 800, Height: 240, RotationDegrees: 90 } && source.Landscape.Width == 1920 && source.Portrait.Height == 1920, "Orientation mapping preserves saved USB preferences and independent design layouts");
        main.PreviewScene(new ClockScene());await usb.ApplyCurrentAsync(); var clock = Decode(await target.Capture());
        Check(clock == landscape && main.Scene.PixelWidth == 800 && main.Scene.PixelHeight == 240, "Non-theme scene output keeps its original canvas and rotation behavior");
        await usb.StopAsync();
    }
    private static (int, int) Decode(byte[] bytes) { using var stream = new MemoryStream(bytes); var image = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; return (image.PixelWidth, image.PixelHeight); }
    private sealed class Settings : ISettingsService
    {
        public AppSettings Value = new() { UsbOutput = new() { SerialNumber = "orientation-test", Width = 800, Height = 240, RotationDegrees = 90 } };
        public Task<AppSettings> LoadAsync() => Task.FromResult(Value);
        public Task<bool> SaveAsync(AppSettings value) { Value = value; return Task.FromResult(true); }
    }
    private sealed class Target : ID92RenderTarget
    {
        private readonly Channel<TaskCompletionSource<byte[]>> _requests = Channel.CreateUnbounded<TaskCompletionSource<byte[]>>();
        public event Action<D92OutputState>? StateChanged { add { } remove { } }
        public async Task<byte[]> Capture() { var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously); await _requests.Writer.WriteAsync(completion); return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        public async Task RunAsync(string serial, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken token)
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(token)) { try { request.TrySetResult(await render(token)); } catch (Exception e) { request.TrySetException(e); throw; } }
        }
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T t) yield return t; foreach (var nested in Find<T>(child)) yield return nested; } }
    private static void Capture(Window window, string name, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)(window.ActualWidth * scale), (int)(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
