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
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class TemplateLibraryTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-templates-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            foreach (var template in ThemeTemplates.All)
            {
                var a = template.Create(); var b = template.Create();
                Check(a.Id != b.Id && a.AllLayouts.SelectMany(l => l.Layers).All(l => !b.AllLayouts.SelectMany(p => p.Layers).Any(other => other.Id == l.Id)), $"{template.Key} creates fresh project and editable layer identities");
                Check(a.AllLayouts.All(l => l.Layers.All(layer => layer.X >= 0 && layer.Y >= 0 && layer.X + layer.Width <= l.Width + .001 && layer.Y + layer.Height <= l.Height + .001)), $"{template.Key} keeps both independent layouts inside logical canvas");
            }
            using var display = new Displays(); var settings = new Settings();
            using var device = new DevicesViewModel(display, new TargetDisplayService(new(), settings, logger), logger);
            using var main = new MainViewModel(device, new ClockScene());
            var plugin = new PluginManagerViewModel(main, settings, new(), logger); main.SetPlugins(plugin);
            plugin.CameraEnabled = false;
            var store = new ThemeProjectStore(directory); var editor = new CreatorViewModel(main, logger, store); main.SetCreator(editor); main.SelectedPage = AppPage.Creator;
            await editor.InitializeAsync(); editor.BrowseTemplatesCommand.Execute(null); await editor.PendingTemplate;
            Check(editor.TemplateCards.Count == ThemeTemplates.All.Count && editor.TemplateCards.All(c => c.Cover is { IsFrozen: true }), "Every built-in card has a frozen cover rendered from editable scene data");
            var digital = editor.TemplateCards.Where(c => c.Template.Style.StartsWith("digital:", StringComparison.Ordinal)).ToArray();
            Check(digital.Length == 20 && digital.Select(c => c.Name).Distinct().Count() == 10, "Ten new clock designs each expose landscape and portrait variants");
            foreach (var card in digital)
            {
                var project = card.Template.Create();
                var restored = JsonSerializer.Deserialize<ThemeProject>(JsonSerializer.Serialize(project))!; restored.Validate();
                foreach (var layout in restored.AllLayouts)
                {
                    var layer = layout.Layers.Single(l => l.Kind == ThemeElementKind.DigitalClock);
                    var size = DeskDisplay.Display.Views.ThemeTextMetrics.MeasureClock(layer);
                    Check(size.Width <= layer.Width && size.Height <= layer.Height, $"{card.Template.Key} clock frame fits the renderer's maximum digit advances ({size.Width}x{size.Height} <= {layer.Width}x{layer.Height})");
                    Check(layer.GetText(DateTimeOffset.Now) != layer.GetText(DateTimeOffset.Now.AddMinutes(1)) && layout.Layers.All(l => l.Kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper or ThemeElementKind.CameraClock)), $"{card.Template.Key} restores live editable time without external image or plugin dependencies");
                }
            }
            var sheet = new Canvas { Width = 900, Height = 2200, Background = Brushes.White };
            var designs = digital.GroupBy(c => c.Name).ToArray();
            for (var i = 0; i < designs.Length; i++)
            {
                var label = new TextBlock { Text = designs[i].Key, FontSize = 18, Foreground = Brushes.Black }; Canvas.SetLeft(label, 10); Canvas.SetTop(label, i * 220); sheet.Children.Add(label);
                foreach (var card in designs[i])
                {
                    var portrait = card.Template.Orientation == ThemeOrientation.Portrait;
                    var image = new Image { Source = card.Cover, Width = portrait ? 45 : 720, Height = 180, Stretch = Stretch.Uniform };
                    Canvas.SetLeft(image, portrait ? 760 : 10); Canvas.SetTop(image, i * 220 + 30); sheet.Children.Add(image);
                }
            }
            sheet.Measure(new Size(900, 2200)); sheet.Arrange(new Rect(0, 0, 900, 2200)); sheet.UpdateLayout(); Capture(sheet, "digital-clock-templates.png");
            var clock = editor.TemplateScene.CurrentTime; await Task.Delay(1100);
            Check(editor.TemplateScene.CurrentTime > clock, "Large template preview uses live system time rather than a fixed clock image");
            var window = new MainWindow { DataContext = main, Width = 1320, Height = 900, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 }; window.Show(); await Task.Delay(80); window.UpdateLayout();
            Capture(window, "template-library-clock.png");
            editor.SelectedTemplate = editor.Templates.First(t => t.Style == "wallpaper" && t.Orientation == ThemeOrientation.Landscape);
            var imagePath = Path.Combine(directory, "wallpaper.png");
            var bitmap = BitmapSource.Create(32, 8, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 32 * 8).SelectMany(_ => new byte[] { 210, 150, 80, 255 }).ToArray(), 128); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(imagePath)) encoder.Save(stream);
            editor.SetTemplateBackgroundFile(imagePath); await editor.PendingTemplate;
            Check(editor.TemplateAvailable && editor.TemplateScene.Children.Values.OfType<ImageScene>().Single().Path == imagePath && editor.TemplateScene.Project.AllLayouts.All(l => l.Layers.Any(layer => layer.Kind == ThemeElementKind.Wallpaper && layer.Path == imagePath)), "Selected local wallpaper is genuinely loaded and referenced by both editable layouts");
            window.UpdateLayout(); Capture(window, "template-library-wallpaper.png");
            editor.UseTemplateCommand.Execute(null); await editor.PendingOperation;
            var firstId = editor.Project.Id; editor.SelectedLayer = editor.Project.ActiveLayout.Layers.First(l => l.Kind == ThemeElementKind.DigitalClock); editor.X = 133;
            await editor.SaveAsync(); var saved = (await store.ListAsync()).Single();
            Check(saved.AllLayouts.SelectMany(l => l.Layers).Any(l => l.Kind == ThemeElementKind.Wallpaper && l.Path == imagePath) && saved.Landscape.Layers.Single(l => l.Kind == ThemeElementKind.DigitalClock).X == 133, "Wallpaper template remains editable and preserves background references through formal save");
            var sourceBytes = JsonSerializer.Serialize(saved); await editor.SaveCustomTemplateAsync();
            Check((await editor.CustomTemplatesAsync()).Count == 1 && JsonSerializer.Serialize(saved) == sourceBytes, "Existing custom template storage remains separate and preserves original source");
            editor.BrowseTemplatesCommand.Execute(null); await editor.PendingTemplate; editor.UseTemplateCommand.Execute(null); await editor.PendingOperation;
            Check(editor.Project.Id != firstId && editor.Project.Landscape.Layers.Single(l => l.Kind == ThemeElementKind.DigitalClock).X != 133, "Reusing built-in template creates a fresh independent copy without retaining prior edits");
            editor.BrowseTemplatesCommand.Execute(null); editor.SetTemplateBackgroundFile(Path.Combine(directory, "missing.png")); await editor.PendingTemplate;
            Check(!editor.TemplateAvailable && !editor.UseTemplateCommand.CanExecute(null) && editor.TemplateStatus.Contains("失败"), "Missing template background is reported and blocks creation instead of a fake preview");
            editor.ClearTemplateBackgroundCommand.Execute(null); await editor.PendingTemplate;
            Check(editor.TemplateAvailable && !editor.TemplateScene.Project.ActiveLayout.Layers.Any(l => l.Kind == ThemeElementKind.Wallpaper), "Explicit solid background clears failed media dependency");
            editor.SelectedTemplate = editor.Templates.First(t => t.RequiresCamera && t.Orientation == ThemeOrientation.Portrait);
            Check(!editor.TemplateAvailable && !editor.ShowTemplateScene && !editor.UseTemplateCommand.CanExecute(null) && editor.TemplateStatus.Contains("启用"), "Disabled camera plugin cannot be presented as a working template");
            window.UpdateLayout(); Capture(window, "template-library-camera-disabled.png");
            plugin.CameraEnabled = true; await editor.PendingTemplate;
            Check(editor.TemplateAvailable && editor.TemplateScene.Children.Values.Contains(plugin.CameraScene), "Enabling installed camera plugin attaches the actual running component to template preview");
            var cameraTime = plugin.CameraScene.CurrentTime; await Task.Delay(1100);
            Check(plugin.CameraScene.CurrentTime > cameraTime, "Camera plugin advances real time while visible inside the template picker");
            window.Width = 1100; window.Height = 740; await Task.Delay(80); window.UpdateLayout(); Capture(window, "template-library-camera-portrait.png");
            editor.UseTemplateCommand.Execute(null); await editor.PendingOperation;
            Check(editor.IsPortrait && editor.Project.ActiveLayout.Layers.Any(l => l.Kind == ThemeElementKind.CameraClock) && editor.Scene.Children.Values.Contains(plugin.CameraScene), "Camera template creates real editable plugin layer in independent portrait layout");
            editor.BrowseTemplatesCommand.Execute(null); plugin.CameraEnabled = false;
            Check(!editor.TemplateAvailable && !editor.TemplateScene.Children.Values.Contains(plugin.CameraScene), "Stopping plugin immediately removes its template preview and disables creation");
            Check((await editor.CustomTemplatesAsync()).Count == 1, "Built-in library does not replace or delete user custom templates");
            window.Close(); await editor.PrepareForCloseAsync();
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static void Capture(FrameworkElement view, string name) { var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream); }
    private sealed class Settings : ISettingsService { public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings()); public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true); }
    private sealed class Displays : IDisplayManager { public event EventHandler? DisplaysChanged { add { } remove { } } public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]); public void Dispose() { } }
}

