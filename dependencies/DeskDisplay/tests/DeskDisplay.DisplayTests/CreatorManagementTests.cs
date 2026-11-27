using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
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

internal static class CreatorManagementTests
{
    public static async Task RunAsync(ILogger logger)
    {
        await CreatorProductivityTests.RunAsync(logger);
        await CreatorWorkbenchTests.RunAsync(logger);
        var root = Path.Combine(Path.GetTempPath(), "DeskDisplay-creator-management-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var settings = new JsonSettingsService(logger, Path.Combine(root, "settings.json")); var initial = new AppSettings();
            using var manager = new WorkspaceTests.Manager(); using var output = new WorkspaceTests.Output();
            using var devices = new DevicesViewModel(manager, new TargetDisplayService(initial, settings, logger), logger, output);
            var clock = new ClockScene(); using var main = new MainViewModel(devices, clock);
            main.SetImageEditor(new ImageSceneViewModel(devices, settings, initial, clock, main.PreviewScene, logger));
            main.SetVideoEditor(new VideoSceneViewModel(main, new WorkspaceTests.Playback(), logger));
            main.SetWorkspace(new WorkspaceViewModel(main, clock, new PerformanceScene(), output, settings, new WorkspaceTests.Sensors(), initial, logger, false));
            await devices.RefreshAsync(); await main.Workspace!.RestoreAsync();
            var catalog = new WallpaperCatalog(logger, Path.Combine(root, "catalog"));
            var missing = new WallpaperAsset { Name = "丢失的图片", Path = Path.Combine(root, "missing.png"), Width = 64, Height = 16 };
            await catalog.SaveAsync(new WallpaperLibrary { Assets = [missing] });
            var library = new LibraryViewModel(main, catalog, logger); main.SetLibrary(library); await library.InitializeAsync();
            var themeDirectory = Path.Combine(root, "themes"); var store = new ThemeProjectStore(themeDirectory);
            var formal = new ThemeProject { Name = "正式版本" };
            formal.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "横屏图片", Path = missing.Path, AssetId = missing.Id, Width = 1920, Height = 480 });
            formal.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "竖屏图片", Path = missing.Path, AssetId = missing.Id, Width = 480, Height = 1920 });
            await store.SaveAsync(formal);
            var first = new CreatorViewModel(main, logger, store); main.SetCreator(first); main.SelectedPage = AppPage.Creator; await first.InitializeAsync();
            await first.OpenProjectAsync(formal.Clone()); first.ProjectName = "恢复的编辑内容";
            await Task.Delay(15300);
            for (var retry = 0; retry < 20 && (await store.ListDraftsAsync()).Count == 0; retry++) await Task.Delay(100);
            Check((await store.ListDraftsAsync()).Count == 1, "The actual dispatcher autosave timer persists modified work without a manual save");
            Check((await store.ListAsync()).Single().Name == "正式版本" && (await store.ListDraftsAsync()).Single().Project.Name == "恢复的编辑内容", "Autosave writes an independent draft without overwriting the formal source");
            first.Dispose(); // Simulates a session ending without normal source save; only the isolated test store is affected.
            var creator = new CreatorViewModel(main, logger, store); main.SetCreator(creator); await creator.InitializeAsync();
            Check(creator.HasDrafts && creator.Drafts.Count == 1, "A fresh creator discovers the previous session's recovery draft");
            var badDraft = Path.Combine(themeDirectory, "Drafts", "broken.draft.json"); await File.WriteAllTextAsync(badDraft, "invalid");
            await File.WriteAllTextAsync(Path.Combine(themeDirectory, "Drafts", "empty.draft.json"), "{\"Project\":null,\"SavedAt\":\"2026-10-20T00:00:00Z\"}");
            Check((await store.ListDraftsAsync()).Count == 1 && File.Exists(badDraft), "Damaged drafts are preserved and do not prevent recovery browsing");
            var recoveryWindow = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 }; recoveryWindow.Show();
            try { await Task.Delay(100); recoveryWindow.UpdateLayout(); Capture(recoveryWindow, "creator-draft-recovery.png"); }
            finally { recoveryWindow.Close(); }
            creator.ConfirmDraftRecovery=_=>Task.FromResult(true);creator.RecoverDraftCommand.Execute(creator.Drafts[0]); await creator.PendingOperation;
            Check(creator.Project.Id != formal.Id && creator.IsDirty && creator.Project.Name.Contains("恢复") && (await store.ListAsync()).Single().Name == "正式版本", "Recovery creates an editable copy and preserves the formal project");
            Check((await store.ListDraftsAsync()).Single().Project.Id == creator.Project.Id, "Recovery writes the new copy before retiring the old draft");
            await creator.SaveAsync(); Check(!(await store.ListDraftsAsync()).Any(), "A successful formal save removes the corresponding recovery draft");
            await creator.RefreshMissingAssetsAsync(); Check(creator.MissingAssets.Count == 2, "Missing asset inspection covers both independent layouts");
            var repairFolder = Path.Combine(root, "batch-repair"); Directory.CreateDirectory(repairFolder);
            Check(await creator.RepairFromFolderAsync(repairFolder) == 0 && creator.MissingAssets.Count == 2, "Folder repair leaves unmatched references unchanged");
            var batchPath = Path.Combine(repairFolder, "missing.png"); await File.WriteAllTextAsync(batchPath, "invalid image");
            Check(await creator.RepairFromFolderAsync(repairFolder) == 0 && creator.MissingAssets.Count == 2, "Folder repair skips invalid same-name image data");
            var invalidPlan = await creator.PreviewFolderRepairAsync(repairFolder);
            Check(invalidPlan.Items.Count == 2 && invalidPlan.Items.All(i => !i.CanRepair) && creator.Project.Landscape.Layers[0].Path == missing.Path && !library.AllItems.Any(i => i.Asset.Path == batchPath), "Repair preview reports invalid matches without modifying source or catalog");
            WriteImage(batchPath);
            var rejectedStalePlan = false;
            try { await creator.ApplyFolderRepairPlanAsync(invalidPlan); } catch (InvalidOperationException) { rejectedStalePlan = true; }
            Check(rejectedStalePlan && creator.Project.Landscape.Layers[0].Path == missing.Path, "Changed matching results invalidate a repair preview before source mutation");
            var previewPlan = await creator.PreviewFolderRepairAsync(repairFolder);
            var previewDialog = new CreatorRepairPreview(previewPlan) { ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual }; previewDialog.Show();
            try { previewDialog.UpdateLayout(); Capture(previewDialog, "creator-repair-preview.png"); Check(previewPlan.Items.All(i => i.CanRepair), "Real WPF repair preview opens with both layout matches"); } finally { previewDialog.Close(); }

            creator.Project.Landscape.Layers[0].AssetOrigin = ThemeAssetOrigin.Cloud;
            creator.Project.Landscape.Layers[0].CloudResourceId = "batch-protected";
            Check(await creator.ApplyFolderRepairPlanAsync(previewPlan) == 2 && creator.MissingAssets.Count == 0 && creator.Project.Landscape.Layers[0].CloudResourceId == "batch-protected" && creator.Project.Landscape.Layers[0].AssetOrigin == ThemeAssetOrigin.Cloud, "Folder repair resolves both layouts while retaining cloud restrictions");
            creator.UndoCommand.Execute(null); await creator.PendingOperation; await creator.RefreshMissingAssetsAsync();
            Check(creator.MissingAssets.Count == 2 && creator.Project.Landscape.Layers[0].Path == missing.Path, "Batch folder repair is one undo transaction");
            creator.Project.Landscape.Layers[0].AssetOrigin = ThemeAssetOrigin.Unknown;
            creator.Project.Landscape.Layers[0].CloudResourceId = "";
            var imagePath = Path.Combine(root, "relocated.png"); WriteImage(imagePath);
            await creator.RepairAssetAsync(creator.MissingAssets[0], imagePath);
            Check(creator.MissingAssets.Count == 0 && creator.Project.Landscape.Layers[0].AssetId == creator.Project.Portrait.Layers[0].AssetId && library.AllItems.Any(i => i.Asset.Path == imagePath), "Relocation reuses the existing library and repairs shared references across layouts");
            creator.UndoCommand.Execute(null); await creator.PendingOperation; await creator.RefreshMissingAssetsAsync();
            Check(creator.MissingAssets.Count == 2, "Asset relocation can be undone as one source edit");
            creator.RedoCommand.Execute(null); await creator.PendingOperation; await creator.RefreshMissingAssetsAsync();
            Check(creator.MissingAssets.Count == 0, "Redo restores repaired asset references");
            var cloudPath = Path.Combine(root, "cloud.png"); WriteImage(cloudPath);
            var transferProject = creator.Project.Clone();
            transferProject.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "云端引用", Path = cloudPath, AssetId = creator.Project.Landscape.Layers[0].AssetId, AssetOrigin = ThemeAssetOrigin.Cloud, CloudResourceId = "protected-cloud-reference" });
            transferProject.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "未知来源", Path = cloudPath, AssetOrigin = ThemeAssetOrigin.Unknown });
            var transfer = new ThemeTransferService(store); var bundle = Path.Combine(root, "export.d92theme");
            var result = await transfer.ExportAsync(transferProject, bundle, true, library.AllItems.ToDictionary(i => i.Asset.Id, i => i.Asset));
            Check(result.Included == 1 && result.Omitted == 2, "Bundles deduplicate local media and omit cloud and unknown-origin files");
            using (var zip = ZipFile.OpenRead(bundle))
            {
                using var reader = new StreamReader(zip.GetEntry("project.theme.json")!.Open()); var packaged = JsonSerializer.Deserialize<ThemeProject>(await reader.ReadToEndAsync())!;
                Check(zip.Entries.Count == 3 && packaged.Landscape.Layers.Last().Path == "" && packaged.Landscape.Layers.Last().CloudResourceId == "protected-cloud-reference", "Portable bundles preserve cloud identity without embedding protected cloud files");
            }
            var imported = await transfer.ImportAsync(bundle); var again = await transfer.ImportAsync(bundle);
            Check(imported.Project.Id != transferProject.Id && again.Project.Id != imported.Project.Id && imported.MediaPaths.Count == 1 && (await File.ReadAllBytesAsync(imported.MediaPaths[0])).SequenceEqual(await File.ReadAllBytesAsync(imagePath)), "Bundle import verifies hashes and allocates fresh project and media directories");
            Check((await store.ListAsync()).Count == 2, "Importing a package does not overwrite or silently publish an existing formal project");
            var source = Path.Combine(root, "source.theme.json"); await transfer.ExportAsync(creator.Project, source, false, library.AllItems.ToDictionary(i => i.Asset.Id, i => i.Asset));
            var sourceImport = await transfer.ImportAsync(source); Check(sourceImport.Project.Id != creator.Project.Id && sourceImport.MediaPaths.Count == 0, "Source-only import creates a new editable identity and leaves media external");
            var traversal = Path.Combine(root, "traversal.d92theme");
            using (var zip = ZipFile.Open(traversal, ZipArchiveMode.Create)) { Write(zip, "project.theme.json", "{}"); Write(zip, "manifest.json", "{}"); Write(zip, "../outside.txt", "bad"); }
            await Reject(() => transfer.ImportAsync(traversal), "Bundle extraction rejects traversal paths");
            Check(!File.Exists(Path.Combine(root, "outside.txt")), "Rejected traversal packages cannot create files outside the import directory");
            var duplicate = Path.Combine(root, "duplicate.d92theme");
            using (var zip = ZipFile.Open(duplicate, ZipArchiveMode.Create)) { Write(zip, "project.theme.json", "{}"); Write(zip, "project.theme.json", "{}"); Write(zip, "manifest.json", "{}"); }
            await Reject(() => transfer.ImportAsync(duplicate), "Duplicate package entries are rejected");
            var executable = Path.Combine(root, "executable.d92theme");
            using (var zip = ZipFile.Open(executable, ZipArchiveMode.Create)) { Write(zip, "project.theme.json", "{}"); Write(zip, "manifest.json", "{}"); Write(zip, "plugin.exe", "not executable content"); }
            await Reject(() => transfer.ImportAsync(executable), "Packages cannot carry arbitrary executable entries");
            var linked = Path.Combine(root, "linked.d92theme");
            using (var zip = ZipFile.Open(linked, ZipArchiveMode.Create)) { Write(zip, "project.theme.json", "{}"); Write(zip, "manifest.json", "{}"); var link = zip.CreateEntry("assets/" + new string('a', 64) + ".png"); link.ExternalAttributes = unchecked((int)0xA1FF0000); using var writer = new StreamWriter(link.Open()); writer.Write("outside"); }
            await Reject(() => transfer.ImportAsync(linked), "Symbolic-link archive entries are rejected");
            var oversize = Path.Combine(root, "oversize.d92theme");
            using (var zip = ZipFile.Open(oversize, ZipArchiveMode.Create)) { Write(zip, "project.theme.json", new string('x', 2_000_001)); Write(zip, "manifest.json", "{}"); }
            await Reject(() => transfer.ImportAsync(oversize), "Oversized package metadata is rejected before extraction");
            var tampered = Path.Combine(root, "tampered.d92theme"); File.Copy(bundle, tampered);
            using (var zip = ZipFile.Open(tampered, ZipArchiveMode.Update))
            { var entry = zip.Entries.First(e => e.FullName.StartsWith("assets/")); var name = entry.FullName; using var memory = new MemoryStream(); using (var input = entry.Open()) input.CopyTo(memory); var bytes = memory.ToArray(); bytes[^1] ^= 1; entry.Delete(); using var outputEntry = zip.CreateEntry(name).Open(); outputEntry.Write(bytes); }
            var directoryCount = Directory.GetDirectories(Path.Combine(themeDirectory, "ImportedAssets")).Length;
            await Reject(() => transfer.ImportAsync(tampered), "Tampered media fails SHA256 verification");
            Check(Directory.GetDirectories(Path.Combine(themeDirectory, "ImportedAssets")).Length == directoryCount, "Failed extraction removes only its own incomplete media directory");
            var beforeImportId = creator.Project.Id;
            await creator.ImportProjectFileAsync(bundle);
            Check(creator.Project.Id != beforeImportId && creator.IsDirty && creator.Project.Landscape.Layers[0].AssetId is Guid adoptedId && library.AllItems.Any(i => i.Asset.Id == adoptedId && File.Exists(i.Asset.Path)), "The complete import workflow registers unpacked media in the existing library");
            Check((await store.ListAsync()).Count == 2, "Import completion keeps the new project as a draft until explicitly saved");
            foreach (var template in creator.Templates)
            {
                var a = template.Create(); var b = template.Create(); a.Validate(); b.Validate();
                if (a.Id == b.Id || a.Landscape.Layers.Count == 0 || a.Portrait.Layers.Count == 0 || a.Landscape.Layers.Any(l => b.Landscape.Layers.Any(r => l.Id == r.Id))) throw new InvalidOperationException("Templates shared editable identities");
            }
            Check(creator.Templates.GroupBy(t => t.Style).All(g => g.Select(t => t.Orientation).Distinct().Count() == 2) && creator.Templates.Any(t => t.RequiresCamera) && creator.Templates.Any(t => t.SupportsBackground), "Built-in template families include independent landscape and portrait sources plus explicit plugin and background metadata");
            creator.BrowseTemplatesCommand.Execute(null); creator.SelectedTemplate = creator.Templates.First(t => t.Style == "clock" && t.Orientation == ThemeOrientation.Portrait);
            Check(creator.IsTemplates && !creator.IsEditor && creator.TemplateAspect == .25 && creator.TemplateScene.Project.ActiveLayout.Layers.Any(l => l.Kind == ThemeElementKind.DigitalClock), "Template selection previews a real portrait composition before creation");
            var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000 }; window.Show();
            try
            {
                await Task.Delay(100); window.UpdateLayout(); Capture(window, "creator-templates.png");
                creator.UseTemplateCommand.Execute(null); await creator.PendingOperation;
                Check(creator.IsEditor && creator.IsPortrait && creator.IsDirty, "Using a previewed template opens an unsaved editable project");
                creator.SelectedTemplate = creator.Templates.First(t => t.Style == "photo"); creator.BrowseTemplatesCommand.Execute(null); creator.UseTemplateCommand.Execute(null); await creator.PendingOperation;
                creator.CheckAssetsCommand.Execute(null); await creator.PendingOperation; window.UpdateLayout(); Capture(window, "creator-missing-assets.png");
                Check(creator.IsRepairing && creator.MissingAssets.Count == 2, "Image templates expose genuine empty image slots through the repair workflow");
                creator.ReturnFromRepairCommand.Execute(null); Check(creator.IsEditor, "Repair inspection returns to the same editable project");
            }
            finally { window.Close(); }
            await creator.PrepareForCloseAsync(); await library.PrepareForCloseAsync(); Console.WriteLine("All creator project management tests passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Write(ZipArchive zip, string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
    private static async Task Reject(Func<Task<ThemeImportResult>> action, string message) { var rejected = false; try { await action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, message); }
    private static void WriteImage(string path)
    {
        var bitmap = BitmapSource.Create(64, 16, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 64 * 16).SelectMany(_ => new byte[] { 95, 60, 30, 255 }).ToArray(), 64 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private static void Capture(Window window, string name) { var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(file); }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
