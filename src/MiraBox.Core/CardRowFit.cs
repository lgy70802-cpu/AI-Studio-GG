namespace MiraBox.Core;

public static class CardRowFit
{
    public static int Count(double availableWidth, double cardWidthWithMargins, int candidates)
    {
        if (!double.IsFinite(availableWidth) || !double.IsFinite(cardWidthWithMargins) ||
            availableWidth <= 0 || cardWidthWithMargins <= 0 || candidates <= 0) return 0;
        return (int)Math.Min(candidates, Math.Floor(availableWidth / cardWidthWithMargins));
    }

    public static (int First, int End) VisibleRows(double viewportHeight, double gridTop, double rowHeight, int rows)
    {
        if (viewportHeight <= 0 || rowHeight <= 0 || rows <= 0 || gridTop >= viewportHeight ||
            gridTop + rows * rowHeight <= 0) return (0, 0);
        var first = Math.Max(0, (int)Math.Floor(-gridTop / rowHeight) - 1);
        var end = Math.Clamp((int)Math.Ceiling((viewportHeight - gridTop) / rowHeight) + 1, first, rows);
        return (first, end);
    }
}
