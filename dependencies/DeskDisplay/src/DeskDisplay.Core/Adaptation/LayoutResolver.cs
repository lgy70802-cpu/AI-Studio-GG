using System.Collections.Immutable;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.Core.Adaptation;

public sealed record LayoutDiagnostic(string Code, Guid? LayerId);
public sealed record ResolvedLayer(LayerDefinition Source, LayerBounds Bounds, double ContentScale);
public sealed record ResolvedLayout(Guid LayoutId, long Revision, PixelSize SurfaceSize,
    LayoutAdaptation Adaptation, double UniformScale, double OffsetX, double OffsetY,
    ImmutableArray<ResolvedLayer> Layers, ImmutableArray<LayoutDiagnostic> Diagnostics)
{
    public bool NeedsAdjustment => !Diagnostics.IsEmpty;
}

/// <summary>Pure geometry. All output coordinates are surface pixels, never HID encoding pixels or UI DIPs.</summary>
public static class LayoutResolver
{
    public static ResolvedLayout Resolve(LayoutProfile source, PixelSize target, LayoutAdaptation adaptation = LayoutAdaptation.Contain)
    {
        source.Validate(); target.Validate();
        if (!Enum.IsDefined(adaptation)) throw new ArgumentOutOfRangeException(nameof(adaptation));
        var scale = adaptation == LayoutAdaptation.Cover
            ? Math.Max(target.Width / source.DesignSize.Width, target.Height / source.DesignSize.Height)
            : Math.Min(target.Width / source.DesignSize.Width, target.Height / source.DesignSize.Height);
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(source), "Unrepresentable output scale.");
        var offsetX = (target.Width - source.DesignSize.Width * scale) / 2;
        var offsetY = (target.Height - source.DesignSize.Height * scale) / 2;
        var layers = ImmutableArray.CreateBuilder<ResolvedLayer>();
        var diagnostics = ImmutableArray.CreateBuilder<LayoutDiagnostic>();
        if (source.DesignSize.Orientation != target.Orientation) diagnostics.Add(new("OrientationMismatch", null));
        foreach (var layer in source.Layers)
        {
            var b = layer.Bounds;
            var width = b.Width * scale; var height = b.Height * scale;
            var x = b.X * scale + offsetX; var y = b.Y * scale + offsetY;
            if (adaptation == LayoutAdaptation.Responsive && layer.Constraint is { } c)
            {
                x = Position(c.Horizontal, b.X, b.Width, source.DesignSize.Width, target.Width, scale, c.RelativeX);
                y = Position(c.Vertical, b.Y, b.Height, source.DesignSize.Height, target.Height, scale, c.RelativeY);
                var margin = c.MinimumMargin * scale;
                if (width + margin * 2 > target.Width || height + margin * 2 > target.Height)
                    diagnostics.Add(new("UnsatisfiedMinimumMargin", layer.Id));
                else
                {
                    x = Math.Clamp(x, margin, target.Width - width - margin);
                    y = Math.Clamp(y, margin, target.Height - height - margin);
                }
            }
            // No source mutation and no ScaleX/ScaleY stretching, including old free-resized images.
            var legacy = layer.CreateLegacyLayer();
            if (layer.Visible && (layer.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper) && legacy.Fill == ImageDisplayMode.Stretch)
                diagnostics.Add(new("LegacyStretchRequiresReview", layer.Id));
            if (layer.Visible)
            {
                var radians = layer.Rotation * Math.PI / 180;
                var rotatedWidth = Math.Abs(width * Math.Cos(radians)) + Math.Abs(height * Math.Sin(radians));
                var rotatedHeight = Math.Abs(width * Math.Sin(radians)) + Math.Abs(height * Math.Cos(radians));
                var left = x + (width - rotatedWidth) / 2; var top = y + (height - rotatedHeight) / 2;
                if (left < -1e-7 || top < -1e-7 || left + rotatedWidth > target.Width + 1e-7 || top + rotatedHeight > target.Height + 1e-7)
                    diagnostics.Add(new("ContentOutsideSurface", layer.Id));
            }
            layers.Add(new(layer, new(x, y, width, height), scale));
        }
        return new(source.LayoutId, source.Revision, target, adaptation, scale, offsetX, offsetY,
            layers.ToImmutable(), diagnostics.ToImmutable());
    }

    private static double Position(AxisAnchor anchor, double position, double extent, double designExtent,
        double targetExtent, double scale, double relative) => anchor switch
    {
        AxisAnchor.Start => position * scale,
        AxisAnchor.End => targetExtent - (designExtent - position) * scale,
        AxisAnchor.Relative => relative * (targetExtent - extent * scale),
        _ => (targetExtent - designExtent * scale) / 2 + position * scale
    };
}
