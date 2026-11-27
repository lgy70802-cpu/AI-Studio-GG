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

internal static class CreatorUsabilityTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-usability-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            using var display = new Displays(); using var device = new DevicesViewModel(display, new TargetDisplayService(new(), new Settings(), logger), logger);
            using var main = new MainViewModel(device, new ClockScene()); var store = new ThemeProjectStore(directory);
            var editor = new CreatorViewModel(main, logger, store); main.SetCreator(editor); main.SelectedPage = AppPage.Creator;
            await editor.InitializeAsync(); await editor.OpenProjectAsync(new ThemeProject());
            var shell = new MainWindow { DataContext = main, Width = 1320, Height = 900, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; shell.Show(); await Task.Delay(80); shell.UpdateLayout();
            var view = Find<CreatorView>(shell).Single(v => v.IsVisible);
            Check(((FrameworkElement)view.FindName("EmptyCanvasActions")).IsVisible, "Blank projects expose actionable image clock and text entry points");
            var addClock = Find<Button>(view).Single(b => Equals(b.Content, "添加时钟") && b.IsVisible); addClock.Command.Execute(addClock.CommandParameter); await editor.PendingOperation; shell.UpdateLayout();
            Check(editor.Project.ActiveLayout.Layers.Single().Kind == ThemeElementKind.DigitalClock && !((FrameworkElement)view.FindName("EmptyCanvasActions")).IsVisible, "Empty-canvas clock action creates a real editable layer and removes the hint");
            var compactClock = editor.SelectedLayer!;
            Check(compactClock.Width < editor.CanvasWidth / 2 && compactClock.Width >= DeskDisplay.Display.Views.ThemeTextMetrics.MeasureClock(compactClock).Width, "New clock uses compact renderer-matched bounds instead of eighty percent canvas width");
            compactClock.Width = 1500; compactClock.Height = 240; compactClock.Rotation = 30;
            var centreX = compactClock.X + compactClock.Width / 2; var centreY = compactClock.Y + compactClock.Height / 2;
            editor.FitClockContentCommand.Execute(null); await editor.PendingOperation;
            var fitted = editor.SelectedLayer!;
            Check(fitted.Width < 1500 && Math.Abs(fitted.X + fitted.Width / 2 - centreX) < .01 && Math.Abs(fitted.Y + fitted.Height / 2 - centreY) < .01 && fitted.Rotation == 30, "Fit clock tightens the frame while retaining the rotated layer centre");
            var fittedWidth = fitted.Width; editor.Scene.Tick(DateTimeOffset.Now.AddSeconds(1));
            Check(fitted.Width == fittedWidth, "Clock ticks never resize fitted bounds");
            editor.UndoCommand.Execute(null); await editor.PendingOperation;
            Check(editor.SelectedLayer!.Width == 1500 && editor.SelectedLayer.Height == 240, "Clock fitting is one reversible editing operation");
            editor.UndoCommand.Execute(null); await editor.PendingOperation; shell.UpdateLayout();
            Check(editor.Project.ActiveLayout.Layers.Count == 0 && ((FrameworkElement)view.FindName("EmptyCanvasActions")).IsVisible, "Undo restores the blank-canvas entry points without losing editing controls");
            editor.ConfirmLeave = _ => CreatorLeaveChoice.Discard; editor.TemplateCommand.Execute(null); await editor.PendingOperation; editor.ConfirmLeave = null; shell.UpdateLayout();
            var selected = editor.Layers[0]; editor.SelectedLayer = selected; shell.UpdateLayout();
            Check(((FrameworkElement)view.FindName("ElementProperties")).IsVisible, "Selecting a layer automatically opens its element properties");
            ((Button)view.FindName("CanvasPropertiesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); shell.UpdateLayout();
            Check(((FrameworkElement)view.FindName("CanvasProperties")).IsVisible && editor.SelectedLayer?.Id == selected.Id, "Canvas settings stay accessible while an element remains selected");
            var canvasWidth = editor.CanvasWidth; editor.CanvasWidth = canvasWidth + 1; shell.UpdateLayout();
            Check(((FrameworkElement)view.FindName("CanvasProperties")).IsVisible && editor.CanvasWidth == canvasWidth + 1, "Editing canvas properties does not unexpectedly switch the inspector back to elements");
            editor.UndoCommand.Execute(null); await editor.PendingOperation;
            ((Button)view.FindName("ElementPropertiesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); shell.UpdateLayout();
            Check(((FrameworkElement)view.FindName("ElementProperties")).IsVisible && editor.SelectedLayer?.Id == selected.Id, "Returning to element properties preserves the current selection and undo history");
            var projectMenu = Find<Button>(view).Single(b => Equals(b.Content, "项目 ▾")); projectMenu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
            var menu = projectMenu.ContextMenu!; var commands = menu.Items.OfType<MenuItem>().Select(i => i.Command).ToArray();
            Check(commands.Contains(editor.SaveAsCommand) && commands.Contains(editor.ExportSourceCommand) && commands.Contains(editor.ExportBundleCommand) && commands.Contains(editor.HistoryCommand) && commands.Contains(editor.CheckAssetsCommand), "Project menu retains actual save-as, export, bundle, history and material-check commands");
            menu.IsOpen = false;
            Check(!Find<Expander>((FrameworkElement)view.FindName("CanvasLayoutBar")).Any() && Find<Expander>((FrameworkElement)view.FindName("CanvasProperties")).Any(e => Equals(e.Header, "布局管理")), "Advanced layout form moves into canvas properties without losing its existing section identity");
            editor.SelectedLayer = null; shell.UpdateLayout();
            Check(((FrameworkElement)view.FindName("CanvasProperties")).IsVisible, "Clearing selection exposes canvas settings instead of an unrelated element panel");
            editor.SelectedLayer = selected; shell.UpdateLayout();
            var tabs = (TabControl)view.FindName("EditorLeftTabs");
            Check(tabs.Items.Cast<TabItem>().Select(t => t.Header).SequenceEqual(new[] { "添加内容", "图层" }) && !Find<Expander>(view).Single(e => Equals(e.Header, "布局管理")).IsExpanded, "Editor separates materials and layers and initially collapses layout management");
            var bar = (FrameworkElement)view.FindName("CanvasLayoutBar");
            Check(Find<ComboBox>(bar).Any(c => c.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path == "LayoutChoices") && !Find<ComboBox>((FrameworkElement)view.FindName("LeftPanel")).Any(c => c.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path == "LayoutChoices"), "Layout selection is centralized above the canvas rather than mixed with material tools");
            var options = Find<Button>(view).Single(b => Equals(b.Content, "视图 ▾")); options.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
            var viewMenu = options.ContextMenu!;
            Check(ReferenceEquals(viewMenu.DataContext, editor), "View popup retains the real editor data context across its popup boundary");
            var gridMenu = viewMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "网格"));
            gridMenu.IsChecked = true; Check(editor.GridEnabled, "View menu updates actual grid setting"); gridMenu.IsChecked = false;
            var guideMenu = viewMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "标尺 / 参考线"));
            guideMenu.IsChecked = false; guideMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(!((ThemeCanvas)view.FindName("Canvas")).ShowGuides, "View menu controls actual canvas guides");
            guideMenu.IsChecked = true; guideMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); viewMenu.IsOpen = false;
            Check(!Find<Expander>(view).Single(e => Equals(e.Header, "位置与尺寸")).IsExpanded && !Find<Expander>(view).Single(e => Equals(e.Header, "更多设置")).IsExpanded, "Geometry and infrequent settings are collapsed by default");
            var rail = Find<ListBox>(view).Single(l => l.ItemsSource == editor.MaterialChoices);
            Check(editor.MaterialChoices.Select(m => m.Name).SequenceEqual(new[] { "图片", "时钟", "插件", "文字", "形状" }), "Image materials have one visible entry rather than duplicate source tabs");
            Check(editor.ResolveMaterialChoice("壁纸") == editor.MaterialChoices[0] && editor.ResolveMaterialChoice("我的素材") == editor.MaterialChoices[0], "Both legacy material workspace names map to the unified image entry");
            editor.SelectedMaterial = editor.ResolveMaterialChoice("图片");
            Check(editor.ShowAssets && Find<Button>(view).Any(b => Equals(b.Content, "选择背景图片")), "Image browsing and explicit background selection remain available");
            Check(rail.ActualWidth <= 56 && ((ColumnDefinition)view.FindName("LeftColumn")).Width.Value == 280, "Reference editor uses a narrow tool rail beside a separate material panel");
            Check(!((ListBox)shell.FindName("SidebarNavigation")).IsVisible, "Global navigation releases its column while editing");
            editor.LeftTabIndex = 1; rail.SelectedIndex = 3; shell.UpdateLayout();
            Check(editor.LeftTabIndex == 0 && editor.SelectedMaterial.Name == "文字", "Selecting a tool opens its real material panel from the layer list");
            Find<Button>(view).Single(b => Equals(b.ToolTip, "图层")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(editor.LeftTabIndex == 1, "Layer rail entry opens existing hierarchical layers");
            editor.SelectLayers([]);
            Check(editor.NoSelection && editor.PropertyTitle == "画布", "Empty selection displays canvas properties");
            var x = editor.Project.ActiveLayout.Layers[0].X; editor.CanvasWidth = 2000; editor.CanvasHeight = 500; editor.CanvasBackground = "#202C43";
            Check(editor.Scene.PixelWidth == 2000 && editor.Scene.PixelHeight == 500 && editor.Project.ActiveLayout.Layers[0].X == x, "Canvas properties update actual scene dimensions without stretching layers");
            editor.CanvasWidth = 100;
            Check(editor.CanvasWidth == 2000, "Canvas size edit cannot silently change layout orientation");
            var canvas = (ThemeCanvas)view.FindName("Canvas"); var zoom = (Slider)view.FindName("ZoomSlider");
            zoom.Value = 2;
            Find<Button>(view).Single(b => Equals(b.ToolTip, "折叠素材和图层")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80); shell.UpdateLayout();
            Check(zoom.Value == 1 && ((FrameworkElement)view.FindName("LeftPanel")).Visibility == Visibility.Collapsed && canvas.ActualWidth > 500, "Panel collapse resets zoom and fits canvas into newly available area");
            Find<Button>(view).Single(b => Equals(b.ToolTip, "折叠素材和图层")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80);
            editor.Add(ThemeElementKind.Text); await editor.PendingOperation; editor.Text = "制作中的主题"; editor.X = 140; editor.Y = 280; editor.Width = 520; editor.Height = 100; editor.FontSize = 60;
            editor.LeftTabIndex = 1; shell.UpdateLayout();
            Check(tabs.SelectedIndex == 1 && Find<ListBox>(view).Any(l => l.Name == "LayerList" && l.IsVisible), "Layer tab gives the full list its own visible workspace");
            var actions = (Border)view.FindName("SelectionActions");
            Check(actions.IsVisible && actions.Margin.Left >= 0 && actions.Margin.Top >= 0, "Selection actions are visible and clamped inside the canvas workspace");
            var clock = editor.Project.ActiveLayout.Layers[0]; var text = editor.SelectedLayer!;
            editor.SelectLayers([clock.Id, text.Id]);
            Check(editor.SupportsSharedFont && editor.SupportsSharedColor && !editor.IsClock && !editor.IsImage, "Text multi-selection exposes only shared editable style controls");
            editor.Add(ThemeElementKind.Rectangle); await editor.PendingOperation; var shape = editor.SelectedLayer!; shape.X = 1600; shape.Y = 250; shape.Width = 220; shape.Height = 100; shape.Color = "#4C85F6"; editor.SelectLayers([clock.Id, shape.Id]);
            Check(!editor.SupportsSharedFont && editor.SupportsSharedColor, "Mixed text and shape selection hides font controls while retaining shared color");
            editor.SelectedLayer = text;
            var imagePath = Path.Combine(directory, "layer.png"); var pixels = new byte[32 * 8 * 4]; for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 220; pixels[i + 1] = 160; pixels[i + 2] = 60; pixels[i + 3] = 255; }
            var bitmap = BitmapSource.Create(32, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 128); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(imagePath)) encoder.Save(stream);
            editor.Add(ThemeElementKind.Image); await editor.PendingOperation; editor.SelectedLayer!.Path = imagePath; var image = editor.SelectedLayer; image.X = 1600; image.Y = 420; image.Width = 220; image.Height = 55;
            editor.SelectLayers([text.Id, image.Id]);
            Check(!editor.SupportsSharedFont && !editor.SupportsSharedColor, "Image and text selection hides inapplicable shared font and color fields");
            Check(editor.LayerRows.Single(r => r.Layer.Id == image.Id).PreviewUri?.LocalPath == imagePath, "Layer thumbnail references actual local material without a separate asset database");
            await editor.OpenProjectAsync(editor.Project.Clone()); editor.ProjectName = "可编辑主题示例"; editor.SelectedLayer = text; editor.LayerName = "标题"; editor.LeftTabIndex = 1; await Task.Delay(80); shell.UpdateLayout(); Capture(shell, "creator-usability-layers-1320.png");
            shell.Width = 1100; shell.Height = 740; await Task.Delay(80); shell.UpdateLayout(); Capture(shell, "creator-usability-layers-1100.png");
            foreach (var scale in new[] { 1d, 1.25, 1.5 }) CaptureDpi(shell, "landscape", scale);
            editor.Orientation = ThemeOrientation.Portrait; await editor.PendingOperation; await Task.Delay(80); shell.UpdateLayout();
            Check(canvas.ActualHeight > canvas.ActualWidth && editor.Scene.PixelWidth < editor.Scene.PixelHeight, "Compact portrait canvas uses vertical scene geometry without rotating editor panels");
            foreach (var scale in new[] { 1d, 1.25, 1.5 }) CaptureDpi(shell, "portrait", scale);
            editor.Orientation = ThemeOrientation.Landscape; await editor.PendingOperation; await Task.Delay(80); shell.UpdateLayout();
            var gaps = ThemeMeasurements.Nearest(new(100, 100, 100, 100), [new() { X = 250, Y = 100, Width = 80, Height = 100 }, new() { X = 100, Y = 220, Width = 100, Height = 60 }, new() { X = 205, Y = 100, Width = 20, Height = 20, Visible = false }]);
            Check(gaps.Count == 2 && gaps.Any(g => g.Distance == 50) && gaps.Any(g => g.Distance == 20), "Spacing feedback measures actual nearest visible neighbor gaps in design units");
            Check(ThemeMeasurements.Nearest(new(100, 100, 100, 100), [new() { X = 150, Y = 150, Width = 100, Height = 100 }]).Count == 0, "Overlapping elements do not produce misleading positive spacing labels");
            editor.ConfirmLeave = _ => CreatorLeaveChoice.Cancel; var id = editor.Project.Id;
            editor.BackCommand.Execute(null); await editor.PendingOperation;
            Check(editor.IsEditor && editor.IsDirty && editor.Project.Id == id, "Canceling leave retains editor source dirty state and selection");
            await editor.PersistDraftAsync(); editor.ConfirmLeave = _ => CreatorLeaveChoice.Discard;
            editor.BackCommand.Execute(null); await editor.PendingOperation;
            Check(editor.IsHome && !editor.IsDirty && (await store.ListAsync()).Count == 0 && (await store.ListDraftsAsync()).Count == 0, "Explicit discard removes recovery draft without creating or replacing a formal project");
            editor.TemplateCommand.Execute(null); await editor.PendingOperation; editor.ProjectName = "已保存的真实主题";
            editor.ConfirmLeave = _ => CreatorLeaveChoice.Save; editor.BackCommand.Execute(null); await editor.PendingOperation;
            Check(editor.IsHome && !editor.IsDirty && editor.Projects.Single().Name == "已保存的真实主题" && editor.RecentProjects.Single().LayoutCount == 2, "Save before leaving writes formal source and refreshes real recent cards and layout counts");
            var cover = await AsyncPreviewImage.LoadAsync(new Uri(store.CoverPath(editor.Projects.Single().Project.Id)));
            var coverPixels = new byte[cover!.PixelWidth * cover.PixelHeight * 4]; new FormatConvertedBitmap(cover, PixelFormats.Bgra32, null, 0).CopyPixels(coverPixels, cover.PixelWidth * 4, 0);
            Check(Enumerable.Range(0, coverPixels.Length / 4).Any(i => coverPixels[i * 4 + 2] > 180 && coverPixels[i * 4 + 3] > 0), "Saved cover contains rendered live-theme text rather than a blank placeholder");
            await Task.Delay(120); shell.UpdateLayout(); Capture(shell, "creator-usability-home.png");
            Check(((ListBox)shell.FindName("SidebarNavigation")).ActualWidth == 68, "Returning home restores the normal application sidebar");
            editor.OpenCommand.Execute(editor.Projects.Single()); await editor.PendingOperation;
            Find<Button>(view).Single(b => Equals(b.Content, "完成并使用") && b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(editor.IsPreview, "Unified preview falls back to source preview when no managed device exists");
            editor.EditCommand.Execute(null); editor.Add(ThemeElementKind.Text); await editor.PendingOperation;
            editor.ConfirmLeave = _ => CreatorLeaveChoice.Cancel;
            try { await editor.PrepareForCloseAsync(); throw new Exception("close unexpectedly succeeded"); } catch (OperationCanceledException) { }
            var time = editor.Scene.CurrentTime; await Task.Delay(1100);
            Check(editor.Scene.CurrentTime > time && editor.IsDirty, "Canceling application close leaves live clock and editable work running");
            var prompt = new CreatorSavePrompt("离线测试项目") { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; prompt.Show(); prompt.UpdateLayout();
            Check(Find<Button>(prompt).Select(b => b.Content).Contains("放弃修改") && prompt.Choice == CreatorLeaveChoice.Cancel, "Save prompt offers explicit discard and defaults to cancel on window dismissal"); Capture(prompt, "creator-usability-save-prompt.png"); prompt.Close();
            editor.ConfirmLeave = _ => CreatorLeaveChoice.Save; await editor.PrepareForCloseAsync();
            Check(!editor.IsDirty && (await store.ListAsync()).Count == 1, "Confirmed application close saves through existing source store");
            shell.Close();
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var c = VisualTreeHelper.GetChild(parent, i); if (c is T t) yield return t; foreach (var t2 in Find<T>(c)) yield return t2; } }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream); }
    private static void CaptureDpi(FrameworkElement view, string orientation, double scale)
    {
        // Rasterization check only: this does not change the monitor's native DPI or certify Per-Monitor V2 behavior.
        var width = (int)Math.Ceiling(view.ActualWidth * scale); var height = (int)Math.Ceiling(view.ActualHeight * scale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, $"creator-acceptance-{orientation}-{scale * 100:0}.png")); encoder.Save(stream);
        var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
        Check(bitmap.PixelWidth == width && bitmap.PixelHeight == height && Enumerable.Range(0, width * height).Any(i => pixels[i * 4 + 3] > 0), $"{orientation} editor rasterizes real WPF content at {scale:P0} without changing system DPI");
    }
    private sealed class Settings : ISettingsService { public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings()); public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true); }
    private sealed class Displays : IDisplayManager { public event EventHandler? DisplaysChanged { add { } remove { } } public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]); public void Dispose() { } }
}
