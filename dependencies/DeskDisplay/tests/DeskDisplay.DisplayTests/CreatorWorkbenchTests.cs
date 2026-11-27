using System.IO;
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

internal static class CreatorWorkbenchTests
{
    public static async Task RunAsync(ILogger logger)
    {
        Check(NumericExpression.TryEvaluate("1920/2 + (20*3)", out var result) && result == 1020 && NumericExpression.TryEvaluate("-5 + .5", out result) && result == -4.5, "Numeric expressions support bounded arithmetic and parentheses");
        Check(!NumericExpression.TryEvaluate("1/0", out _) && !NumericExpression.TryEvaluate("System.IO.File.Delete(1)", out _) && !NumericExpression.TryEvaluate("2*(3", out _), "Numeric expressions reject zero division, code and malformed input without evaluation");
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-workbench-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = new DisplayInfo("WORKBENCH", "测试副屏", "workbench-target", 0, 0, 1920, 462, false);
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); var initial = new AppSettings { TargetDisplay = new() { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = target.Width, LastHeight = target.Height } };
            using var manager = new WorkspaceTests.Manager { Displays = [target] }; using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger)); main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false)); await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog")); var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); using var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); main.SelectedPage = AppPage.Creator;
            var imagePath = Path.Combine(root, "crop.png"); var pixels = new byte[800 * 200 * 4]; for (var i = 0; i < 800 * 200; i++) { pixels[i * 4] = (byte)(i % 800 < 400 ? 20 : 220); pixels[i * 4 + 2] = (byte)(i % 800 < 400 ? 220 : 20); }
            var bitmap = BitmapSource.Create(800, 200, 96, 96, PixelFormats.Bgr32, null, pixels, 800 * 4); bitmap.Freeze(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(imagePath)) encoder.Save(file);
            var project = new ThemeProject { Name = "裁剪与应用验收", Category = "风景" }; var photo = new ThemeLayer { Name = "真实图片", Kind = ThemeElementKind.Image, Path = imagePath, AssetOrigin = ThemeAssetOrigin.Local, X = 100, Y = 50, Width = 400, Height = 200 }; var text = new ThemeLayer { Kind = ThemeElementKind.Text, Text = "编辑测试", Name = "文字", X = 700, Width = 400 }; var shape = new ThemeLayer { Kind = ThemeElementKind.Rectangle, Locked = true, X = 1200, Name = "锁定形状" }; project.ActiveLayout.Layers.AddRange([photo, text, shape]);
            await creator.OpenProjectAsync(project); creator.SelectedLayer = photo; creator.ApplyImageCrop(photo.Id, 2, 0, .5); var renderer = new SceneJpegRenderer(); var left = renderer.Render(creator.Scene, 0);
            creator.ApplyImageCrop(photo.Id, 2, 1, .5); var right = renderer.Render(creator.Scene, 0);
            Check(!left.SequenceEqual(right) && photo.ImageZoom == 2 && File.Exists(imagePath), "Editable crop parameters change the real JPEG renderer without modifying the original image");
            creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.SelectedLayer?.CropX == 0, "Crop changes restore as one source undo step");
            creator.ApplyImageCrop(photo.Id, 1, .5, .5, 4); var actual = creator.SelectedLayer!;
            Check(actual.Width / actual.Height == 4 && actual.ImageZoom == 1 && actual.CropX == .5, "Original image ratio and reset crop are restored as editable source parameters");
            creator.GridEnabled = true; creator.GridSpacing = 25; var snapped = ThemeGeometry.Snap(new ThemeLayer { Width = 100, Height = 50 }, 49, 74, creator.Project.ActiveLayout, 2);
            Check(snapped.X == 50 && snapped.Y == 75, "Configured grid spacing participates in design-coordinate snapping");
            var withGrid = renderer.Render(creator.Scene, 0); creator.GridEnabled = false; var withoutGrid = renderer.Render(creator.Scene, 0); Check(withGrid.SequenceEqual(withoutGrid), "Editor grid does not appear in the shared output renderer"); creator.GridEnabled = true;
            creator.SelectLayers([photo.Id, text.Id, shape.Id]); creator.BatchOpacity = .4; creator.ApplyBatch("opacity");
            Check(creator.Project.ActiveLayout.Layers.Where(l => l.Id != shape.Id).All(l => l.Opacity == .4) && creator.Project.ActiveLayout.Layers.Single(l => l.Id == shape.Id).Opacity == 1, "Batch opacity affects unlocked selections while retaining locked layers");
            creator.UndoCommand.Execute(null); await creator.PendingOperation; Check(creator.Project.ActiveLayout.Layers.All(l => l.Opacity == 1), "One undo restores the entire batch property change");
            creator.BatchColor = "#FFBF69"; creator.ApplyBatch("color"); creator.BatchFontSize = 66; creator.ApplyBatch("font");
            Check(creator.Project.ActiveLayout.Layers.Single(l => l.Id == text.Id) is { Color: "#FFBF69", FontSize: 66 } && creator.Project.ActiveLayout.Layers.Single(l => l.Id == photo.Id).Color == "#FFFFFF", "Batch font and color apply only to compatible unlocked types");
            creator.ApplyBatch("unlock"); Check(creator.SelectedLayers.All(l => !l.Locked), "Batch unlock can unlock the selected locked layer");
            await creator.SaveAsync(); var saved = (await store.ListAsync()).Single(); Check(saved.Category == "风景" && saved.ActiveLayout.GridSpacing == 25 && saved.ActiveLayout.Layers.Single(l => l.Id == photo.Id).CropX == .5, "Crop, grid and project category persist in actual editable source");
            creator.ProjectCategoryFilter = "风景"; creator.ProjectSearch = "风景"; Check(creator.VisibleProjects.Count == 1, "Project search matches saved categories and category filters"); creator.ProjectSearch = ""; creator.ProjectCategoryFilter = "全部分类";
            var template = await store.SaveTemplateAsync(saved); await store.RenameTemplateAsync(template.Id, "我的裁剪模板"); Check((await store.ListTemplatesAsync()).Single().Name == "我的裁剪模板", "Custom template rename persists without replacing the original theme"); await store.DeleteTemplateAsync(template.Id); Check((await store.ListTemplatesAsync()).Count == 0 && File.Exists(imagePath) && (await store.ListAsync()).Count == 1, "Deleting a custom template preserves original source and assets");
            var managedTemplate = await store.SaveTemplateAsync(saved);
            using (var templatePreview = new CreatorViewModel(main, logger, store))
            {
                var browser = new CreatorSourceBrowser(await store.ListTemplatesAsync(), templatePreview, "自定义模板", "使用模板", store.RenameTemplateAsync, store.DeleteTemplateAsync) { ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; browser.Show();
                try { await Task.Delay(60); ((TextBox)browser.FindName("TemplateName")).Text = "窗口重命名模板"; Find<Button>(browser).Single(b => Equals(b.Content, "重命名")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); for (var retry = 0; retry < 20 && (await store.ListTemplatesAsync()).Single().Name != "窗口重命名模板"; retry++) await Task.Delay(20); Check((await store.ListTemplatesAsync()).Single().Name == "窗口重命名模板", "Actual custom-template manager renames the persisted source through the UI"); browser.UpdateLayout(); Capture(browser, "creator-workbench-templates.png"); }
                finally { browser.Close(); }
            }
            var invalidPlaylist = false; try { await store.SaveThemePlaylistAsync(new("Windows 副屏", [new(saved.Id, 1)])); } catch (InvalidDataException) { invalidPlaylist = true; } Check(invalidPlaylist, "Playlist storage rejects out-of-range per-theme duration");
            creator.OutputTarget = "D92 USB"; Check(!creator.CheckThemeApplication().CanApply, "Application preflight refuses an absent D92 device"); creator.OutputTarget = "Windows 副屏"; Check(creator.CheckThemeApplication().CanApply, "Application preflight accepts the configured isolated Windows target");
            var confirmation = new CreatorApplyConfirmation(creator) { ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; confirmation.Show();
            try { confirmation.UpdateLayout(); Capture(confirmation, "creator-workbench-confirm.png"); manager.Displays = []; await devices.RefreshAsync(); Find<Button>(confirmation).Single(b => Equals(b.Content, "确认应用")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(confirmation.IsVisible && confirmation.CurrentCheck?.CanApply == false && !output.IsOpen, "Confirmation rechecks a disconnected target instead of accepting a stale enabled button"); }
            finally { confirmation.Close(); }
            manager.Displays = [target]; await devices.RefreshAsync();
            creator.SelectedLayer = creator.Project.ActiveLayout.Layers.Single(l => l.Id == photo.Id);
            var cropWindow = new CreatorCropEditor(creator.SelectedLayer, bitmap) { ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; cropWindow.Show(); try { await Task.Delay(50); cropWindow.UpdateLayout(); Check(Find<Image>(cropWindow).Single().Source is not null, "Actual crop editor displays the real image rather than a mock background"); Capture(cropWindow, "creator-workbench-crop.png"); } finally { cropWindow.Close(); }
            var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 }; window.Show();
            try { await Task.Delay(100); window.UpdateLayout(); Find<Expander>(window).Single(e => Equals(e.Header, "位置与尺寸")).IsExpanded = true; window.UpdateLayout(); var stepper = Find<NumericStepper>(window).First(n => n.GetBindingExpression(NumericStepper.ValueProperty)?.ParentBinding.Path?.Path == "Width"); var input = Find<TextBox>(stepper).Single(); input.Text = "200+50"; input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Check(creator.SelectedLayer.Width == 250, "Actual WPF dimension field evaluates arithmetic through its MVVM binding"); await Task.Delay(40); window.UpdateLayout(); Capture(window, "creator-workbench-editor.png"); }
            finally { window.Close(); }
            await creator.FlushEditorViewportAsync(); await creator.SaveAsync();
            var second = new ThemeProject { Name = "实时时钟轮播" }; second.ActiveLayout.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, ShowSeconds = true }); await store.SaveAsync(second);
            var broken = new ThemeProject { Name = "缺失素材主题" }; broken.ActiveLayout.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = Path.Combine(root, "gone.png") }); await store.SaveAsync(broken); await creator.RefreshProjectsAsync(); await creator.LoadThemePlaylistAsync();
            creator.ThemePlaylistTarget = "Windows 副屏"; creator.AddThemePlaylist(saved.Id); creator.AddThemePlaylist(broken.Id); creator.AddThemePlaylist(second.Id); foreach (var item in creator.ThemePlaylistItems) item.Seconds = 10; await creator.FlushThemePlaylistAsync();
            Check((await store.ReadThemePlaylistAsync()).Entries.Count == 3 && !creator.IsThemeRotating, "Theme playlist saves source IDs and per-theme duration without starting output");
            var managerWindow = new CreatorThemePlaylist(creator) { ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; managerWindow.Show(); try { await Task.Delay(60); managerWindow.UpdateLayout(); Check(Find<DataGrid>(managerWindow).Single().Columns[0].ActualWidth >= 260, "Playlist manager reserves readable space for actual theme titles"); Capture(managerWindow, "creator-workbench-playlist.png"); } finally { managerWindow.Close(); }
            var editingId = creator.Project.Id; await creator.StartThemePlaylistAsync(); Check(output.CurrentScene is ThemeScene first && first.Project.Id == saved.Id && creator.Project.Id == editingId, "Theme playback uses the existing output service without replacing the active editing project");
            await Task.Delay(10600); Check(output.CurrentScene is ThemeScene next && next.Project.Id == second.Id && creator.IsThemeRotating, "Actual dispatcher timer advances saved themes and skips missing media");
            main.PreviewScene(new ClockScene()); Check(!creator.IsThemeRotating, "Manual scene changes stop theme switching instead of being overwritten by the next timer");
            using var fresh = new CreatorViewModel(main, logger, store); await fresh.LoadThemePlaylistAsync(); Check(fresh.ThemePlaylistItems.Count == 3 && !fresh.IsThemeRotating, "A fresh session restores playlist settings without automatically outputting to a device");
            await creator.FlushThemePlaylistAsync(); await creator.FlushEditorViewportAsync();
        }
        finally { Directory.Delete(root, true); }
    }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject { if (parent is T match) yield return match; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var c in Find<T>(VisualTreeHelper.GetChild(parent, i))) yield return c; }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); Console.WriteLine("PASS: " + label); }
}
