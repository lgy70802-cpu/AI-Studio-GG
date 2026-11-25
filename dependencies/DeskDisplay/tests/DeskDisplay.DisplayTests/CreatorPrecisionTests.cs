using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorPrecisionTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-precision-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var displays = new Displays(); using var devices = new DevicesViewModel(displays, new TargetDisplayService(new(), new Settings(), logger), logger); using var main = new MainViewModel(devices, new ClockScene()); using var output = new WorkspaceTests.Output();
            main.SetImageEditor(new ImageSceneViewModel(devices, new Settings(), new(), new ClockScene(), main.PreviewScene, logger)); main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger)); main.SetWorkspace(new WorkspaceViewModel(main, new ClockScene(), new PerformanceScene(), output, new Settings(), new WorkspaceTests.Sensors(), new(), logger, false)); await main.Workspace!.RestoreAsync();
            var library = new LibraryViewModel(main, new WallpaperCatalog(logger, Path.Combine(root, "library")), logger); main.SetLibrary(library); await library.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); var editor = new CreatorViewModel(main, logger, store); main.SetCreator(editor); main.SelectedPage = AppPage.Creator; await editor.InitializeAsync();
            var project = new ThemeProject(); project.Landscape.Width = 400; project.Landscape.Height = 200; project.Landscape.Background = "#000000";
            project.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Text, Name = "多行标题", Text = "Line A\nLine B", X = 10, Y = 10, Width = 220, Height = 180, FontSize = 24, Color = "#FFFFFF" });
            await editor.OpenProjectAsync(project); editor.SelectedLayer = editor.Project.ActiveLayout.Layers[0];
            var legacy = JsonSerializer.Deserialize<ThemeLayer>("{\"Text\":\"old\"}")!;
            Check(legacy.VerticalAlignment == ThemeVerticalAlignment.Center && !legacy.AutoGrowTextHeight, "Old layer JSON retains centered fixed-height behavior without migration writes");
            var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; window.Show(); await Task.Delay(100); window.UpdateLayout();
            try
            {
                var view = Find<CreatorView>(window).Single(v => v.IsVisible);
                editor.TextVerticalAlignment = ThemeVerticalAlignment.Top; var top = WhiteY(editor.Scene);
                editor.TextVerticalAlignment = ThemeVerticalAlignment.Bottom; var bottom = WhiteY(editor.Scene);
                Check(bottom > top + 70, "Actual scene pixels move multiline text from top to bottom alignment");
                editor.Height = 20; Check(editor.TextOverflowHint.Length > 0, "Text overflow is reported using actual font metrics");
                var width = editor.Width; editor.AutoGrowTextHeight = true; var prior = editor.Height; editor.Text = "One\nTwo\nThree\n四";
                Check(editor.Height > prior && editor.Width == width, "Multiline auto-growth expands height without stretching text or changing width");
                editor.UndoCommand.Execute(null); await editor.PendingOperation;
                Check(editor.Text == "Line A\nLine B" && Math.Abs(editor.Height - prior) < .01, "One undo restores both text and automatically grown height");
                editor.AutoGrowTextHeight = false; editor.Height = 20; editor.FitTextHeightCommand.Execute(null); Check(editor.Height >= ThemeTextMetrics.Measure(editor.SelectedLayer!).Height, "Explicit fit height uses renderer metrics");
                await editor.SaveAsync(); var saved = (await store.ListAsync()).Single().ActiveLayout.Layers.Single(); Check(saved.VerticalAlignment == ThemeVerticalAlignment.Bottom && !saved.AutoGrowTextHeight, "Vertical alignment and growth option persist in editable source");
                var path = Path.Combine(root, "image.png"); var bitmap = BitmapSource.Create(80, 20, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 1600).SelectMany(_ => new byte[] { 160, 110, 60, 255 }).ToArray(), 320); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream); var original = await File.ReadAllBytesAsync(path);
                await editor.InsertImageFilesAsync([path], 40, 30); var image = editor.SelectedLayer!; editor.ApplyImageCrop(image.Id, 3, .2, .8); var asset = library.AllItems.Single().Asset;
                await editor.ReplaceSelectedAssetAsync(asset); Check(image.ImageZoom == 3 && image.CropX == .2 && image.CropY == .8, "Image replacement preserves crop by default");
                await editor.ReplaceSelectedAssetAsync(asset, false); Check(image.ImageZoom == 1 && image.CropX == .5 && image.CropY == .5, "Image replacement can explicitly reset crop while keeping geometry");
                editor.ApplyImageCrop(image.Id, 2, .3, .7); editor.ResetImageCropCommand.Execute(null); Check(image.ImageZoom == 1 && image.Fill == ImageDisplayMode.Uniform && image.CropX == .5, "One-click reset restores complete proportional image display");
                editor.UndoCommand.Execute(null); await editor.PendingOperation; image = editor.SelectedLayer!; Check(image.ImageZoom == 2 && image.CropX == .3, "Crop reset is a single undo transaction");
                var crop = new CreatorCropEditor(image, bitmap) { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; crop.Show(); await Task.Delay(60); crop.UpdateLayout();
                var before = crop.CropX; crop.PanImage(new Vector(80, 0)); Check(crop.CropX < before && crop.CropX >= 0, "Crop dragging pans the actual image in the expected direction with bounds");
                Find<Button>(crop).Single(b => Equals(b.Content, "恢复原比例")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(crop.RestoreAspect, "Crop original-aspect action is explicit");
                Find<Button>(crop).Single(b => Equals(b.Content, "重置裁剪")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(!crop.RestoreAspect && crop.Zoom == 1 && crop.CropX == .5, "Reset clears a previously requested aspect change"); crop.Close();
                Check(image.ImageZoom == 2 && original.SequenceEqual(await File.ReadAllBytesAsync(path)), "Closing crop preview without apply preserves layer crop and original file bytes");
                editor.Add(ThemeElementKind.Rectangle); await editor.PendingOperation; var shape = editor.SelectedLayer!; editor.SelectLayers([image.Id, shape.Id]); editor.GroupCommand.Execute(null); var group = shape.GroupId;
                editor.SelectGroupMemberCommand.Execute(null); Check(editor.SelectionCount == 1 && editor.SelectedLayer!.GroupId == group && editor.GroupMembers(group!.Value).Count == 2, "Group member editing keeps the original group relationship");
                var otherX = editor.GroupMembers(group!.Value).Single(l => l.Id != editor.SelectedLayer!.Id).X; editor.MoveBy(5, 0); Check(editor.GroupMembers(group.Value).Single(l => l.Id != editor.SelectedLayer!.Id).X == otherX, "Editing a selected group member does not move its neighbor");
                editor.ToggleSelectionLockCommand.Execute(null); Check(editor.SelectedLayer!.Locked, "Layer lock action updates the true source"); editor.ToggleSelectionLockCommand.Execute(null);
                var count = editor.Project.ActiveLayout.Layers.Count; editor.DuplicateSelectionCommand.Execute(null); await editor.PendingOperation; Check(editor.Project.ActiveLayout.Layers.Count > count, "Layer duplicate command creates editable copies"); editor.UndoCommand.Execute(null); await editor.PendingOperation;
                var list = (ListBox)view.FindName("LayerList"); Check(list.ContextMenu!.Items.OfType<MenuItem>().Any(m => m.GetBindingExpression(MenuItem.CommandProperty)?.ParentBinding.Path.Path == "SelectGroupMemberCommand"), "Actual layer menu exposes group member editing alongside existing commands");
                var layout = new ThemeLayout { Width = 400, Height = 200, Layers = [new() { X = 100, Y = 40, Width = 50, Height = 30 }] }; var moving = new ThemeLayer { Width = 40, Height = 20 };
                Check(ThemeGeometry.Snap(moving, 97, 41, layout, 6).X == 100, "Existing design-coordinate snapping aligns to a nearby layer edge");
                editor.Add(ThemeElementKind.Text); await editor.PendingOperation; editor.Text = "This text is intentionally wider than the box"; editor.Width = 30; editor.X = -40; var issueLayer = editor.SelectedLayer!.Id;
                editor.Add(ThemeElementKind.Image); await editor.PendingOperation; editor.SelectedLayer!.Path = Path.Combine(root, "missing.png"); var missing = editor.SelectedLayer.Id;
                var issues = editor.InspectTheme(); Check(issues.Any(i => i.LayerId == issueLayer && !i.Blocking && i.Message.Contains("画布外")) && issues.Any(i => i.LayerId == missing && i.Blocking), "Preflight separates locatable layout warnings from blocking missing media");
                Check(editor.InspectTheme(true).Any(i => i.LayerId is null && i.Blocking), "Preflight checks actual unavailable device configuration without claiming compatibility");
                var report = new CreatorPreflightDialog(editor) { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; report.Show(); report.UpdateLayout(); Capture(report, "creator-precision-preflight.png"); Check(!Find<Button>(report).Single(b => Equals(b.Content, "继续预览并检查设备")).IsEnabled, "Real preflight window blocks continuation when assets are missing"); report.Close();
                editor.LocatePreflightIssue(issueLayer); Check(editor.SelectedLayer?.Id == issueLayer && editor.LeftTabIndex == 1, "Preflight location selects the exact layer in the editor");
                window.UpdateLayout(); Capture(window, "creator-precision-editor.png");
            }
            finally { window.Close(); }
        }
        finally { Directory.Delete(root, true); }
    }
    private static double WhiteY(ThemeScene scene)
    {
        var view = new SceneView { Scene = scene, Width = 400, Height = 200 }; view.Measure(new(400, 200)); view.Arrange(new(0, 0, 400, 200)); view.UpdateLayout(); var image = new RenderTargetBitmap(400, 200, 96, 96, PixelFormats.Pbgra32); image.Render(view); var pixels = new byte[400 * 200 * 4]; image.CopyPixels(pixels, 1600, 0); var points = Enumerable.Range(0, 400 * 200).Where(i => pixels[i * 4] > 180 && pixels[i * 4 + 1] > 180 && pixels[i * 4 + 2] > 180).Select(i => (double)(i / 400)).ToArray(); return points.Average();
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is T t) yield return t; foreach (var nested in Find<T>(c)) yield return nested; } }
    private static void Capture(FrameworkElement view, string name) { var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private sealed class Settings : ISettingsService { public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings()); public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true); }
    private sealed class Displays : IDisplayManager { public event EventHandler? DisplaysChanged { add { } remove { } } public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]); public void Dispose() { } }
}
