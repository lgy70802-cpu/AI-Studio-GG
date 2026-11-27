using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-creator-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = new DisplayInfo("CREATOR", "Creator target", "creator-device", 0, 0, 1920, 462, false);
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); var initial = new AppSettings { TargetDisplay = new TargetDisplaySettings { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = 1920, LastHeight = 462 } };
            using var manager = new WorkspaceTests.Manager { Displays = [target] }; using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            await devices.RefreshAsync();
            var clockScene = new ClockScene();
            using var main = new MainViewModel(devices, clockScene);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clockScene, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clockScene, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            var store = new ThemeProjectStore(Path.Combine(root, "themes"));
            var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); main.SelectedPage = AppPage.Creator;
            await creator.InitializeAsync();
            Check(creator.IsHome && !creator.HasProjects && !creator.CanCopyCurrent, "Creator starts with real empty projects and disables copying non-editable scenes");
            Check(creator.IsMaterialEmpty && creator.MaterialEmptyText.Contains("尚无"), "Material panel reports a real empty library");
            CheckGeometry();
            creator.TemplateCommand.Execute(null); await creator.PendingOperation;
            Check(creator.IsEditor && creator.Project.Landscape.Layers.Count == 1 && creator.Project.Portrait.Layers.Count == 1, "Clock template creates editable, independent landscape and portrait layers");
            creator.SelectedLayer = creator.Layers[0]; creator.X = 123; creator.FontSize = 150; creator.ShowSeconds = true;
            creator.CenterLayerCommand.Execute(null);
            Check(creator.X == (creator.Project.ActiveLayout.Width - creator.Width) / 2 && creator.Y == (creator.Project.ActiveLayout.Height - creator.Height) / 2, "Center action uses the active layout design coordinates");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            var row = creator.LayerRows.Single(); row.Name = "主时钟";
            Check(creator.SelectedLayer?.Name == "主时钟", "Inline layer rename updates the editable source");
            creator.LayerRows.Single().ToggleLockCommand.Execute(null);
            Check(!creator.CanEditSelection && !creator.CenterLayerCommand.CanExecute(null), "Locked selection disables property editing and alignment commands");
            creator.LayerRows.Single().ToggleLockCommand.Execute(null);
            creator.LayerRows.Single().ToggleVisibilityCommand.Execute(null);
            Check(!creator.SelectedLayer!.Visible, "Layer visibility controls update the source");
            creator.LayerRows.Single().ToggleVisibilityCommand.Execute(null);
            var selectedId = creator.SelectedLayer!.Id;
            creator.BeginGesture(); creator.MoveBy(7, 3); creator.MoveBy(10, 4); creator.EndGesture();
            Check(creator.X == 140, "Canvas gestures mutate design coordinates");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.X == 123, "One undo reverts the entire drag gesture");
            creator.RedoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.X == 140, "Redo restores the gesture without decoded image snapshots");
            creator.Locked = true; creator.MoveBy(100, 0); creator.Width = 900;
            Check(creator.X == 140 && !creator.DeleteCommand.CanExecute(null), "Locked layers reject movement and deletion");
            creator.Locked = false; creator.CopyCommand.Execute(null); creator.PasteCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers.Count == 2 && creator.SelectedLayer?.Id != selectedId, "Copy and paste generate a distinct editable layer ID");
            creator.DeleteCommand.Execute(null); await creator.PendingOperation;
            creator.SelectedLayer = creator.Layers[0]; var landscapeX = creator.X;
            creator.Orientation = ThemeOrientation.Portrait; await creator.PendingOperation; creator.SelectedLayer = creator.Layers[0]; creator.X = 30;
            creator.Orientation = ThemeOrientation.Landscape; await creator.PendingOperation;
            Check(creator.Project.Landscape.Layers[0].X == landscapeX && creator.Project.Portrait.Layers[0].X == 30, "Orientation switching preserves separate coordinates rather than rotating the landscape");
            creator.SelectedLayer = creator.Layers[0]; var portraitCount = creator.Project.Portrait.Layers.Count;
            creator.CopyToOtherLayoutCommand.Execute(null);
            var copied = creator.Project.Portrait.Layers[^1];
            Check(creator.Project.Portrait.Layers.Count == portraitCount + 1 && copied.Id != creator.SelectedLayer.Id && copied.Kind == creator.SelectedLayer.Kind && creator.Project.Landscape.Layers[0].X == landscapeX, "Cross-layout copy creates an independent layer without overwriting the target layout");
            creator.UndoCommand.Execute(null); await creator.PendingOperation;
            Check(creator.Project.Portrait.Layers.Count == portraitCount, "Cross-layout copy is undone as one transaction");
            creator.Add(ThemeElementKind.Rectangle); await creator.PendingOperation; creator.Color = "#4C85F6"; creator.Width = 250; creator.Height = 50; creator.Y = 400;
            var shape = creator.SelectedLayer!; var clock = creator.Project.Landscape.Layers[0]; creator.Reorder(shape.Id, clock.Id);
            Check(shape.ZIndex < clock.ZIndex, "Layer reorder changes the composition draw order");
            creator.Visible = false;
            var assetPath = Path.Combine(root, "background.png");
            var fixture = BitmapSource.Create(64, 16, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 64 * 16).SelectMany(_ => new byte[] { 85, 48, 20, 255 }).ToArray(), 64 * 4);
            var fixtureEncoder = new PngBitmapEncoder(); fixtureEncoder.Frames.Add(BitmapFrame.Create(fixture)); using (var file = File.Create(assetPath)) fixtureEncoder.Save(file);
            var asset = new WallpaperAsset { Name = "真实图片引用", Path = assetPath, Width = 64, Height = 16 };
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog")); await catalog.SaveAsync(new WallpaperLibrary { Assets = [asset] });
            var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            creator.MaterialSearch = "真实图片"; Check(creator.FilteredAssets.Count == 1 && creator.HasFilteredAssets, "Material search reuses the existing wallpaper catalog");
            creator.MaterialSearch = "不存在"; Check(creator.IsMaterialEmpty && creator.MaterialEmptyText.Contains("匹配"), "Material search distinguishes no matches from an empty library"); creator.MaterialSearch = "";
            creator.AddAsset(asset); await creator.PendingOperation;
            Check(creator.SelectedLayer?.AssetId == asset.Id && creator.Scene.Children.ContainsKey(creator.SelectedLayer.Id) && creator.Project.ActiveLayout.Layers.Max(l => l.ZIndex) == creator.SelectedLayer.ZIndex, "New material layer resolves its real asset ID and appears above existing layers");
            creator.Opacity = .7; creator.Fill = ImageDisplayMode.Fill; creator.CropX = .25; creator.Brightness = .9;
            creator.Scene.Tick(new DateTimeOffset(2026, 10, 18, 12, 34, 56, TimeSpan.FromHours(8)));
            using var renderer = new SceneJpegRenderer(); var first = renderer.Render(creator.Scene, 0);
            creator.Scene.Tick(new DateTimeOffset(2026, 10, 18, 12, 34, 57, TimeSpan.FromHours(8))); var second = renderer.Render(creator.Scene, 0);
            Check(!first.SequenceEqual(second), "Composed digital clock renders actual timestamp changes instead of a fixed JPEG");
            creator.Scene.SetCanvasSize(1920, 462); var native = renderer.Render(creator.Scene, 0);
            using (var stream = new MemoryStream(native)) { var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); Check(decoder.Frames[0].PixelWidth == 1920 && decoder.Frames[0].PixelHeight == 462 && creator.Project.ActiveLayout.Height == 480, "USB target pixels do not overwrite portable design coordinates"); }
            creator.ProjectName = "编辑恢复测试";
            var saveStates = new List<string>(); creator.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CreatorViewModel.SaveFeedback)) saveStates.Add(creator.SaveFeedback); };
            Check(creator.DirtyIndicator.Contains("未保存"), "Edited projects show an explicit unsaved indicator");
            await creator.SaveAsync();
            Check(saveStates.Contains("正在保存…") && creator.SaveFeedback.StartsWith("已保存") && !creator.IsDirty && creator.DirtyIndicator.Length == 0, "Save feedback reports progress and clears dirty state only after source persistence");
            creator.Brightness = .8;
            using (var lockedCover = new FileStream(store.CoverPath(creator.Project.Id), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await creator.SaveAsync();
                Check(!creator.IsDirty && creator.SaveFeedback.Contains("源项目已保存") && creator.SaveFeedback.Contains("封面"), "A locked cover does not misreport a successful source save as lost edits");
            }
            await creator.SaveAsync();
            var restored = (await store.ListAsync()).Single();
            Check(restored.Name == "编辑恢复测试" && restored.Landscape.Layers[0].FontSize == 150 && restored.Portrait.Layers[0].X == 30 && File.Exists(store.CoverPath(restored.Id)), "Theme sources restore parameters and generate a real rendered cover");
            Check(restored.Landscape.Layers.Any(l => l.AssetId == asset.Id && l.Path == assetPath && l.CropX == .25 && l.Opacity == .7), "Saved image layers retain existing asset references and crop parameters");
            await creator.OpenProjectAsync(restored); creator.SelectedLayer = creator.Layers.First(l => l.Kind == ThemeElementKind.DigitalClock);
            Check(creator.X == landscapeX && creator.ShowSeconds && creator.FontSize == 150, "Reopening saved projects restores editable clock settings");
            creator.OutputTarget = "Windows 副屏"; creator.ApplyCommand.Execute(null); await creator.PendingOperation;
            Check(ReferenceEquals(main.Scene, creator.Scene) && ReferenceEquals(output.CurrentScene, creator.Scene) && creator.CanCopyCurrent, "Theme application reuses the same scene on the existing display service");
            creator.OutputTarget = "D92 USB";
            var videoLayer = new ThemeLayer { IsVideo = true, Kind = ThemeElementKind.Wallpaper, Path = "missing.mp4" }; creator.Project.ActiveLayout.Layers.Add(videoLayer);
            var rejected = false; try { renderer.Render(creator.Scene, 0); } catch (NotSupportedException) { rejected = true; }
            Check(rejected && creator.OutputCheck.StartsWith("不能应用") && creator.Capability.Contains("一个可见视频"), "Undecoded missing video still fails preflight while the editor advertises the bounded video-composition capability");
            creator.Project.ActiveLayout.Layers.Remove(videoLayer);
            var unavailable = new ThemeLayer { Kind = ThemeElementKind.Image, Path = "missing.png" }; creator.Project.ActiveLayout.Layers.Add(unavailable);
            Check(creator.OutputCheck.StartsWith("不能应用"), "Output preflight reports missing layer content before device application");
            creator.Project.ActiveLayout.Layers.Remove(unavailable);
            var invalid = restored.Clone(); invalid.Landscape.Layers[0].Width = double.NaN;
            var invalidRejected = false; try { await store.SaveAsync(invalid); } catch (InvalidDataException) { invalidRejected = true; }
            Check(invalidRejected && (await store.ListAsync()).Count == 1, "Invalid geometry cannot replace a valid saved theme");
            await File.WriteAllTextAsync(Path.Combine(root, "themes", "corrupt.theme.json"), "invalid");
            Check((await store.ListAsync()).Count == 1 && File.Exists(Path.Combine(root, "themes", "corrupt.theme.json")), "Corrupt theme sources are preserved without preventing project browsing");
            var blockedPath = Path.Combine(root, "blocked-store"); await File.WriteAllTextAsync(blockedPath, "directory is deliberately blocked");
            using (var failing = new CreatorViewModel(main, logger, new ThemeProjectStore(blockedPath)))
            {
                failing.TemplateCommand.Execute(null); await failing.PendingOperation; var failedId = failing.Project.Id;
                failing.SaveCommand.Execute(null); await failing.PendingOperation;
                Check(failing.IsDirty && failing.SaveFeedback.Contains("失败") && failing.Project.ActiveLayout.Layers.Count == 1, "Failed saves retain editable layers and unsaved state");
                failing.BackCommand.Execute(null); await failing.PendingOperation;
                Check(failing.IsEditor && failing.Project.Id == failedId, "A save failure prevents navigation from discarding the current editor");
                var closeRejected = false;
                try { await failing.PrepareForCloseAsync(); } catch (IOException) { closeRejected = true; }
                Check(closeRejected && failing.CanEdit && failing.IsDirty && failing.Project.Id == failedId, "Failed close-time persistence preserves a usable editable project for retry");
            }
            var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 };
            window.Show();
            try
            {
                await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-landscape.png");
                FindVisuals<System.Windows.Controls.Expander>(window).Single(e => Equals(e.Header, "更多设置")).IsExpanded = true;
                creator.Locked = true; window.UpdateLayout();
                var nameField = FindVisuals<System.Windows.Controls.TextBox>(window).First(t => t.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == nameof(CreatorViewModel.LayerName));
                Check(!nameField.IsEnabled && creator.SelectedLayer is not null, "WPF property controls disable for locked layers without losing selection");
                creator.Locked = false; window.UpdateLayout(); Check(nameField.IsEnabled, "Unlock restores WPF property editing");
                creator.LeftTabIndex = 1; window.UpdateLayout();
                var imageName = FindVisuals<System.Windows.Controls.TextBox>(window).First(t => t.DataContext is CreatorLayerRow row && row.Layer.AssetId == asset.Id);
                imageName.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Check(creator.SelectedLayer?.AssetId == asset.Id, "Clicking an inline name selects its layer instead of only focusing the text field");
                creator.SelectedLayer = creator.Layers.First(l => l.Kind == ThemeElementKind.DigitalClock);
                creator.Orientation = ThemeOrientation.Portrait; await creator.PendingOperation; await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-portrait.png");
                window.Width = 1100; window.Height = 740; await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-compact.png");
                creator.PreviewCommand.Execute(null); window.UpdateLayout(); Capture(window, "creator-preview.png");
                Check(window.ActualWidth == 1100 && creator.IsPreview, "Creator editor and preview render at standard and compact window sizes");
                creator.EditCommand.Execute(null); creator.BackCommand.Execute(null); await creator.PendingOperation; window.UpdateLayout(); Capture(window, "creator-home.png");
                Check(creator.IsHome && creator.Projects.Count == 1, "Creator home lists real saved sources and covers");
                using (var coverStream = File.OpenRead(store.CoverPath(creator.Project.Id)))
                {
                    var frame = BitmapDecoder.Create(coverStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0); var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                    var whiteX = Enumerable.Range(0, converted.PixelWidth * converted.PixelHeight).Where(i => pixels[i * 4] > 200 && pixels[i * 4 + 1] > 200 && pixels[i * 4 + 2] > 200).Select(i => i % converted.PixelWidth).ToArray();
                    Check(whiteX.Length > 0 && whiteX.Max() - whiteX.Min() > converted.PixelWidth / 5, "Portrait cover preserves design framing after applying a landscape physical output");
                }
                main.SelectedNavigation = "插件"; Check(main.SelectedPage == AppPage.Plugins, "Existing plugin management remains independently reachable");
            }
            finally { window.Close(); }
            await creator.PrepareForCloseAsync(); Console.WriteLine("All creator tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Capture(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file);
    }
    private static void CheckGeometry()
    {
        static (double X, double Y) Rotate(double x, double y, double degrees) { var a = degrees * Math.PI / 180; return (x * Math.Cos(a) - y * Math.Sin(a), x * Math.Sin(a) + y * Math.Cos(a)); }
        foreach (var angle in new[] { 0d, 45, 90, -30 })
        foreach (var corner in Enumerable.Range(0, 4))
        {
            var layer = new ThemeLayer { X = 20, Y = 30, Width = 200, Height = 100, Rotation = angle };
            var sx = corner is 0 or 2 ? -1 : 1; var sy = corner is 0 or 1 ? -1 : 1;
            var pointer = Rotate(sx * 150, sy * 80, angle);
            var result = ThemeGeometry.ResizeCorner(layer, corner, 120 + pointer.X, 80 + pointer.Y);
            var before = Rotate(-sx * 100, -sy * 50, angle); var after = Rotate(-sx * result.Width / 2, -sy * result.Height / 2, angle);
            if (Math.Abs(result.Width - 250) > .00001 || Math.Abs(result.Height - 130) > .00001 || Math.Abs(result.X + result.Width / 2 + after.X - (120 + before.X)) > .00001 || Math.Abs(result.Y + result.Height / 2 + after.Y - (80 + before.Y)) > .00001) throw new InvalidOperationException("Rotated corner resize moved the opposite anchor");
        }
        Check(true, "All four rotated resize handles preserve the opposite corner at multiple angles");
        var moving = new ThemeLayer { Width = 200, Height = 100 }; var layout = new ThemeLayout { Width = 1920, Height = 480 };
        var snapped = ThemeGeometry.Snap(moving, 854, 188, layout, 8); var free = ThemeGeometry.Snap(moving, 800, 188, layout, 8);
        Check(snapped.X == 860 && snapped.Y == 190 && free.X == 800, "Canvas snapping respects design-coordinate tolerance instead of sticking outside the threshold");
    }
    private static IEnumerable<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T item) yield return item; foreach (var nested in FindVisuals<T>(child)) yield return nested; }
    }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}

