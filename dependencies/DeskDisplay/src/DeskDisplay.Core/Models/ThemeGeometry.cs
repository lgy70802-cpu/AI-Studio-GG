namespace DeskDisplay.Core.Models;

public readonly record struct ThemeBounds(double X, double Y, double Width, double Height);

/// <summary>Pure design-coordinate geometry, shared by future desktop UI adapters.</summary>
public static class ThemeGeometry
{
    public static ThemeBounds Union(IEnumerable<ThemeLayer> layers)
    {
        var bounds = layers.Select(VisualBounds).ToArray(); if (bounds.Length == 0) return new(0, 0, 1, 1);
        var x = bounds.Min(b => b.X); var y = bounds.Min(b => b.Y);
        return new(x, y, bounds.Max(b => b.X + b.Width) - x, bounds.Max(b => b.Y + b.Height) - y);
    }
    public static IReadOnlyList<ThemeLayer>? ScaleGroup(IReadOnlyList<ThemeLayer> layers, ThemeBounds target, bool preserveAspect = true)
    {
        if (layers.Count == 0 || !double.IsFinite(target.X) || !double.IsFinite(target.Y) || !double.IsFinite(target.Width) || !double.IsFinite(target.Height) || target.Width <= 0 || target.Height <= 0) return null;
        var original = Union(layers); var scale = Math.Min(target.Width / original.Width, target.Height / original.Height);
        var scaleX = preserveAspect ? scale : target.Width / original.Width;
        var scaleY = preserveAspect ? scale : target.Height / original.Height;
        var result = new List<ThemeLayer>();
        foreach (var l in layers)
        {
            if (l.Locked) return null;
            var copy = l.Clone();
            if (!preserveAspect && copy.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper) copy.Fill = ImageDisplayMode.Stretch;
            copy.Width *= scaleX; copy.Height *= scaleY; copy.FontSize *= scale; copy.Spacing *= scale;
            copy.X = target.X + (l.X + l.Width / 2 - original.X) * scaleX - copy.Width / 2;
            copy.Y = target.Y + (l.Y + l.Height / 2 - original.Y) * scaleY - copy.Height / 2;
            if (!double.IsFinite(scale) || copy.Width is < 1 or > 16384 || copy.Height is < 1 or > 16384 || copy.FontSize is < 1 or > 2000 || Math.Abs(copy.Spacing) > 200 || Math.Abs(copy.X) > 32768 || Math.Abs(copy.Y) > 32768) return null;
            result.Add(copy);
        }
        return result;
    }
    private static (double X, double Y) Rotate(double x, double y, double degrees)
    {
        var angle = degrees * Math.PI / 180; return (x * Math.Cos(angle) - y * Math.Sin(angle), x * Math.Sin(angle) + y * Math.Cos(angle));
    }
    public static ThemeBounds ResizeCorner(ThemeLayer start, int corner, double pointerX, double pointerY, bool preserveAspect = false)
    {
        if (corner is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(corner));
        var sx = corner is 0 or 2 ? -1 : 1; var sy = corner is 0 or 1 ? -1 : 1;
        var offset = Rotate(-sx * start.Width / 2, -sy * start.Height / 2, start.Rotation);
        var ax = start.X + start.Width / 2 + offset.X; var ay = start.Y + start.Height / 2 + offset.Y;
        var local = Rotate(pointerX - ax, pointerY - ay, -start.Rotation);
        var width = Math.Clamp(sx * local.X, 1, 16384); var height = Math.Clamp(sy * local.Y, 1, 16384);
        if (preserveAspect)
        {
            var factor = Math.Clamp(Math.Max(width / start.Width, height / start.Height), Math.Max(1 / start.Width, 1 / start.Height), Math.Min(16384 / start.Width, 16384 / start.Height));
            width = start.Width * factor; height = start.Height * factor;
        }
        var center = Rotate(sx * width / 2, sy * height / 2, start.Rotation);
        return new(ax + center.X - width / 2, ay + center.Y - height / 2, width, height);
    }
    public static ThemeBounds Snap(ThemeLayer layer, double x, double y, ThemeLayout layout, double tolerance)
    {
        var xGuides = new List<double> { 0, layout.Width / 2, layout.Width };
        var yGuides = new List<double> { 0, layout.Height / 2, layout.Height };
        xGuides.AddRange(layout.VerticalGuides); yGuides.AddRange(layout.HorizontalGuides);
        if (layout.ShowGrid) { xGuides.Add(Math.Round(x / layout.GridSpacing) * layout.GridSpacing); yGuides.Add(Math.Round(y / layout.GridSpacing) * layout.GridSpacing); }
        foreach (var other in layout.Layers.Where(l => l.Id != layer.Id && l.Visible))
        {
            var bounds = Extents(other); xGuides.AddRange([other.X + other.Width / 2 - bounds.Width / 2, other.X + other.Width / 2, other.X + other.Width / 2 + bounds.Width / 2]);
            yGuides.AddRange([other.Y + other.Height / 2 - bounds.Height / 2, other.Y + other.Height / 2, other.Y + other.Height / 2 + bounds.Height / 2]);
        }
        var size = Extents(layer);
        double Adjust(double start, double logicalSize, double visualSize, List<double> guides)
        {
            var offsets = new[] { logicalSize / 2 - visualSize / 2, logicalSize / 2, logicalSize / 2 + visualSize / 2 };
            var delta = guides.SelectMany(g => offsets.Select(o => g - start - o)).OrderBy(Math.Abs).First();
            return start + (Math.Abs(delta) <= tolerance ? delta : 0);
        }
        return new(Adjust(x, layer.Width, size.Width, xGuides), Adjust(y, layer.Height, size.Height, yGuides), layer.Width, layer.Height);
    }
    public static ThemeBounds SnapSize(ThemeLayer original, ThemeBounds bounds, int corner, ThemeLayout layout, double tolerance)
    {
        if (original.Rotation % 360 != 0) return bounds; // Preserve rotated opposite-corner anchoring.
        var left = corner is 0 or 2; var top = corner is 0 or 1;
        var anchorX = left ? original.X + original.Width : original.X;
        var anchorY = top ? original.Y + original.Height : original.Y;
        double Nearest(double v, IEnumerable<double> guides) { var n = guides.OrderBy(g => Math.Abs(g - v)).First(); return Math.Abs(n - v) <= tolerance ? n : v; }
        var x = Nearest(left ? bounds.X : bounds.X + bounds.Width, layout.VerticalGuides.Concat(new[] { 0d, layout.Width / 2, layout.Width }).Concat(layout.ShowGrid ? new[] { Math.Round((left ? bounds.X : bounds.X + bounds.Width) / layout.GridSpacing) * layout.GridSpacing } : []));
        var y = Nearest(top ? bounds.Y : bounds.Y + bounds.Height, layout.HorizontalGuides.Concat(new[] { 0d, layout.Height / 2, layout.Height }).Concat(layout.ShowGrid ? new[] { Math.Round((top ? bounds.Y : bounds.Y + bounds.Height) / layout.GridSpacing) * layout.GridSpacing } : []));
        if ((left ? anchorX - x : x - anchorX) < 1 || (top ? anchorY - y : y - anchorY) < 1) return bounds;
        return new(Math.Min(x, anchorX), Math.Min(y, anchorY), Math.Abs(x - anchorX), Math.Abs(y - anchorY));
    }
    public static ThemeBounds VisualBounds(ThemeLayer layer)
    {
        var (width, height) = Extents(layer);
        return new(layer.X + (layer.Width - width) / 2, layer.Y + (layer.Height - height) / 2, width, height);
    }
    private static (double Width, double Height) Extents(ThemeLayer layer)
    {
        var angle = layer.Rotation * Math.PI / 180; var c = Math.Abs(Math.Cos(angle)); var s = Math.Abs(Math.Sin(angle));
        return (layer.Width * c + layer.Height * s, layer.Width * s + layer.Height * c);
    }
}
