using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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

namespace DeskDisplay.DisplayTests;
internal static class ClientExperienceTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-experience-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "source.png");
            var bitmap = BitmapSource.Create(1600, 400, 96, 96, PixelFormats.Bgr32, null, new byte[1600 * 400 * 4], 1600 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) encoder.Save(file);
            var uri = new Uri(path); var pending = AsyncPreviewImage.LoadAsync(uri); var heartbeat = false;
            await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => heartbeat = true);
            var preview = await pending; var shared = await AsyncPreviewImage.LoadAsync(uri);
            Check(heartbeat && preview is { IsFrozen: true, PixelWidth: 960, PixelHeight: 240 } && ReferenceEquals(preview, shared), "Background preview decoding preserves aspect, dispatcher responsiveness and shared frozen cache");
            var broken = Path.Combine(root, "broken.png"); await File.WriteAllTextAsync(broken, "broken");
            Check(await AsyncPreviewImage.LoadAsync(new Uri(broken)) is null, "Malformed preview is an empty result without crashing the dispatcher");
            File.Copy(path, broken, true); Check(await AsyncPreviewImage.LoadAsync(new Uri(broken)) is not null, "Repaired preview is retried rather than retaining a failed task forever");
            var image = new Image(); AsyncPreviewImage.SetUri(image, uri);
            var previewWindow = new Window { Content = image, Width = 400, Height = 180, ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000 };
            previewWindow.Show(); await Task.Delay(100); Check(image.Source is not null, "Actual WPF preview attachment loads after the control is visible"); previewWindow.Close(); await Task.Delay(50); Check(image.Source is null, "Unloaded preview releases its image reference");

            var target = new DisplayInfo("EXPERIENCE", "隔离测试副屏", "experience-target", 0, 0, 1920, 462, false);
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); var initial = new AppSettings { StudioServerUrl = "http://127.0.0.1:3000", TargetDisplay = new() { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = target.Width, LastHeight = target.Height } };
            using var manager = new WorkspaceTests.Manager { Displays = [target] }; using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger)); main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false)); await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "library")); var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            var store = new ThemeProjectStore(Path.Combine(root, "themes")); using var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator);
            Check(creator.FilteredAssets.Count == 0, "Creator starts with the real empty resource catalog");
            var bytes = await File.ReadAllBytesAsync(path); var id = Guid.NewGuid().ToString();
            var resource = new StudioResource(id, "联调素材", "", "STATIC_WALLPAPER", "1.0.0", Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, "image/png", $"/api/v1/client/resources/{id}/download", $"/api/v1/client/resources/{id}/preview", $"/api/v1/client/resources/{id}/thumbnail", [], [], DateTimeOffset.UtcNow) { Width = 1600, Height = 400 };
            var failDownload = true;
            using var api = new StudioApiClient(logger, new HttpClient(new Handler(request =>
            {
                var url = request.RequestUri!.AbsolutePath;
                if (url.EndsWith("/resources")) return Json(new { items = new[] { resource }, total = 1, page = 1, pageSize = 24 });
                if (url.EndsWith("/catalog/options")) return Json(new { categories = Array.Empty<object>(), tags = Array.Empty<object>() });
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(failDownload ? new byte[bytes.Length] : bytes) { Headers = { ContentType = new("image/png") } } };
            })), Path.Combine(root, "cache"));
            var cloud = new CloudLibraryViewModel(main, settings, initial, logger, api); main.SetCloud(cloud); await cloud.InitializeAsync();
            cloud.OpenDetailCommand.Execute(cloud.Items.Single()); cloud.UpdateScrollOffset(123);
            cloud.DownloadCommand.Execute(null); await cloud.PendingOperation;
            var task = cloud.DownloadTasks.Single(); Check(task.CanRetry && !cloud.LoadFailed && cloud.IsDetails, "Failed download retains resource details and does not turn the catalog into a connection failure");
            failDownload = false; cloud.RetryTaskCommand.Execute(task); await cloud.PendingOperation;
            Check(cloud.DownloadTasks.Count == 1 && ReferenceEquals(cloud.DownloadTasks[0], task) && task.State == DownloadTaskState.Completed, "Retry updates the original task and imports a verified real image");
            Check(creator.FilteredAssets.Count == 1 && ReferenceEquals(creator.FilteredAssets, creator.FilteredAssets), "Creator material cache refreshes when the existing library receives a download");
            var downloaded = creator.FilteredAssets.Single(); Check(downloaded.CreatorUsageHint.Contains("云端") && downloaded.Asset.CloudResourceId == id, "Downloaded materials retain cloud identity and show an honest source hint");
            cloud.ClearFinishedTasksCommand.Execute(null); Check(cloud.DownloadTasks.Count == 0 && library.AllItems.Count == 1 && File.Exists(downloaded.Asset.Path), "Clearing finished task records never deletes downloaded assets");
            double restored = 0; cloud.ScrollRestoreRequested += offset => restored = offset; cloud.BackCommand.Execute(null); Check(restored == 123 && cloud.IsGallery, "Online detail back restores the saved gallery position");
            var oldOptionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var newRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var releaseNew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var requests = 0; var optionsCalls = 0;
            using (var stagedApi = new StudioApiClient(logger, new HttpClient(new AsyncHandler(async request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/resources")) { if (++requests == 2) { newRequestStarted.SetResult(); await releaseNew.Task; } return Json(new { items = new[] { resource }, total = 1, page = 1, pageSize = 24 }); }
                if (++optionsCalls == 1) { oldOptionsStarted.SetResult(); await releaseOld.Task; }
                return Json(new { categories = new[] { new StudioCategory(optionsCalls == 1 ? "old" : "latest", "分类", null) }, tags = Array.Empty<object>() });
            })), Path.Combine(root, "staged-cache")))
            using (var stagedCloud = new CloudLibraryViewModel(main, settings, initial, logger, stagedApi))
            {
                var refresh = stagedCloud.InitializeAsync(); await oldOptionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); stagedCloud.SortMode = "新品"; releaseOld.SetResult(); await newRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(stagedCloud.Categories.Count == 0, "Cancelled cloud refresh cannot publish stale category metadata"); releaseNew.SetResult(); await refresh;
                Check(stagedCloud.Categories.Single().Id == "latest" && !stagedCloud.IsBusy, "Awaiting a refresh includes its coalesced latest request");
            }
            creator.NewCommand.Execute(null); await creator.PendingOperation; creator.AddAsset(downloaded.Asset); await creator.PendingOperation;
            Check(creator.Project.ActiveLayout.Layers.Single().CloudResourceId == id, "Adding a downloaded resource to an editable theme preserves its authorization identity");
            creator.ProjectName = "轮播甲"; await creator.SaveAsync(); var first = creator.Project.Clone();
            await store.SaveDraftAsync(first); var draftPath = Path.Combine(root, "themes", "Drafts", first.Id.ToString("N") + ".draft.json"); var mismatched = Path.Combine(root, "themes", "Drafts", Guid.NewGuid().ToString("N") + ".draft.json"); File.Copy(draftPath, mismatched);
            var drafts = await store.ListDraftsAsync(); Check(drafts.Count == 1 && store.DraftReadIssueCount == 1 && File.Exists(mismatched), "Draft identity mismatch is ignored and preserved without deleting a different project");
            await creator.RecoverDraftAsync(drafts.Single()); Check(creator.Project.Id != first.Id && (await store.ListAsync()).Any(p => p.Id == first.Id), "Recovery creates an independent editable project and retains the saved source");
            creator.ProjectName = "轮播乙"; await creator.SaveAsync(); await creator.RefreshProjectsAsync(); await creator.LoadThemePlaylistAsync();
            creator.AddThemePlaylist(first.Id); creator.AddThemePlaylist(creator.Project.Id); creator.ThemePlaylistTarget = "Windows 副屏"; await creator.StartThemePlaylistAsync();
            Check(creator.IsThemeRotating && creator.ThemeCurrentName == "轮播甲" && creator.ThemeNextName == "轮播乙", "Theme playback exposes current and next saved sources through the real scene pipeline");
            var playlistWindow = new CreatorThemePlaylist(creator) { ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000 }; playlistWindow.Show(); await Task.Delay(80); playlistWindow.UpdateLayout(); Capture(playlistWindow, "client-experience-playlist.png"); playlistWindow.Close();
            var materialsWindow = new CreatorMaterialBrowser(creator) { ShowActivated = false, ShowInTaskbar = false, Left = -30000, Top = -30000 }; materialsWindow.Show(); await Task.Delay(100); materialsWindow.UpdateLayout(); Capture(materialsWindow, "client-experience-materials.png"); materialsWindow.Close();
            creator.ToggleThemePause(); var pausedScene = output.CurrentScene; await Task.Delay(100); Check(creator.IsThemePaused && creator.ThemeCountdown.Contains("已暂停") && ReferenceEquals(output.CurrentScene, pausedScene), "Pausing automatic theme switching retains the live current scene");
            await creator.AdvanceThemePlaylistAsync(); Check(creator.IsThemePaused && creator.ThemeCurrentName == "轮播乙", "Manual next works while automatic switching remains paused");
            creator.ToggleThemePause(); Check(!creator.IsThemePaused && creator.ThemeCountdown.Contains("秒后"), "Resume restarts the remaining theme interval"); creator.StopThemePlaylist();
            await creator.PrepareForCloseAsync(); await cloud.PrepareForCloseAsync(); await library.PrepareForCloseAsync();
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Capture(FrameworkElement view, string name) { var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json") };
    private sealed class AsyncHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); Console.WriteLine("PASS: " + label); }
}
