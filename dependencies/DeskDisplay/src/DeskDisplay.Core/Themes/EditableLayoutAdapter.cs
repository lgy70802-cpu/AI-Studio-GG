using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Themes;

/// <summary>Bakes a resolved uniform transform into editable design coordinates, never into pixels.</summary>
public static class EditableLayoutAdapter
{
    /// <summary>Explicit background-only cover. Foreground geometry retains the selected uniform adaptation.</summary>
    public static NamedThemeLayout CaptureWithFilledBackground(ThemeDocument document, ResolvedLayout resolved, string name)
    {
        var source = document.Layouts.Single(l => l.LayoutId == resolved.LayoutId);
        var background = source.Layers.Where(l => l.Visible && l.Rotation == 0 && l.Kind is ThemeElementKind.Wallpaper or ThemeElementKind.Image)
            .Where(l => { var b = l.Bounds; return b.X <= .01 && b.Y <= .01 && b.X + b.Width >= source.DesignSize.Width - .01 && b.Y + b.Height >= source.DesignSize.Height - .01 && !l.CreateLegacyLayer().IsVideo; })
            .OrderBy(l => l.ZIndex).FirstOrDefault()
            ?? throw new InvalidOperationException("没有覆盖原画布的背景图片。请先将背景图片覆盖整个画布，再生成铺满布局。");
        var result = Capture(document, resolved, name);
        var layer = result.Layout.Layers.Single(l => l.Id == background.Id);
        layer.X = 0; layer.Y = 0; layer.Width = resolved.SurfaceSize.Width; layer.Height = resolved.SurfaceSize.Height;
        layer.Fill = ImageDisplayMode.Fill;
        return result;
    }
    public static NamedThemeLayout Capture(ThemeDocument document, ResolvedLayout resolved, string name)
    {
        var source = document.Layouts.Single(l => l.LayoutId == resolved.LayoutId);
        var layout = new ThemeLayout { Width = resolved.SurfaceSize.Width, Height = resolved.SurfaceSize.Height, Background = source.Background };
        foreach (var item in resolved.Layers)
        {
            var layer = item.Source.CreateLegacyLayer();
            layer.X = item.Bounds.X; layer.Y = item.Bounds.Y; layer.Width = item.Bounds.Width; layer.Height = item.Bounds.Height;
            layer.FontSize *= item.ContentScale; layer.Spacing *= item.ContentScale;
            layout.Layers.Add(layer);
        }
        var result = new NamedThemeLayout(Guid.NewGuid(), name, 1, layout, source.LayoutId);
        var validation = new ThemeProject { Version = 2, LayoutVersions = [result] }; validation.Validate();
        return result;
    }
}
