using System.IO;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    private bool _insertingImages;
    public async Task InsertImageFilesAsync(IEnumerable<string> paths, double x, double y, bool background = false)
    {
        if (!CanEdit || _insertingImages || !double.IsFinite(x) || !double.IsFinite(y)) return;
        _insertingImages = true;
        var project = Project; var layout = Project.ActiveLayout;
        try
        {
            var count = 0;
            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).Take(100))
            {
                if (_disposed || !ReferenceEquals(Project, project) || !ReferenceEquals(Project.ActiveLayout, layout) || !CanEdit) return;
                if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()) || !File.Exists(path)) { Status = "仅支持现有资源库支持的 PNG / JPEG 图片。"; continue; }
                var image = await new ImageSceneLoader().LoadAsync(path, ImageDisplayMode.Uniform);
                var asset = await AdoptMediaAsync(path);
                if (_disposed || !ReferenceEquals(Project, project) || !ReferenceEquals(Project.ActiveLayout, layout) || !CanEdit) return;
                if (background && layout.Layers.FirstOrDefault(l => l.Kind == ThemeElementKind.Wallpaper) is { } old)
                {
                    if (old.Locked) { Status = "背景已锁定，请先解锁。"; return; }
                    SelectedLayer = old; await ReplaceSelectedAssetAsync(asset); Status = "背景已替换，位置与填充方式保留。"; return;
                }
                if (layout.Layers.Count >= 100) { Status = "最多支持100个图层。"; return; }
                var ratio = (double)image.Image.OriginalWidth / image.Image.OriginalHeight;
                var width = background ? layout.Width : Math.Min(layout.Width * .5, layout.Height * .7 * ratio);
                var height = background ? layout.Height : width / ratio;
                Insert(new ThemeLayer { Kind = background ? ThemeElementKind.Wallpaper : ThemeElementKind.Image, Name = asset.Name, AssetId = asset.Id, Path = asset.Path, Fill = ImageDisplayMode.Uniform, Width = width, Height = height, X = background ? 0 : Math.Clamp(x + count * 20, 0, Math.Max(0, layout.Width - width)), Y = background ? 0 : Math.Clamp(y + count * 20, 0, Math.Max(0, layout.Height - height)), AssetOrigin = string.IsNullOrEmpty(asset.CloudResourceId) ? ThemeAssetOrigin.Local : ThemeAssetOrigin.Cloud, CloudResourceId = asset.CloudResourceId }, asBackground: background);
                await PendingOperation; count++; Status = $"已添加 {count} 张图片，可撤销；原文件保持引用。";
                if (background) return;
            }
        }
        catch (Exception exception) { _logger.Warning(exception, "Cannot insert canvas image"); Status = "图片添加失败：" + exception.Message; }
        finally { _insertingImages = false; }
    }
}
