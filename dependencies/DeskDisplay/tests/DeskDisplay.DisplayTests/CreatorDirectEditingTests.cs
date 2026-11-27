using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorDirectEditingTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-direct-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var displays = new Displays(); using var devices = new DevicesViewModel(displays, new TargetDisplayService(new(), new Settings(), logger), logger);
            using var main = new MainViewModel(devices, new ClockScene());
            using var output = new WorkspaceTests.Output();

            main.SetImageEditor(new ImageSceneViewModel(devices, new Settings(), new AppSettings(), new ClockScene(), main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, new ClockScene(), new PerformanceScene(), output, new Settings(), new WorkspaceTests.Sensors(), new AppSettings(), logger, false));
            await main.Workspace!.RestoreAsync();
            var library = new LibraryViewModel(main, new WallpaperCatalog(logger, Path.Combine(root, "library")), logger); main.SetLibrary(library); await library.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); var editor = new CreatorViewModel(main, logger, store); main.SetCreator(editor); main.SelectedPage = AppPage.Creator;
            await editor.InitializeAsync(); editor.TemplateCommand.Execute(null); await editor.PendingOperation;
            editor.Add(ThemeElementKind.Text); await editor.PendingOperation; editor.Text = "原始文字"; editor.X = 120; editor.Y = 10; editor.Width = 650; editor.Height = 100; editor.FontSize = 60;
            var window = new MainWindow { DataContext = main, ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000 }; window.Show(); await Task.Delay(100); window.UpdateLayout();
            try
            {
                var view = Find<CreatorView>(window).Single(v => v.IsVisible); var canvas = (ThemeCanvas)view.FindName("Canvas");
                Check(canvas.BeginInlineTextEdit(), "Literal text opens a genuine editable canvas TextBox");
                var input = canvas.Children.OfType<TextBox>().Single(); Check(input.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && input.TextWrapping == TextWrapping.NoWrap, "Multiline input can scroll without pretending long unbroken text wraps in the output"); Check(((SolidColorBrush)input.Foreground).Color != ((SolidColorBrush)input.Background).Color, "Inline editor remains readable for white theme text without changing its saved color"); input.Text = "画布内修改\n第二行";
                window.UpdateLayout(); Capture(window, "creator-six-inline.png"); canvas.CommitInlineTextEdit();
                Check(editor.Text == "画布内修改\n第二行" && !canvas.IsInlineEditing, "Inline commit writes real editable source including multiline text");
                editor.UndoCommand.Execute(null); await editor.PendingOperation;
                Check(editor.Text == "原始文字", "One undo reverses the complete inline text transaction");
                canvas.BeginInlineTextEdit(); canvas.Children.OfType<TextBox>().Single().Text = "不保存"; canvas.CommitInlineTextEdit(false);
                Check(editor.Text == "原始文字", "Canceling inline text leaves source unchanged");
                canvas.BeginInlineTextEdit(); canvas.Children.OfType<TextBox>().Single().Text = "保存时尚在输入的文字"; await editor.SaveAsync();
                Check(!canvas.IsInlineEditing && (await store.ListAsync()).Single().ActiveLayout.Layers.Any(l => l.Text == "保存时尚在输入的文字"), "Saving commits pending canvas text before writing formal editable source");
                editor.Locked = true; Check(!canvas.BeginInlineTextEdit(), "Locked text refuses inline editing"); editor.Locked = false;
                editor.SelectedLayer = editor.Project.ActiveLayout.Layers.First(l => l.Kind == ThemeElementKind.DigitalClock);
                Check(!canvas.BeginInlineTextEdit(), "Live clock cannot be replaced with literal inline text");
                var image = Path.Combine(root, "source.png"); var bitmap = BitmapSource.Create(80, 20, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 1600).SelectMany(_ => new byte[] { 220, 140, 60, 255 }).ToArray(), 320); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(image)) encoder.Save(stream);
                var bytes = await File.ReadAllBytesAsync(image); var before = editor.Project.ActiveLayout.Layers.Count;
                await editor.InsertImageFilesAsync([image], 200, 80);
                var inserted = editor.SelectedLayer!;
                Check(editor.Project.ActiveLayout.Layers.Count == before + 1 && inserted.Kind == ThemeElementKind.Image && inserted.AssetId is not null && inserted.Fill == ImageDisplayMode.Uniform && Math.Abs(inserted.Width / inserted.Height - 4) < .001, "Canvas file insertion reuses real library identity and original image aspect");
                Check(editor.Scene.Children.GetValueOrDefault(inserted.Id) is ImageScene && inserted.X == 200 && inserted.Y == 80, "Inserted image decodes through actual scene rendering at design coordinates");
                await editor.InsertImageFilesAsync([image], 0, 0, true); var background = editor.SelectedLayer!; var total = editor.Project.ActiveLayout.Layers.Count;
                await editor.InsertImageFilesAsync([image], 0, 0, true);
                Check(editor.Project.ActiveLayout.Layers.Count == total && editor.SelectedLayer!.Id == background.Id && background.ZIndex < inserted.ZIndex, "Quick background replacement reuses the background layer below foreground content");
                Check(bytes.SequenceEqual(await File.ReadAllBytesAsync(image)) && library.AllItems.Count == 1, "Repeated insertion preserves original file bytes and does not duplicate asset records");
                var unsupported = Path.Combine(root, "unsupported.txt"); await File.WriteAllTextAsync(unsupported, "not an image"); await editor.InsertImageFilesAsync([unsupported], 0, 0);
                Check(editor.Project.ActiveLayout.Layers.Count == total, "Unsupported dropped files create no fake image layers");
                editor.SelectLayers(editor.Project.ActiveLayout.Layers.Where(l => l.Kind is ThemeElementKind.Text or ThemeElementKind.Image).Select(l => l.Id)); editor.GroupCommand.Execute(null);
                var group = editor.SelectedLayers[0].GroupId!.Value; editor.ToggleGroupExpanded(group);
                editor.LeftTabIndex = 1; await Task.Delay(100); window.UpdateLayout();
                var geometry = Find<Expander>(view).Single(e => Equals(e.Header, "位置与尺寸")); geometry.IsExpanded = true;
                Find<Button>(view).Single(b => Equals(b.ToolTip, "折叠属性")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80);
                var zoom = (Slider)view.FindName("ZoomSlider"); zoom.Value = 1.5; await Task.Delay(80); window.UpdateLayout();
                await editor.SaveAsync(); await editor.FlushEditorViewportAsync();
                var state = await store.ReadViewportAsync(editor.Project.Id, editor.Orientation, editor.Project.SelectedLayoutId);
                Check(state?.Workbench is { RightOpen: false, LeftTab: 1 } saved && saved.ExpandedSections.Contains("位置与尺寸") && saved.CollapsedGroups.Contains(group) && state.Zoom == 1.5, "Workspace sidecar saves panels, property sections, collapsed groups and zoom");
                var snapshot = editor.Project.Clone(); await editor.OpenProjectAsync(snapshot); await Task.Delay(100); window.UpdateLayout();
                Check(((FrameworkElement)view.FindName("RightPanel")).Visibility == Visibility.Collapsed && geometry.IsExpanded && zoom.Value == 1.5 && editor.IsGroupCollapsed(group), "Reopening editable source restores the actual WPF workspace without overwriting it during restore");
                var newStore = new ThemeProjectStore(Path.Combine(root, "themes")); Check((await newStore.ReadViewportAsync(editor.Project.Id, editor.Orientation))?.Workbench?.CollapsedGroups.Contains(group) == true, "Workspace state survives a fresh store instance");
                Find<Button>(view).Single(b => Equals(b.ToolTip, "折叠属性")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80); window.UpdateLayout(); Capture(window, "creator-six-workspace.png");
                var actions = (Border)view.FindName("SelectionActions"); var frame = canvas.TranslatePoint(new Point(0, 0), (FrameworkElement)view.FindName("CanvasWorkspace"));
                Check(!actions.IsVisible || actions.Margin.Top + actions.ActualHeight <= frame.Y || actions.Margin.Left + actions.ActualWidth <= frame.X || actions.Margin.Left >= frame.X + canvas.ActualWidth || actions.Margin.Top >= frame.Y + canvas.ActualHeight, "Floating actions stay outside the whole theme frame or hide when no safe space exists");
                editor.Orientation = ThemeOrientation.Portrait; await editor.PendingOperation; await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-six-portrait.png");
                Check(canvas.ActualHeight > canvas.ActualWidth, "Direct editing workbench retains independent portrait geometry");
            }
            finally { window.Close(); }
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is T t) yield return t; foreach (var n in Find<T>(c)) yield return n; } }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream); }
    private sealed class Settings : ISettingsService { public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings()); public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true); }
    private sealed class Displays : IDisplayManager { public event EventHandler? DisplaysChanged { add { } remove { } } public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]); public void Dispose() { } }
}
