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
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using System.Windows.Threading;

namespace DeskDisplay.DisplayTests;
internal static class ThemeQuickUseTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-quick-use-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var initial = new AppSettings(); var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); clock.UpdateTime(DateTimeOffset.Now); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "library"));
            main.SetLibrary(new LibraryViewModel(main, catalog, logger)); await main.Library!.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes"));
            var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); await creator.InitializeAsync();
            await creator.OpenProjectAsync(new ThemeProject { Name = "可快速使用的真实主题" });
            creator.Add(ThemeElementKind.DigitalClock); await creator.PendingOperation; creator.ShowSeconds = true;
            await creator.SaveAsync(); var saved = creator.Project.Id;
            Check(creator.Projects.Count == 1 && main.Library.SavedThemes.Single().Project.Id == saved, "Saving creator source immediately exposes the same project ID in resource library");
            Check(main.HomeRecentEntries.Count == 0, "Saving or browsing alone never creates a usage record");
            creator.Add(ThemeElementKind.Text); creator.Text = "未保存的编辑";
            var editingBytes = JsonSerializer.Serialize(creator.Project); var dirty = creator.IsDirty;
            var source = await creator.ReadSavedProjectAsync(saved);
            Check(source.ActiveLayout.Layers.Count == 1 && creator.Project.ActiveLayout.Layers.Count == 2, "Quick use reads saved source rather than silently using dirty editor changes");
            source.Name = "快照修改";
            Check((await creator.ReadSavedProjectAsync(saved)).Name != source.Name && JsonSerializer.Serialize(creator.Project) == editingBytes && creator.IsDirty == dirty, "Quick-use snapshots leave saved source, editor and undo state unchanged");
            await creator.RecordThemeUseAsync(saved);
            Check(main.HomeRecentEntries.Single().Source is ThemeCard t && t.Project.Id == saved, "Accepted theme use notifies home recent list without navigation or editor replacement");
            var restored = new ThemeProjectStore(Path.Combine(root, "themes"));
            Check((await restored.ReadUsageAsync()).Single().LastUsedAt is not null, "Theme use history persists across store instances");
            await creator.ToggleThemeFavoriteAsync(creator.Projects.Single());
            main.Library.SelectedResourceType = "主题"; main.Library.SelectedResourceScope = "收藏";
            Check(main.Library.SavedThemes.Count == 1 && (await restored.ReadUsageAsync()).Single().Favorite, "Theme favorites use persistent sidecar metadata and actual library scope");
            main.Library.Search = "missing"; Check(main.Library.SavedThemes.Count == 0, "Theme name search filters real saved projects");
            main.Library.Search = ""; main.Library.SelectedResourceScope = "已购买";
            Check(main.Library.SavedThemes.Count == 0, "Local creations are never labeled as purchased resources");
            creator.ShowSavedThemes();
            Check(main.SelectedPage == AppPage.Library && main.Library.SelectedResourceType == "主题" && main.Library.SavedThemes.Count == 1, "Saved-theme shortcut opens resource library with explicit theme filter");
            Check(JsonSerializer.Serialize(creator.Project) == editingBytes && creator.IsDirty, "Library navigation retains unsaved editor state");
            for (var i = 0; i < 13; i++) await store.SaveAsync(new ThemeProject { Name = "分页主题 " + i, ModifiedAt = DateTimeOffset.Now.AddMinutes(i) });
            await creator.RefreshProjectsAsync();
            Check(main.Library.SavedThemes.Count == 14 && main.Library.VisibleThemes.Count == 12 && main.Library.ThemePageCount == 2, "Theme cards are bounded to twelve per page without copying assets into wallpaper catalog");
            main.Library.ChangeThemePage(1); Check(main.Library.VisibleThemes.Count == 2, "Saved theme pagination reaches remaining projects");
            main.Library.SelectedResourceScope = "使用历史";
            Check(main.Library.SavedThemes.Single().Project.Id == saved && main.Library.ThemePage == 1, "History scope includes only used existing themes and resets pagination");
            var other = creator.Projects.First(p => p.Project.Id != saved).Project.Id;
            await store.UpdateUsageAsync(other, DateTimeOffset.Now.AddMinutes(1)); await creator.RefreshProjectsAsync();
            Check(main.HomeRecentEntries.First().Source is ThemeCard first && first.Project.Id == other, "Home orders theme usage by use time instead of edit time");
            await store.DeleteAsync(other); await creator.RefreshProjectsAsync();
            Check(main.HomeRecentEntries.All(e => e.Source is not ThemeCard c || c.Project.Id != other), "Deleted projects are excluded from history despite retained sidecar entry");
            var view = new LibraryView { DataContext = main, Width = 1100, Height = 800 }; view.Measure(new(1100, 800)); view.Arrange(new(0, 0, 1100, 800)); view.UpdateLayout();
            Check(Find<Button>(view).Any(b => Equals(b.Content, "使用主题")) && Find<Button>(view).Any(b => Equals(b.Content, "编辑")), "Resource library creates real theme-use and edit controls from saved projects");
            Capture(view, "theme-quick-use-library.png");
            var home = new HomeView { DataContext = main, Width = 1100, Height = 1500 }; home.Measure(new(1100, 1500)); home.Arrange(new(0, 0, 1100, 1500)); home.UpdateLayout();
            Check(Find<Button>(home).Any(b => Equals(b.Content, "使用主题")), "Home recent theme exposes an actual use action"); Capture(home, "theme-quick-use-home.png");
            Check(Find<Button>(view).Where(b=>Equals(b.Content,"使用主题")).All(b=>!b.IsVisible),"Saved-theme use actions are hidden before hover");
            var registry=new DeviceRegistry();var descriptor=new DeviceDescriptor("quick-D92","D92",DeviceTransport.UsbHid,new("test","quick-serial",IdentityConfidence.VerifiedStable),new("quick-endpoint","offline",1),true);
            registry.Replace(DeviceTransport.UsbHid,[descriptor]);
            await using var sessions=new OutputSessionManager(registry,new InMemoryDeviceLeaseProvider(),(_,_)=>ValueTask.FromResult<IOutputSessionDriver>(new QuickDriver()));
            using var managed=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(root,"device-prefs.json")),()=>[],()=>[],_=>Task.CompletedTask,logger);
            var previewFactory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,manager,logger);
            managed.PreviewFactory=previewFactory.CreatePreviewAsync;managed.HomeOutputChanged=main.SetHomeOutputScene;
            devices.AttachManaged(managed);await managed.InitializeAsync();var target=managed.Cards.Single();target.Width="1920";target.Height="462";target.Confirmed=true;
            var savedCard=creator.Projects.Single(c=>c.Project.Id==saved);
            var useButton = Find<Button>(view).Single(b => Equals(b.Content, "使用主题") && b.DataContext is ThemeCard c && c.Project.Id == saved);
            useButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0; attempt < 100 && !useButton.IsEnabled; attempt++) await Task.Delay(20);
            Check(useButton.IsEnabled && main.QuickDevice == target, "Library use button selects the sole confirmed device");
            main.SelectedPage = AppPage.Home;
            var shell = new MainWindow { DataContext = main, Width = 1440, Height = 1000 };
            var surface = (FrameworkElement)shell.Content;
            surface.DataContext = main;
            surface.Measure(new(1440, 1000));
            surface.Arrange(new(0, 0, 1440, 1000));
            surface.UpdateLayout();
            Check(main.NowPlaying.Contains(savedCard.Name) && target.AppliedTheme == savedCard.Name,
                "Home shows the content accepted by the device");
            var footer = Find<ItemsControl>(surface).Single(control => control.Name == "DeviceOutputBar" && ReferenceEquals(control.ItemsSource, managed.Cards));
            var deviceTile = (FrameworkElement)footer.ItemTemplate.LoadContent();
            deviceTile.DataContext = target;
            deviceTile.Measure(new(600, 200));
            deviceTile.Arrange(new(0, 0, 600, 200));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(Find<TextBlock>(deviceTile).Any(label => label.Text == savedCard.Name),
                "Footer device tile shows the accepted content name");
            Capture(surface, "v63-wpf-home.png");
            var firstSession=sessions.Snapshot.Single().Snapshot.SessionId;
            Check(target.IsActive && main.HomeScene is ThemeScene && (Application.Current is null || !Application.Current.Windows.OfType<CreatorDevicePreview>().Any()),"Direct saved-theme use starts D92 output and updates home without opening a preview window");
            await SavedThemeUse.ApplyDirectAsync(main,savedCard);
            Check(sessions.Snapshot.Single().Snapshot.SessionId!=firstSession && JsonSerializer.Serialize(creator.Project)==editingBytes && creator.IsDirty,"Direct theme use switches owned output without saving or replacing the dirty editor");
            target.Confirmed=false;
            try { await SavedThemeUse.ApplyDirectAsync(main,savedCard);throw new Exception("Unconfirmed target accepted"); }
            catch(InvalidOperationException ex) { Check(ex.Message.Contains("确认"),"Direct theme use retains first-use device confirmation protection"); }
            var second=descriptor with {DeviceId="quick-second",Identity=descriptor.Identity with {SerialNumber="quick-other"},Endpoint=new("second","offline-second",1)};
            registry.Replace(DeviceTransport.UsbHid,[descriptor,second]);managed.Synchronize();
            try { await SavedThemeUse.ApplyDirectAsync(main,savedCard);throw new Exception("Multiple targets guessed"); }
            catch(InvalidOperationException ex) { Check(ex.Message.Contains("多台"),"Direct use does not guess between multiple connected D92 devices"); }
            await sessions.StopAsync(descriptor.DeviceId);
            var usagePath = Path.Combine(root, "themes", "usage.json"); await File.WriteAllTextAsync(usagePath, "broken");
            try { await store.UpdateUsageAsync(saved, favorite: false); throw new Exception("Broken metadata replaced"); } catch (JsonException) { }
            Check(await File.ReadAllTextAsync(usagePath) == "broken", "Unreadable usage metadata is preserved rather than replaced with defaults");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Capture(FrameworkElement view, string filename)
    {
        var bounds = new Rect(0, 0, view.ActualWidth, view.ActualHeight);
        var surface = new DrawingVisual(); using (var context = surface.RenderOpen()) { context.DrawRectangle(new SolidColorBrush(Color.FromRgb(244, 247, 251)), null, bounds); context.DrawRectangle(new VisualBrush(view), null, bounds); }
        var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); Directory.CreateDirectory("artifacts"); using var file = File.Create(Path.Combine("artifacts", filename)); encoder.Save(file);
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var nested in Find<T>(child)) yield return nested; } }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private sealed class QuickDriver : IOutputSessionDriver
    {
        public async Task RunAsync(Action<OutputActualState> report,CancellationToken ct) { report(OutputActualState.Streaming);await Task.Delay(Timeout.InfiniteTimeSpan,ct); }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
