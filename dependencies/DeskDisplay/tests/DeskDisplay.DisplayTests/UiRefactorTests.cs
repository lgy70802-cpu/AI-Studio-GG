using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class UiRefactorTests
{
    public static async Task RunAsync()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var root = Path.Combine(Path.GetTempPath(), "D92-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            var initial = new AppSettings();
            using var manager = new WorkspaceTests.Manager { Displays = [] };
            using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene();
            using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "Library"));
            main.SetLibrary(new LibraryViewModel(main, catalog, logger));
            await main.Library!.InitializeAsync();
            main.SetCreator(new CreatorViewModel(main, logger, new ThemeProjectStore(Path.Combine(root, "Themes"))));
            await main.Creator!.InitializeAsync();
            var window = new MainWindow { DataContext = main, ShowActivated = false, Title = "D92 UI isolated review" };
            if (Environment.GetEnvironmentVariable("DESKDISPLAY_UI_REVIEW") != "1") { window.Left = -30000; window.Top = -30000; window.ShowInTaskbar = false; }
            try
            {
                window.Show();
                foreach (var (width, height) in new[] { (960d, 640d), (1320d, 900d), (1440d, 1000d) })
                {
                    window.Width = width; window.Height = height;
                    main.SelectedPage = AppPage.Home; await Idle(window);
                    var home = Descendants<HomeView>(window).Single();
                    var controls = Descendants<Button>(home).Where(b => b.IsVisible && b.Content is string s && (s.Contains("设备管理") || s.Contains("轮播管理"))).ToArray();
                    Check(controls.Length == 2 && controls.All(b => FitsWidth(b, home)), $"Home device actions remain inside the viewport at {width}x{height}");
                    main.SelectedPage = AppPage.Settings; await Idle(window);
                    var groups = Descendants<TabControl>(window).Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "设置分组");
                    foreach (var label in new[] { "常规", "资源与备份", "版本与更新", "运行诊断" })
                    {
                        groups.SelectedItem = groups.Items.OfType<TabItem>().Single(t => Equals(t.Header, label)); await Idle(window);
                        Check(Descendants<ScrollViewer>(groups).Any(s => s.IsVisible && s.ViewportHeight > 0), $"Settings group {label} retains a scrollable content area at {width}x{height}");
                    }
                    main.SelectedPage = AppPage.Creator;
                    await main.Creator.OpenProjectAsync(new ThemeProject { Name = "UI layout validation" }); await Idle(window);
                    var editor = Descendants<CreatorView>(window).Single();
                    var canvas = (ThemeCanvas)editor.FindName("Canvas");
                    Check(canvas.ActualWidth > 100 && canvas.ActualHeight > 20, $"Editable canvas remains usable with both panels at {width}x{height}");
                    Check(Descendants<GridSplitter>(editor).Count(s => s.IsVisible) == 2, "Editor exposes independent material and property panel resize controls");
                }
                main.SelectedPage = AppPage.Home; await Idle(window);
                var focusButton = Descendants<Button>(window).First(b => b.IsVisible && b.IsEnabled && Equals(b.Content, "设备管理"));
                window.Activate(); focusButton.Focus(); await Idle(window);
                Check(focusButton.IsKeyboardFocused && focusButton.Template.FindName("FocusRing", focusButton) is Border { Visibility: Visibility.Visible }, "Keyboard focus is visible on the actual home action");
                if (Environment.GetEnvironmentVariable("DESKDISPLAY_UI_REVIEW") == "1") await Task.Delay(TimeSpan.FromSeconds(120));
            }
            finally { window.DataContext = null; window.Close(); }
        }
        finally { Directory.Delete(root, true); }
    }
    private static Task Idle(DispatcherObject target) => target.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
    private static bool FitsWidth(FrameworkElement child, FrameworkElement viewport)
    {
        var point = child.TranslatePoint(new Point(), viewport);
        return point.X >= -1 && point.X + child.ActualWidth <= viewport.ActualWidth + 1;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
