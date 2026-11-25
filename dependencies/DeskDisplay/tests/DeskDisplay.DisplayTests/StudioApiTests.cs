using System.Net;
using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DeskDisplay.Library;
using Serilog;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskDisplay.DisplayTests;

internal static class StudioApiTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-studio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = Encoding.UTF8.GetBytes("verified-wallpaper-content");
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var id = Guid.NewGuid(); var downloads = 0;
            var handler = new Handler(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/catalog/options"))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"categories\":[{\"id\":\"nature\",\"name\":\"风光美景\",\"parentId\":null},{\"id\":\"mountain\",\"name\":\"山川湖泊\",\"parentId\":\"nature\"}],\"tags\":[{\"id\":\"calm\",\"name\":\"治愈\"}]}", Encoding.UTF8, "application/json") };
                if (request.RequestUri!.AbsolutePath.EndsWith("/resources"))
                {
                    var portrait = request.RequestUri.Query.Contains("orientation=portrait", StringComparison.Ordinal);
                    var width = portrait ? 462 : 1920;
                    var height = portrait ? 1920 : 462;
                    var json = $$"""{"items":[{"id":"{{id}}","name":"官方壁纸","description":"测试","type":"STATIC_WALLPAPER","version":"1.0.0","sha256":"{{sha}}","byteSize":{{bytes.Length}},"mimeType":"image/png","width":{{width}},"height":{{height}},"downloadUrl":"/api/v1/client/resources/{{id}}/download","previewUrl":"/api/v1/client/resources/{{id}}/preview","thumbnailUrl":"/api/v1/client/resources/{{id}}/thumbnail","minimumClientVersion":"0.0.1","supportedDevices":["D92"],"categories":[],"tags":[],"updatedAt":"2026-10-10T00:00:00Z"}],"total":1,"page":1,"pageSize":24}""";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }
                if (request.RequestUri.AbsolutePath.EndsWith("/thumbnail") || request.RequestUri.AbsolutePath.EndsWith("/preview"))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentLength = bytes.Length, ContentType = new("image/png") } } };
                downloads++; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentLength = bytes.Length, ContentType = new("image/png") } } };
            });
            using var client = new StudioApiClient(logger, new HttpClient(handler), root);
            var sessionStore = new StudioSessionStore(Path.Combine(root, "test.session"));
            sessionStore.Save("https://studio.example.test", "test-token");
            Check(sessionStore.Read("https://studio.example.test") == "test-token" && sessionStore.Read("https://other.example.test") is null,
                "Studio encrypted session never leaks to a different server");
            sessionStore.Clear("https://studio.example.test");
            var page = await client.GetResourcesAsync("http://127.0.0.1:3000");
            Check(page.Items.Count == 1 && page.Items[0].PreviewUri?.AbsolutePath.EndsWith("/preview") == true, "Studio client accepts a valid published static resource manifest");
            Check(page.Items[0].Width == 1920 && page.Items[0].Height == 462 && !page.Items[0].IsPortrait, "Studio client preserves resource dimensions and landscape orientation");
            var vip = page.Items[0] with { AccessPolicy = "VIP" };
            var paid = page.Items[0] with { AccessPolicy = "PURCHASE", PriceMinor = 990 };
            Check(!vip.HasDownloadEntitlement && vip.AccessLabel == "VIP" && paid.AccessLabel == "¥9.9", "Studio client exposes honest VIP and paid access states without granting local entitlement");
            var reports = new List<StudioDownloadProgress>();
            var path = await client.DownloadAsync("http://127.0.0.1:3000", page.Items[0], new Progress<StudioDownloadProgress>(reports.Add));
            Check(File.Exists(path) && await File.ReadAllBytesAsync(path) is var saved && saved.SequenceEqual(bytes), "Studio download is persisted only after size and SHA-256 verification");
            await Task.Delay(20);
            Check(page.Items[0].IsCompatible && reports.LastOrDefault()?.Percentage == 100, "Studio compatibility and byte progress are exposed to the client UI");
            Check(await client.DownloadAsync("http://127.0.0.1:3000", page.Items[0]) == path && downloads == 1, "Verified Studio cache avoids duplicate downloads");
            await File.WriteAllTextAsync(path, "corrupt");
            await client.DownloadAsync("http://127.0.0.1:3000", page.Items[0]);
            Check(downloads == 2, "Corrupt Studio cache is replaced from the server");
            await AssertThrowsAsync<InvalidDataException>(() => client.GetResourcesAsync("http://example.com"));
            Check(true, "Studio client rejects cleartext non-loopback servers");

            var bad = page.Items[0] with { Sha256 = new string('0', 64) };
            await AssertThrowsAsync<InvalidDataException>(() => client.DownloadAsync("http://127.0.0.1:3000", bad));
            Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "Failed Studio verification removes incomplete files");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                await AssertThrowsAsync<OperationCanceledException>(() => client.DownloadAsync("http://127.0.0.1:3000", page.Items[0] with { Version = "1.9.0" }, cancelled.Token));
                Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "Cancelled Studio download leaves no incomplete cache files");
            }
            await CaptureOnlinePageAsync(logger, root, handler);
            if (Environment.GetEnvironmentVariable("DESKDISPLAY_STUDIO_TEST_URL") is { Length: > 0 } liveServer)
                await VerifyLiveServerAsync(logger, root, liveServer);
            Console.WriteLine("All Studio API client tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static async Task VerifyLiveServerAsync(ILogger logger, string root, string server)
    {
        using var api = new StudioApiClient(logger, cacheDirectory: Path.Combine(root, "live-cache"));
        var landscape = await api.GetResourcesAsync(server);
        var portrait = await api.GetResourcesAsync(server, orientation: "portrait");
        Check(landscape.Items.Count > 0 && landscape.Items.All(item => !item.IsPortrait) && portrait.Items.Count > 0 && portrait.Items.All(item => item.IsPortrait),
            "Live Studio serves real landscape and portrait resources with correct dimensions");
        var portraitThumbnail = BitmapDecoder.Create(portrait.Items[0].ThumbnailUri!, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Check(portraitThumbnail.PixelHeight > portraitThumbnail.PixelWidth,
            "Live portrait thumbnails use a portrait image canvas without landscape padding");
        Check(landscape.Items.Any(item => item.AccessPolicy == "VIP") && landscape.Items.Any(item => item.AccessPolicy == "PURCHASE"),
            "Live Studio catalog exposes published free, VIP and paid resources");
        var resource = landscape.Items.First(item => item.AccessPolicy == "FREE");
        var path = await api.DownloadAsync(server, resource);
        var image = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Check(image.PixelWidth == resource.Width && image.PixelHeight == resource.Height, "Live downloaded image passes hash verification and matches manifest dimensions");
        var settings = new JsonSettingsService(logger, Path.Combine(root, "live-settings.json"));
        var initial = new AppSettings { StudioServerUrl = server };
        using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
        using var main = new MainViewModel(devices, new ClockScene());
        main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, (ClockScene)main.Scene, main.PreviewScene, logger));
        main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
        main.SetWorkspace(new WorkspaceViewModel(main, (ClockScene)main.Scene, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
        await main.Workspace!.RestoreAsync();
        var catalog = new WallpaperCatalog(logger, Path.Combine(root, "live-library"));
        var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
        await library.ImportCloudAsync(path, resource.Name, resource.Id, resource.Version, "验收测试", "测试", false);
        await library.ImportCloudAsync(path, resource.Name, resource.Id, resource.Version, "验收测试", "测试", false);
        var cloud = new CloudLibraryViewModel(main, settings, initial, logger, api); main.SetCloud(cloud);
        await cloud.InitializeAsync(); cloud.Selected = cloud.Items.First(item => item.Id == resource.Id);
        cloud.DownloadCommand.Execute(null); await cloud.PendingOperation;
        Check(cloud.DownloadTasks.First().State == DownloadTaskState.Completed, "Download manager records successful real API downloads");
        cloud.Selected = resource with { Version = "1.9.0", Sha256 = new string('0', 64) };
        cloud.DownloadCommand.Execute(null); await cloud.PendingOperation;
        Check(cloud.DownloadTasks.First().State == DownloadTaskState.Failed && cloud.DownloadTasks.First().CanRetry,
            "Download manager exposes verification failures with a retry action");
        main.SelectedPage = AppPage.Online;
        var window = new DeskDisplay.App.Views.MainWindow { DataContext = main };
        try
        {
            window.Show(); window.UpdateLayout(); await WaitForGalleryImagesAsync(window, cloud); Capture(window, "online-download-management.png");
            await VerifyReferenceLayoutAsync(window, cloud);
            cloud.DisplayOrientation = "竖屏超窄"; await cloud.PendingOperation; window.UpdateLayout(); await WaitForGalleryImagesAsync(window, cloud);
            Capture(window, "online-portrait-optimized.png");
            ((System.Windows.Controls.TextBox)window.FindName("OnlineSearchBox")).Text = "no-match-" + Guid.NewGuid().ToString("N"); cloud.SearchCommand.Execute(null); await cloud.PendingOperation;
            Check(cloud.ShowEmptyState && !cloud.ShowDemoItems, "Successful empty cloud filters show a real empty state without demo resources");
            window.UpdateLayout(); Capture(window, "online-empty-optimized.png");
            cloud.SaveServerCommand.Execute(null); await cloud.PendingOperation;
            Check(cloud.Search == "" && cloud.Categories.Count > 0 && cloud.Items.Count > 0,
                "Reconnecting a Studio server resets old catalog filters and reloads categories");
        }
        finally { window.Close(); }
        library.SelectedTab = "我的资源"; library.Select(library.VisibleItems.First());
        library.Selected!.Asset.Name = "保留本地名称"; library.Selected.Asset.Favorite = true;
        library.PlaylistCommand.Execute(null);
        var updatePath = Path.Combine(root, "replacement.png"); File.Copy(path, updatePath);
        await library.ImportCloudAsync(updatePath, resource.Name, resource.Id, "1.1.0", "验收测试", "测试", false);
        await library.PrepareForCloseAsync();
        var saved = await catalog.LoadAsync();
        Check(saved.Assets.Count == 1 && saved.Assets[0].CloudVersion == "1.1.0" && saved.Assets[0].Path == updatePath, "Live cloud download imports and local version replacement preserve one resource identity");
        Check(saved.Assets[0].Name == "保留本地名称" && saved.Assets[0].Favorite && saved.Playlist.Contains(saved.Assets[0].Id),
            "Cloud version replacement preserves local name, favorite and playlist identity");
        await VerifyLargeLibraryAsync(logger, root, path);
    }
    private static async Task VerifyLargeLibraryAsync(ILogger logger, string root, string source)
    {
        var directory = Path.Combine(root, "large-library"); Directory.CreateDirectory(directory);
        var document = new WallpaperLibrary();
        for (var index = 0; index < 500; index++)
        {
            var path = Path.Combine(directory, $"resource-{index}.png"); File.Copy(source, path);
            document.Assets.Add(new WallpaperAsset { Path = path, Name = $"压力测试 {index}", Width = 1920, Height = 462, AddedAt = DateTimeOffset.UnixEpoch.AddSeconds(index) });
        }
        var catalog = new WallpaperCatalog(logger, directory); await catalog.SaveAsync(document);
        var settings = new JsonSettingsService(logger, Path.Combine(directory, "settings.json")); var initial = new AppSettings();
        using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
        using var main = new MainViewModel(devices, new ClockScene());
        main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, (ClockScene)main.Scene, main.PreviewScene, logger));
        main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
        main.SetWorkspace(new WorkspaceViewModel(main, (ClockScene)main.Scene, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
        await main.Workspace!.RestoreAsync();
        var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library);
        var timer = System.Diagnostics.Stopwatch.StartNew(); await library.InitializeAsync(); library.SelectedTab = "我的资源";
        var firstScreen = library.VisibleItems.Count; library.LoadMore(); library.LoadMore();
        Check(firstScreen > 0 && library.VisibleItems.Count <= firstScreen * 3 && library.CanNextPage,
            "500-resource library limits rendered cards to three screens and enables the next page");
        for (var index = 0; index < 30; index++) { library.Search = "499"; library.Search = ""; }
        Check(timer.Elapsed < TimeSpan.FromSeconds(10), "500-resource initialization and repeated filters complete within the regression budget");
        Console.WriteLine($"LARGE LIBRARY: 500 resources, visible={library.VisibleItems.Count}, elapsedMs={timer.ElapsedMilliseconds}");
        await library.PrepareForCloseAsync();
    }
    private static async Task CaptureOnlinePageAsync(ILogger logger, string root, HttpMessageHandler handler)
    {
        var settings = new JsonSettingsService(logger, Path.Combine(root, "ui-settings.json"));
        var initial = new AppSettings { StudioServerUrl = "http://127.0.0.1:3000" };
        using var manager = new WorkspaceTests.Manager();
        using var output = new WorkspaceTests.Output();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
        using var main = new MainViewModel(devices, new ClockScene());
        using var api = new StudioApiClient(logger, new HttpClient(handler), Path.Combine(root, "ui-cache"));
        using var cloud = new CloudLibraryViewModel(main, settings, initial, logger, api);
        main.SetCloud(cloud); main.SelectedPage = AppPage.Online;
        using var accountApi = new StudioApiClient(logger, new HttpClient(handler), Path.Combine(root, "account-cache"));
        using var account = new StudioAccountViewModel(main, initial.StudioServerUrl, logger, accountApi);
        main.SetAccount(account);
        Check(!account.LoginCommand.CanExecute(null), "Studio login is disabled before credentials are supplied");
        account.Email = "user@example.test"; account.Password = "password123";
        Check(account.LoginCommand.CanExecute(null), "Studio login becomes available after credentials are entered");
        await cloud.InitializeAsync(); await cloud.PendingOperation;
        cloud.OpenDetailCommand.Execute(cloud.Items[0]);
        main.SelectedPage = AppPage.Library; main.SelectedPage = AppPage.Online;
        Check(cloud.IsDetails && cloud.Selected?.Id == cloud.Items[0].Id, "Online resource detail state survives navigation to another page and back");
        cloud.BackCommand.Execute(null);
        var window = new DeskDisplay.App.Views.MainWindow { DataContext = main };
        try
        {
            window.Show(); window.UpdateLayout(); await Task.Delay(100);
            Capture(window, "online-resources.png");
            window.Width = 1100; window.Height = 740; window.UpdateLayout();
            Capture(window, "online-resources-compact.png");
            cloud.DisplayOrientation = "竖屏超窄"; await cloud.PendingOperation;
            window.Width = 1440; window.Height = 900; window.UpdateLayout();
            Capture(window, "online-resources-portrait.png");
            main.SelectedPage = AppPage.Account; window.UpdateLayout();
            Capture(window, "studio-account.png");
        }
        finally { window.Close(); }
        Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "online-resources.png"))
              && File.Exists(Path.Combine(AppContext.BaseDirectory, "online-resources-compact.png")),
            "Online resource page renders at standard and compact window sizes");
        Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "online-resources-portrait.png")),
            "Online resource page renders the portrait resource layout");
    }
    private static async Task VerifyReferenceLayoutAsync(DeskDisplay.App.Views.MainWindow window, CloudLibraryViewModel cloud)
    {
        var view = FindVisual<DeskDisplay.App.Views.OnlineResourcesView>(window).Single();
        var search = (System.Windows.Controls.TextBox)window.FindName("OnlineSearchBox");
        var previous = (System.Windows.Controls.Button)window.FindName("OnlinePreviousPage"); var next = (System.Windows.Controls.Button)window.FindName("OnlineNextPage");
        Check(previous.TranslatePoint(new(), window).X < search.TranslatePoint(new(), window).X && search.TranslatePoint(new(), window).X < next.TranslatePoint(new(), window).X && search.TranslatePoint(new(), window).Y < 48, "Top search is positioned between the real previous and next resource-page commands");
        Check(ReferenceEquals(previous.Command, cloud.PreviousCommand) && ReferenceEquals(next.Command, cloud.NextCommand), "Top arrows reuse actual resource pagination rather than decorative navigation");
        var sort = (System.Windows.Controls.ComboBox)view.FindName("SortSelector"); var layout = (System.Windows.Controls.ComboBox)view.FindName("LayoutSelector");
        Check(sort.TranslatePoint(new(), view).X > view.ActualWidth / 2 && layout.TranslatePoint(new(), view).X > sort.TranslatePoint(new(), view).X, "Sort and layout selectors sit in the resource area upper right");
        var parent = cloud.PrimaryCategories.FirstOrDefault(c => cloud.Categories.Any(child => child.ParentId == c.Id));
        if (parent is not null)
        {
            cloud.SelectedPrimaryCategory = parent; await cloud.PendingOperation; await WaitForGalleryImagesAsync(window, cloud);
            var primary = (System.Windows.FrameworkElement)view.FindName("PrimaryCategoryRow"); var secondary = (System.Windows.FrameworkElement)view.FindName("SecondaryCategoryRow");
            Check(secondary.IsVisible && secondary.TranslatePoint(new(), view).Y - primary.TranslatePoint(new(), view).Y <= primary.ActualHeight + 3, "Secondary categories follow the selected primary category with no intervening filter section");
        }
        Capture(window, "online-reference-default.png");
        layout.IsDropDownOpen = true; await Task.Delay(80);
        var layoutPopup = (System.Windows.Controls.Primitives.Popup)layout.Template.FindName("PART_Popup", layout);
        Check(layoutPopup.IsOpen && FindVisual<System.Windows.Controls.ComboBoxItem>(layoutPopup.Child).Count() == 2, "Custom Soft UI layout dropdown exposes real selectable native ComboBox items");
        layout.IsDropDownOpen = false;
        layout.SelectedItem = "紧凑"; await cloud.PendingOperation; await WaitForGalleryImagesAsync(window, cloud);
        Check(cloud.ResourceColumns == 3 && FindVisual<System.Windows.Controls.Primitives.UniformGrid>(view).Any(grid => grid.IsVisible && grid.Columns == 3), "Actual layout selector switches landscape resources from two to three columns"); Capture(window, "online-reference-compact.png");
        var sidebar = (System.Windows.Controls.ListBox)window.FindName("SidebarNavigation"); var nav = sidebar.Items.OfType<NavigationItem>().Single(item => item.Page == AppPage.Online);
        window.OpenOnlineMenu((System.Windows.UIElement)sidebar.ItemContainerGenerator.ContainerFromItem(nav)); await Task.Delay(80);
        var popup = (System.Windows.Controls.Primitives.Popup)window.FindName("OnlineResourcePopup"); var buttons = FindVisual<System.Windows.Controls.Button>(popup.Child).Where(button => button.Tag is OnlineResourceKind).ToArray();
        Check(popup.IsOpen && buttons.Length == 4 && buttons.Select(button => (OnlineResourceKind)button.Tag).Distinct().Count() == 4, "Sidebar opens a real four-type secondary resource menu");
        CaptureElement((System.Windows.FrameworkElement)popup.Child, "online-reference-sidebar-menu.png");
        buttons.Single(button => (OnlineResourceKind)button.Tag == OnlineResourceKind.Clock).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); window.UpdateLayout();
        Check(!popup.IsOpen && cloud.ShowTypeUnavailable && !cloud.IsGallery && !cloud.SearchCommand.CanExecute(null), "Unimplemented online clock closes the menu and shows an honest unavailable page without wallpaper substitution"); Capture(window, "online-reference-unavailable.png");
        cloud.ResourceKind = OnlineResourceKind.Wallpaper; await cloud.PendingOperation; await WaitForGalleryImagesAsync(window, cloud);
        cloud.OpenDetailCommand.Execute(cloud.Items.First()); cloud.ResourceKind = OnlineResourceKind.Theme; cloud.ResourceKind = OnlineResourceKind.Wallpaper; await cloud.PendingOperation;
        Check(cloud.IsDetails && cloud.Selected is not null, "Resource type round trip retains the wallpaper detail state"); cloud.SearchCommand.Execute(null); await cloud.PendingOperation; Check(cloud.IsGallery, "Top search from resource details returns to the filtered resource gallery");
        cloud.LayoutMode = "舒适"; cloud.SelectedPrimaryCategory = null; await cloud.PendingOperation;
        cloud.DisplayOrientation = "竖屏超窄"; await cloud.PendingOperation; await WaitForGalleryImagesAsync(window, cloud);
        Check(cloud.ResourceColumns == 6 && cloud.Items.All(item => item.CardAspectRatio == .25), "Portrait layout retains one-to-four image frames with the default six columns");
        cloud.LayoutMode = "紧凑"; await cloud.PendingOperation; await WaitForGalleryImagesAsync(window, cloud); Check(cloud.ResourceColumns == 8, "Portrait compact mode switches independently to eight columns"); Capture(window, "online-reference-portrait.png");
        cloud.LayoutMode = "舒适"; cloud.DisplayOrientation = "横屏超宽"; await cloud.PendingOperation;
        window.Width = 1100; window.Height = 740; window.UpdateLayout(); await WaitForGalleryImagesAsync(window, cloud);
        var searchBounds = search.TransformToAncestor(window).TransformBounds(new System.Windows.Rect(search.RenderSize));
        Check(searchBounds.Left > 68 && searchBounds.Right < window.ActualWidth - 100 && layout.TranslatePoint(new(), view).X + layout.ActualWidth <= view.ActualWidth, "Minimum-size window keeps top search and right-side selectors inside the viewport"); Capture(window, "online-reference-minimum.png"); window.Width = 1320; window.Height = 900; window.UpdateLayout(); await cloud.PendingOperation;
    }
    private static IEnumerable<T> FindVisual<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        if (parent is T value) yield return value;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) foreach (var child in FindVisual<T>(VisualTreeHelper.GetChild(parent, index))) yield return child;
    }
    private static void CaptureElement(System.Windows.FrameworkElement view, string filename)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, filename)); encoder.Save(file);
    }
    private static async Task WaitForGalleryImagesAsync(System.Windows.Window window, CloudLibraryViewModel cloud)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            window.UpdateLayout();
            var images = FindImages(window).Where(image => image.IsVisible && DeskDisplay.App.Views.AsyncPreviewImage.GetUri(image) is not null).ToArray();
            if (!cloud.IsBusy && images.Length > 0 && images.All(image => image.Source is not null)) { Check(true, "Real online gallery thumbnails finish asynchronous decoding before screenshot capture"); return; }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Real online gallery thumbnails did not finish loading within five seconds");
    }
    private static IEnumerable<System.Windows.Controls.Image> FindImages(System.Windows.DependencyObject parent)
    {
        if (parent is System.Windows.Controls.Image image) yield return image;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) foreach (var child in FindImages(VisualTreeHelper.GetChild(parent, index))) yield return child;
    }
    private static void Capture(System.Windows.Window window, string filename)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, filename)); encoder.Save(file);
    }
    private static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
