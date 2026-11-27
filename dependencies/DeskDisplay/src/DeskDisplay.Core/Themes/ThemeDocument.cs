using System.Collections.Immutable;
using System.Text.Json;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Themes;

public readonly record struct DesignSize(double Width, double Height)
{
    public double AspectRatio => Width / Height;
    public DisplayOrientation Orientation => Width >= Height ? DisplayOrientation.Landscape : DisplayOrientation.Portrait;
    public void Validate()
    {
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
    }
}

public readonly record struct LayerBounds(double X, double Y, double Width, double Height)
{
    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) || !double.IsFinite(Width) || !double.IsFinite(Height) || Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
    }
}

public enum LayoutAdaptation { Contain, Cover, Responsive }
public enum AxisAnchor { Start, Center, End, Relative }

/// <summary>Margin is measured in source design units; relative positions are normalized 0..1.</summary>
public sealed record LayerConstraint(AxisAnchor Horizontal = AxisAnchor.Center, AxisAnchor Vertical = AxisAnchor.Center,
    double RelativeX = .5, double RelativeY = .5, double MinimumMargin = 0);

/// <summary>Configuration owns its JsonElement storage and never exposes a mutable ThemeLayer.</summary>
public sealed record LayerDefinition(Guid Id, ThemeElementKind Kind, LayerBounds Bounds, int ZIndex,
    bool Visible, bool Locked, double Opacity, double Rotation, JsonElement Configuration,
    LayerConstraint? Constraint = null)
{
    public static LayerDefinition FromLegacy(ThemeLayer layer) => new(layer.Id, layer.Kind,
        new(layer.X, layer.Y, layer.Width, layer.Height), layer.ZIndex, layer.Visible, layer.Locked,
        layer.Opacity, layer.Rotation, JsonSerializer.SerializeToElement(layer));

    public ThemeLayer CreateLegacyLayer() => Configuration.Deserialize<ThemeLayer>()
        ?? throw new InvalidDataException("Missing layer configuration.");
}

public sealed record LayoutProfile(Guid LayoutId, string Name, long Revision, DesignSize DesignSize,
    string Background, ImmutableArray<LayerDefinition> Layers, JsonElement EditorMetadata,
    Guid? DerivedFromLayoutId = null)
{
    public void Validate()
    {
        DesignSize.Validate();
        if (LayoutId == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Revision < 1 || string.IsNullOrWhiteSpace(Background)
            || Layers.IsDefault || Layers.Length > 100 || Layers.Select(l => l.Id).Distinct().Count() != Layers.Length)
            throw new InvalidDataException("Invalid layout identity, revision or layers.");
        foreach (var layer in Layers)
        {
            layer.Bounds.Validate();
            if (layer.Id == Guid.Empty || !Enum.IsDefined(layer.Kind) || !double.IsFinite(layer.Opacity) || layer.Opacity is < 0 or > 1
                || !double.IsFinite(layer.Rotation) || layer.Configuration.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Invalid layer snapshot.");
            var configuration = layer.CreateLegacyLayer();
            if (configuration.Id != layer.Id || configuration.Kind != layer.Kind || !Enum.IsDefined(configuration.Fill) || configuration.FontWeightValue is < 100 or > 900 || configuration.FontStretchValue is < 1 or > 9 || !Enum.IsDefined(configuration.ClockAppearance))
                throw new InvalidDataException("Layer configuration identity or content mode does not match its definition.");
            if (layer.Constraint is { } c && (!Enum.IsDefined(c.Horizontal) || !Enum.IsDefined(c.Vertical)
                || !double.IsFinite(c.RelativeX) || !double.IsFinite(c.RelativeY) || c.RelativeX is < 0 or > 1 || c.RelativeY is < 0 or > 1
                || !double.IsFinite(c.MinimumMargin) || c.MinimumMargin < 0))
                throw new InvalidDataException("Invalid layout constraint.");
        }
    }
}

/// <summary>Portable V2 contract. No device identity or active output state belongs here.</summary>
public sealed record ThemeDocument(int SchemaVersion, Guid ThemeId, long Revision, string Name,
    string Category, DateTimeOffset ModifiedAt, Guid DefaultLayoutId, ImmutableArray<LayoutProfile> Layouts)
{
    public void Validate()
    {
        if (SchemaVersion != 2 || ThemeId == Guid.Empty || Revision < 1 || string.IsNullOrWhiteSpace(Name)
            || Layouts.IsDefaultOrEmpty || Layouts.Length > 64 || Layouts.Select(l => l.LayoutId).Distinct().Count() != Layouts.Length
            || !Layouts.Any(l => l.LayoutId == DefaultLayoutId)) throw new InvalidDataException("Invalid theme document.");
        foreach (var layout in Layouts) layout.Validate();
    }
}


