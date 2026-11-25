using System.IO;
using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed record CreatorPreflightIssue(Guid? LayerId, string Message, bool Blocking)
{
    public string Label => (Blocking ? "需处理 · " : "提示 · ") + Message;
}
public sealed partial class CreatorViewModel
{
    public IReadOnlyList<CreatorPreflightIssue> InspectTheme(bool includeLegacyTarget = false)
    {
        var issues = new List<CreatorPreflightIssue>(); var layout = Project.ActiveLayout;
        foreach (var layer in layout.Layers.Where(l => l.Visible))
        {
            var bounds = ThemeGeometry.VisualBounds(layer);
            if (bounds.X < -.01 || bounds.Y < -.01 || bounds.X + bounds.Width > layout.Width + .01 || bounds.Y + bounds.Height > layout.Height + .01) issues.Add(new(layer.Id, "画布外元素：" + layer.Name, false));
            if (layer.Kind == ThemeElementKind.Text && Overflow(layer) is { Length: > 0 } warning) issues.Add(new(layer.Id, layer.Name + "：" + warning, false));
            if (layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper)
            {
                var path = LibraryAssets.FirstOrDefault(a => a.Asset.Id == layer.AssetId)?.Asset.Path ?? layer.Path;
                if (!File.Exists(path) || !Scene.Children.ContainsKey(layer.Id)) issues.Add(new(layer.Id, "素材缺失或未加载：" + layer.Name, true));
            }
            if (layer.Kind == ThemeElementKind.StockPlugin && _main.Plugins?.Enabled != true || layer.Kind == ThemeElementKind.CameraClock && _main.Plugins?.CameraEnabled != true) issues.Add(new(layer.Id, "插件未启用：" + layer.Name, true));
        }
        if (includeLegacyTarget) foreach (var message in CheckThemeApplication().Problems) if (!issues.Any(i => i.Message == message)) issues.Add(new(null, message, true));
        return issues;
    }
    public void LocatePreflightIssue(Guid layerId)
    {
        if (!Project.ActiveLayout.Layers.Any(l => l.Id == layerId)) return;
        var layer = Project.ActiveLayout.Layers.Single(l => l.Id == layerId);
        if (layer.GroupId is Guid group) _collapsedGroups.Remove(group);
        _selection.Clear(); _selection.Add(layerId); _selectedId = layerId; LeftTabIndex = 1; RefreshLayers(); NotifySelection(); FocusCanvasSelection?.Invoke(this, EventArgs.Empty);
    }
}
