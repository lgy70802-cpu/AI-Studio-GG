using System.IO;
using DeskDisplay.Core.Models;
using Microsoft.Win32;

namespace DeskDisplay.App.ViewModels;

public sealed record ThemeRepairMatch(Guid LayerId, string Layout, string Name, string OriginalPath, string CandidatePath, string Result, bool CanRepair);
public sealed record ThemeRepairPlan(Guid ProjectId, string Directory, IReadOnlyList<ThemeRepairMatch> Items);

public sealed partial class CreatorViewModel
{
    public AsyncCommand RepairFolderCommand { get; private set; } = null!;
    private void InitializeFolderRepair() => RepairFolderCommand = new(() => RunAsync(async () =>
    {
        var dialog = new OpenFolderDialog { Title = "选择素材所在文件夹（仅匹配当前目录内同名文件）", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        var plan = await PreviewFolderRepairAsync(dialog.FolderName);
        var preview = new DeskDisplay.App.Views.CreatorRepairPreview(plan) { Owner = System.Windows.Application.Current?.MainWindow };
        if (preview.ShowDialog() == true) await ApplyFolderRepairPlanAsync(plan, preview.SelectedMatches);
    }), () => CanEdit && _editing, _logger);

    public async Task<ThemeRepairPlan> PreviewFolderRepairAsync(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("素材文件夹不存在。");
        await RefreshMissingAssetsAsync();
        var projectId = Project.Id;
        var missing = MissingAssets.ToArray();
        var files = await Task.Run(() => Directory.EnumerateFiles(directory).GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToArray(), StringComparer.OrdinalIgnoreCase));
        var rows = new List<ThemeRepairMatch>();
        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in missing)
        {
            var candidate = ""; var result = "未找到同名文件"; var name = Path.GetFileName(item.Path);
            if (!string.IsNullOrEmpty(name) && files.TryGetValue(name, out var matches))
            {
                result = "存在多个同名文件，需手动定位";
                if (matches.Length == 1)
                {
                    candidate = matches[0]; var ext = Path.GetExtension(candidate).ToLowerInvariant();
                    result = item.IsVideo ? ext == ".mp4" ? "可修复（视频播放能力仍由现有后端检查）" : "文件类型不符" : ext is ".png" or ".jpg" or ".jpeg" ? "可修复" : "文件类型不符";
                    if (result.StartsWith("可修复") && !item.IsVideo)
                    {
                        if (!validated.TryGetValue(candidate, out var error))
                        {
                            error = "";
                            try { await new DeskDisplay.Display.DataProviders.ImageSceneLoader().LoadAsync(candidate, ImageDisplayMode.Uniform); }
                            catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException) { error = "图片损坏或无法读取"; }
                            validated[candidate] = error;
                        }
                        if (error.Length > 0) result = error;
                    }
                }
            }
            rows.Add(new(item.LayerId, item.Orientation == ThemeOrientation.Portrait ? "竖屏" : "横屏", item.Name, item.Path, candidate, result, result.StartsWith("可修复")));
        }
        return new(projectId, Path.GetFullPath(directory), rows);
    }
    public async Task<int> ApplyFolderRepairPlanAsync(ThemeRepairPlan plan, IReadOnlyList<ThemeRepairMatch>? selected = null)
    {
        var current = await PreviewFolderRepairAsync(plan.Directory);
        if (current.ProjectId != plan.ProjectId || !current.Items.SequenceEqual(plan.Items))
            throw new InvalidOperationException("项目或匹配结果已变化，请重新预览后修复。");
        selected ??= plan.Items.Where(i => i.CanRepair).ToArray();
        if (selected.Any(i => !i.CanRepair || !plan.Items.Contains(i))) throw new InvalidOperationException("匹配选择无效，请重新预览。");
        if (selected.Count == 0) return 0;
        return await RepairFolderSelectionAsync(plan.Directory, selected);

    }

    public Task<int> RepairFromFolderAsync(string directory) => RepairFolderSelectionAsync(directory, null);
    private async Task<int> RepairFolderSelectionAsync(string directory, IReadOnlyList<ThemeRepairMatch>? selected)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("素材文件夹不存在。");
        await RefreshMissingAssetsAsync();
        var files = await Task.Run(() => Directory.EnumerateFiles(directory).GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToArray(), StringComparer.OrdinalIgnoreCase));
        var replacements = new List<(ThemeLayer Layer, WallpaperAsset Asset)>();
        var adopted = new Dictionary<string, WallpaperAsset>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        foreach (var missing in MissingAssets)
        {
            if (selected is not null && !selected.Any(i => i.LayerId == missing.LayerId && i.Layout == (missing.Orientation == ThemeOrientation.Portrait ? "竖屏" : "横屏"))) { skipped++; continue; }

            var name = Path.GetFileName(missing.Path);
            if (string.IsNullOrEmpty(name) || !files.TryGetValue(name, out var matches) || matches.Length != 1) { skipped++; continue; }
            var path = matches[0]; var extension = Path.GetExtension(path).ToLowerInvariant();
            if (missing.IsVideo ? extension != ".mp4" : extension is not (".png" or ".jpg" or ".jpeg")) { skipped++; continue; }
            if (!adopted.TryGetValue(path, out var asset))
            {
                try
                {
                    if (!missing.IsVideo) await new DeskDisplay.Display.DataProviders.ImageSceneLoader().LoadAsync(path, ImageDisplayMode.Uniform);
                }
                catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException) { skipped++; continue; }
                asset = await AdoptMediaAsync(path); adopted.Add(path, asset);
            }
            var layout = Project.AllLayouts.FirstOrDefault(l => l.Layers.Any(layer => layer.Id == missing.LayerId && layer.Path == missing.Path)) ?? (missing.Orientation == ThemeOrientation.Portrait ? Project.Portrait : Project.Landscape);
            var layer = layout.Layers.FirstOrDefault(l => l.Id == missing.LayerId);
            if (layer is not null) replacements.Add((layer, asset));
        }
        // Resolve and register every candidate before touching project source: one edit, one undo.
        if (replacements.Count > 0)
        {
            _session.Checkpoint();
            foreach (var (layer, asset) in replacements)
            {
                var cloudId = layer.CloudResourceId.Length > 0 ? layer.CloudResourceId : LibraryAssets.FirstOrDefault(i => i.Asset.Id == layer.AssetId)?.Asset.CloudResourceId ?? "";
                var cloud = layer.AssetOrigin == ThemeAssetOrigin.Cloud || cloudId.Length > 0 || !string.IsNullOrWhiteSpace(asset.CloudResourceId);
                layer.AssetId = asset.Id; layer.Path = asset.Path;
                layer.AssetOrigin = cloud ? ThemeAssetOrigin.Cloud : ThemeAssetOrigin.Local;
                layer.CloudResourceId = cloudId.Length > 0 ? cloudId : asset.CloudResourceId;
            }
            Changed(); await ReloadAssetsAsync();
        }
        await RefreshMissingAssetsAsync();
        Status = $"已修复 {replacements.Count} 个图层引用，跳过 {skipped} 项；可一次撤销。";
        return replacements.Count;
    }
}
