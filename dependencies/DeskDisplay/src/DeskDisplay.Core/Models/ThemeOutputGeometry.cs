namespace DeskDisplay.Core.Models;

public readonly record struct ThemeOutputGeometry(int Width, int Height, int Rotation)
{
    /// <summary>Change design orientation while preserving the configured encoded raster footprint.</summary>
    public static ThemeOutputGeometry Resolve(int width, int height, int rotation, ThemeOrientation orientation)
    {
        if (width < 1 || height < 1 || rotation is not (0 or 90 or 180 or 270) || !Enum.IsDefined(orientation)) throw new ArgumentOutOfRangeException(nameof(width));
        var portrait = orientation == ThemeOrientation.Portrait;
        if (width == height || (height > width) == portrait) return new(width, height, rotation);
        return new(height, width, (rotation + (portrait ? 270 : 90)) % 360);
    }
}
