using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class LibraryTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog"));
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json"));
            var target = new DisplayInfo("LIBRARY", "Library target", "library-device", -1600, 0, 1600, 900, false);
            var initial = new AppSettings { TargetDisplay = new TargetDisplaySettings { DeviceId = target.DeviceId, DeviceName = target.DeviceName, FriendlyName = target.FriendlyName, LastWidth = 1600, LastHeight = 900 } };
            using var manager = new WorkspaceTests.Manager { Displays = [target] };
            using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); var performance = new PerformanceScene();
            using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, performance, output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            var library = new LibraryViewModel(main, catalog, logger);
            main.SetLibrary(library);
            await devices.RefreshAsync(); await main.Workspace!.RestoreAsync(); await library.InitializeAsync();
            Check(library.IsEmpty, "Empty local library starts without fake online resources");
            string[] names = ["静谧山川", "暮光海岸", "晨雾森林", "冰蓝远山", "落日余晖", "星河之境", "月色湖畔", "云端日出", "竹林清风", "雪原微光", "碧海长空", "秋野黄昏", "雨后青山", "极光之夜", "春日花谷"];
            var files = names.Select(name => Path.Combine(root, name + ".png")).ToArray();
            for (var index = 0; index < files.Length; index++) CreateFixture(files[index], index);
            await library.ImportAsync(files.Concat([files[0], Path.Combine(root, "missing.png")]));
            Check(library.VisibleItems.Count == 12 && library.HasMoreItems && library.Status.Contains("重复跳过 1") && library.Status.Contains("失败 1"), "Initial library batch is bounded and import continues after duplicate or missing files");
            library.LoadMore();
            Check(library.VisibleItems.Count == 15 && !library.HasMoreItems, "Scrolling can append the next library batch without paging");
            await Task.WhenAll(library.VisibleItems.Select(item => item.EnsureThumbnailAsync()));
            Check(library.VisibleItems.All(item => item.Asset.Width == 1600 && item.Thumbnail is { PixelWidth: 640, PixelHeight: 360 }),
                "Static card thumbnails load asynchronously and preserve the complete source aspect ratio");
            var visibleSnapshot = library.VisibleItems;
            var categoriesSnapshot = library.Categories;
            var cachedThumbnail = library.VisibleItems[0].Thumbnail;
            main.SelectedPage = AppPage.Devices; main.SelectedPage = AppPage.Library;
            Check(ReferenceEquals(visibleSnapshot, library.VisibleItems) && ReferenceEquals(categoriesSnapshot, library.Categories), "Sidebar navigation preserves resource and category collections without rebuilding cards");
            library.SelectedCategory = "不存在的分类"; library.SelectedCategory = "全部";
            Check(ReferenceEquals(cachedThumbnail, library.VisibleItems[0].Thumbnail), "Returning from category filters reuses the warm thumbnail cache");
            Check(library.VisibleItems[0].Name == names[^1], "Newly imported wallpapers appear first with stable added-time sorting");
            library.VisibleItems[0].IsSelected = true; library.VisibleItems[1].IsSelected = true;
            library.BatchCategory = "批量分类"; library.BatchSubcategory = "批量子类"; library.BatchTags = "批量 标签";
            library.BatchUpdateCommand.Execute(null);
            Check(library.SelectedCount == 2 && library.VisibleItems.Take(2).All(item => item.Category == "批量分类" && item.Subcategory == "批量子类" && item.Tags == "批量 标签"), "Batch metadata update applies only to selected wallpapers");
            library.ClearSelectionCommand.Execute(null); Check(library.SelectedCount == 0, "Batch selection can be cleared without changing files");
            library.UpdateGalleryViewport(1200, 100, 0);
            Check(library.VisibleItems.Count == 2 && library.PageCount == 3 && library.ShowPagination && !library.CanNextPage && library.PageLabel.Contains("加载满三屏"), "A two-column page starts with one viewport and visible paging guidance");
            library.UpdateGalleryViewport(1183, 100, 0);
            Check(library.VisibleItems.Count == 2 && library.PageCount == 3, "Scrollbar-width viewport changes do not restart gallery layout");
            library.LoadMore(); library.LoadMore();
            Check(library.VisibleItems.Count == 6 && library.ShowPagination && library.CanNextPage && library.PageLabel == "第 1 / 3 页", "Pagination activates after three two-column viewport batches");
            library.NextPageCommand.Execute(null);
            Check(library.CurrentPage == 2 && library.VisibleItems.Count == 2, "Pagination opens the next bounded page");
            library.UpdateGalleryViewport(1200, 800, 0);
            using (var exclusive = new FileStream(files[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(exclusive.Length > 0, "Import and thumbnails do not lock original files");
            var first = library.VisibleItems[0];
            var detailTask = first.EnsureDetailImageAsync();
            Check(!detailTask.IsCompleted, "Full-size detail decoding yields to the UI dispatcher");
            await detailTask;
            Check(first.DetailImage is { PixelWidth: 1600, PixelHeight: 900 }, "Detail preview uses a higher-resolution source image instead of the 640-pixel card thumbnail");
            using (var exclusive = new FileStream(files[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(exclusive.Length > 0, "Detail preview does not lock the original image");
            library.Select(first);
            library.SelectedTab = "插件"; library.SelectedTab = "我的资源";
            Check(library.IsDetails && library.Selected == first, "Returning from another menu restores the wallpaper detail state");
            library.EditCategory = "自然风景"; library.EditSubcategory = "山川"; library.EditTags = "山川 宁静";
            library.SaveMetadataCommand.Execute(null); library.FavoriteCommand.Execute(first);
            library.BackCommand.Execute(null);
            library.Search = "宁静"; Check(library.VisibleItems.Count == 1, "Tag search filters local resources");
            library.Search = ""; library.SelectedCategory = "自然风景";
            Check(library.Subcategories.Contains("山川"), "Primary category exposes its secondary categories");
            library.SelectedSubcategory = "山川";
            Check(library.VisibleItems.Count == 1 && library.VisibleItems[0] == first, "Secondary category filters within the selected primary category");
            library.Search = ""; library.SelectedCategory = "全部"; library.SelectedResourceScope = "收藏"; Check(library.VisibleItems.Count == 1, "Favorites resource scope works");
            library.SelectedResourceScope = "全部资源";
            library.SelectedCategory = "全部";
            var window = new DeskDisplay.App.Views.MainWindow { DataContext = main };
            try
            {
                window.Show(); window.UpdateLayout(); await Task.Delay(100);
                Capture(window, "library-gallery.png");
                library.SelectedResourceType = "壁纸"; window.UpdateLayout(); Capture(window, "library-wallpaper-categories.png");
                Check(library.ShowCategoryFilters && library.ResourceScopes.Count == 6 && library.ResourceTypes.Count == 5,
                    "Resource center exposes status, type and wallpaper category levels");
                library.SelectedResourceScope = "已购买";
                Check(library.IsEmpty && library.ShowFilteredEmpty && library.EmptyStateMessage.Contains("已授权资源"),
                    "Unavailable purchase rights use an honest empty state");
                library.SelectedResourceScope = "全部资源"; library.SelectedResourceType = "全部";
                library.Select(first); library.PreviewCommand.Execute(null); await library.PendingOperation;
                Check(main.Scene is ImageScene && library.SelectedIsLive, "Detail preview reuses shared ImageScene renderer");
                window.UpdateLayout(); Capture(window, "library-detail.png");
                library.ApplyCommand.Execute(null); await library.PendingOperation;
                Check(ReferenceEquals(output.CurrentScene, main.Scene), "Detail apply uses same scene instance on target");
                library.PlaylistCommand.Execute(first);
                var second = library.VisibleItems[1]; library.PlaylistCommand.Execute(second);
                main.OpenCarouselManagerCommand.Execute(null); window.UpdateLayout(); Capture(window, "carousel-management.png");
                Check(main.SelectedPage == AppPage.Carousel && library.PlaylistItems.Count == 2 && library.HasPlaylistItems,
                    "Home carousel entry opens the dedicated manager with the saved playlist");
                main.SelectedPage = AppPage.Library;
                library.Select(second); library.MoveUpCommand.Execute(null);
                library.PlaylistOnly = true;
                Check(library.VisibleItems[0] == second, "Playlist order editing changes the management view order");
                library.SelectedTab = "静态壁纸";
                Check(!library.PlaylistOnly && library.VisibleItems.Count > 0,
                    "Leaving My Resources clears the playlist-only filter before showing static wallpapers");
                library.SelectedTab = "我的资源";
                library.MoveDownCommand.Execute(null);
                library.IntervalSeconds = 10;
                library.NextCommand.Execute(null); await library.PendingOperation;
                var savedFirst = ((ImageScene)output.CurrentScene!).Path;
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(((ImageScene)output.CurrentScene!).Path != savedFirst, "Manual next traverses the saved playlist");
                library.RotationCommand.Execute(null); await library.PendingOperation;
                var rotatingPath = ((ImageScene)output.CurrentScene!).Path;
                await Task.Delay(10600);
                await library.PendingOperation;
                Check(output.CurrentScene is ImageScene next && next.Path != rotatingPath, "Automatic playlist timer advances after configured interval");
                library.StopCommand.Execute(null); Check(!output.IsOpen && library.RotationLabel.Contains("开启"), "Stop disables rotation and external output");
                library.MoveCarouselItem(second.Asset.Id, first.Asset.Id);
                Check(library.PlaylistItems[0] == second, "Drag reorder moves an item to the target position");
                library.MoveCarouselItem(first.Asset.Id, second.Asset.Id);
                var originalPreset = library.SelectedPreset;
                library.PresetName = "测试方案"; library.CreatePresetCommand.Execute(null);
                Check(library.CarouselPresets.Count == 2 && library.SelectedPreset?.Name == "测试方案", "New preset clones the existing playlist");
                library.RemoveCarouselItems(new[] { first.Asset.Id, second.Asset.Id });
                library.AddCarouselItems(new[] { second.Asset.Id, second.Asset.Id, Guid.NewGuid() });
                library.IntervalSeconds = 30;
                Check(library.PlaylistItems.Count == 1, "Batch editing ignores duplicate and unknown IDs");
                library.SelectedPreset = originalPreset;
                Check(library.PlaylistItems.Count == 2 && library.IntervalSeconds == 10, "Switching presets restores independent list and interval");
                library.RotationCommand.Execute(null); await library.PendingOperation;
                var pausedScene = output.CurrentScene;
                library.CarouselPauseCommand.Execute(null);
                await Task.Delay(600);
                Check(library.CarouselPlaybackStatus.Contains("暂停") && ReferenceEquals(pausedScene, output.CurrentScene) && output.IsOpen, "Pausing retains the current output scene");
                library.CarouselPauseCommand.Execute(null);
                Check(library.CarouselPlaybackStatus == "正在轮播" && library.CarouselCountdown.Contains("秒"), "Resuming restores the countdown");
                main.OpenCarouselManagerCommand.Execute(null); window.UpdateLayout(); Capture(window, "carousel-management.png");
                library.StopCommand.Execute(null);
                File.Delete(first.Asset.Path);
                library.NextCommand.Execute(null); await library.PendingOperation;
                Check(output.CurrentScene is ImageScene valid && valid.Path == second.Asset.Path, "Missing playlist files are skipped");
                manager.Displays = []; await devices.RefreshAsync();
                Check(!library.RotationCommand.CanExecute(null), "Missing display disables new rotation");
                library.SelectedTab = "设备"; window.UpdateLayout(); Capture(window, "library-devices.png");
                library.SelectedTab = "我的资源"; library.BackCommand.Execute(null);
                window.Width = 1100; window.Height = 740; window.UpdateLayout(); Capture(window, "library-compact.png");
            }
            finally { window.Close(); }
            await library.PrepareForCloseAsync(); await main.Workspace.PrepareForCloseAsync();
            var restored = await catalog.LoadAsync();
            Check(restored.Assets.Count == 15 && restored.Assets.Any(item => item.Favorite && item.Category == "自然风景" && item.Subcategory == "山川") && restored.Playlist.Count == 2 && restored.IntervalSeconds == 10,
                "Resource metadata, favorites and playlist survive restart");
            Check(restored.CarouselPresets.Count == 2 && restored.CarouselPresets.Any(p => p.Name == "测试方案" && p.Playlist.Count == 1 && p.IntervalSeconds == 30), "Multiple carousel presets survive restart");
            await File.WriteAllTextAsync(Path.Combine(root, "catalog", "library.json"), "invalid");
            Check((await catalog.LoadAsync()).Assets.Count == 0, "Damaged resource index cannot prevent startup");
            using var unopened = new LibraryViewModel(main, catalog, logger);
            await unopened.PrepareForCloseAsync();
            Check(await File.ReadAllTextAsync(Path.Combine(root, "catalog", "library.json")) == "invalid", "Closing before library initialization does not overwrite existing data");
            Console.WriteLine("All library tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void CreateFixture(string path, int variant)
    {
        var palettes = new[] { ("#7DADA9", "#173F48", "#EDD9A4"), ("#DAB3A1", "#505C70", "#FFE5AE"), ("#7E9C88", "#183F3D", "#D7E5C0"), ("#A8D5DF", "#3B607D", "#EAF3EE"), ("#D1917A", "#624B62", "#FFD1A0"), ("#4A5778", "#182F4B", "#C5CBEA") };
        var palette = palettes[variant % palettes.Length];
        static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(ColorOf(palette.Item1), ColorOf(palette.Item2), 90), null, new Rect(0, 0, 1600, 900));
            dc.DrawEllipse(new SolidColorBrush(ColorOf(palette.Item3)), null, new Point(1150 - variant * 60, 230), 105, 105);
            for (var layer = 0; layer < 4; layer++)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, 900), true, true);
                    for (var x = 0; x <= 1600; x += 80)
                        ctx.LineTo(new Point(x, 470 + layer * 95 + Math.Sin(x / (160d + layer * 35) + variant + layer) * (85 - layer * 10)), true, false);
                    ctx.LineTo(new Point(1600, 900), true, false);
                }
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(75 + layer * 45), 14, 40, 53)), null, geometry);
            }
        }
        var bitmap = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static void Capture(Window window, string filename)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, filename)); encoder.Save(file);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
