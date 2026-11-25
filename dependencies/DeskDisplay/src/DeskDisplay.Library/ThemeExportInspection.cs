using System.Text;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed record ThemeExportIssue(string Layout, string Layer, string Status, string Path);
public static class ThemeExportInspection
{
    public static IReadOnlyList<ThemeExportIssue> Inspect(ThemeProject source, IReadOnlyDictionary<Guid, WallpaperAsset> catalog)
    {
        source.Validate(); var rows = new List<ThemeExportIssue>();
        foreach (var (name, layout) in new[] { ("横屏", source.Landscape), ("竖屏", source.Portrait) }.Concat(source.LayoutVersions.Select(v => (v.Name, v.Layout))))
            foreach (var layer in layout.Layers)
            {
                if (layer.Kind is ThemeElementKind.StockPlugin or ThemeElementKind.CameraClock)
                { rows.Add(new(name, layer.Name, "插件依赖：源参数保留；插件程序及全局配置不随包迁移，目标端需安装并启用", "")); continue; }
                if (layer.Kind is not (ThemeElementKind.Image or ThemeElementKind.Wallpaper)) continue;
                var asset = layer.AssetId is Guid id ? catalog.GetValueOrDefault(id) : null;
                var path = asset?.Path ?? layer.Path;
                var cloud = layer.AssetOrigin == ThemeAssetOrigin.Cloud || layer.CloudResourceId.Length > 0 || !string.IsNullOrWhiteSpace(asset?.CloudResourceId);
                var local = !cloud && (asset is not null || layer.AssetOrigin == ThemeAssetOrigin.Local);
                var status = cloud ? "云端引用：不打包，目标端需恢复账号权益及素材" : !local ? "未知来源：不打包" : "本地素材：可打包";
                if (!File.Exists(path)) status = status.Replace("可打包", "不可打包") + "；文件缺失";
                else
                {
                    var size = new FileInfo(path).Length;
                    if (size <= 0 || size > ThemeTransferService.MaximumFileBytes) status = status.Replace("可打包", "不可打包") + "；文件大小不符合打包限制";
                    if (Path.GetExtension(path).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".mp4")) status = status.Replace("可打包", "不可打包") + "；打包格式不支持";
                }
                if (layer.IsVideo) status += "；视频依赖系统解码，D92 USB不支持";
                rows.Add(new(name, layer.Name, status, path));
            }
        return rows;
    }
    public static string Markdown(ThemeProject source, IReadOnlyList<ThemeExportIssue> rows)
    {
        static string Escape(string text) => text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        var text = new StringBuilder("# 主题导出检查\n\n"); text.AppendLine(Escape(source.Name));
        text.AppendLine("\n检查横竖布局及隐藏图层。仅检查引用和文件状态，不代表权益授权、媒体解码或设备能力验收。单文件上限128 MiB、素材去重后总量上限512 MiB，由实际打包流程再次校验。字体需在目标电脑安装；插件与全局配置不包含在主题包内。源项目导出不包含任何素材；下表打包状态针对本地素材包。\n");
        text.AppendLine("| 布局 | 图层 | 检查结果 | 路径 |\n|---|---|---|---|");
        foreach (var r in rows) text.AppendLine($"| {Escape(r.Layout)} | {Escape(r.Layer)} | {Escape(r.Status)} | {Escape(r.Path)} |");
        if (rows.Count == 0) text.AppendLine("\n没有图片、视频或插件外部引用。");
        return text.ToString();
    }
}
