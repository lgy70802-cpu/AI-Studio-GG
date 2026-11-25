namespace DeskDisplay.Core.Models;

public sealed record ThemeSpacing(double X1, double Y1, double X2, double Y2, double Distance);

/// <summary>Nearest non-overlapping neighbor gaps in design coordinates; hidden/selected layers are excluded.</summary>
public static class ThemeMeasurements
{
    public static IReadOnlyList<ThemeSpacing> Nearest(ThemeBounds selection, IEnumerable<ThemeLayer> others)
    {
        var horizontal = new List<ThemeSpacing>(); var vertical = new List<ThemeSpacing>();
        foreach (var layer in others.Where(l => l.Visible))
        {
            var b = ThemeGeometry.VisualBounds(layer);
            var top = Math.Max(selection.Y, b.Y); var bottom = Math.Min(selection.Y + selection.Height, b.Y + b.Height);
            var left = Math.Max(selection.X, b.X); var right = Math.Min(selection.X + selection.Width, b.X + b.Width);
            if (bottom > top)
            {
                var y = (top + bottom) / 2;
                if (b.X >= selection.X + selection.Width) horizontal.Add(new(selection.X + selection.Width, y, b.X, y, b.X - selection.X - selection.Width));
                else if (selection.X >= b.X + b.Width) horizontal.Add(new(b.X + b.Width, y, selection.X, y, selection.X - b.X - b.Width));
            }
            if (right > left)
            {
                var x = (left + right) / 2;
                if (b.Y >= selection.Y + selection.Height) vertical.Add(new(x, selection.Y + selection.Height, x, b.Y, b.Y - selection.Y - selection.Height));
                else if (selection.Y >= b.Y + b.Height) vertical.Add(new(x, b.Y + b.Height, x, selection.Y, selection.Y - b.Y - b.Height));
            }
        }
        return new[] { horizontal.MinBy(v => v.Distance), vertical.MinBy(v => v.Distance) }.OfType<ThemeSpacing>().ToArray();
    }
}
