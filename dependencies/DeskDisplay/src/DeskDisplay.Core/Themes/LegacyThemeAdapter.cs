using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Themes;

/// <summary>In-memory read adapter only. Never saves or changes the source V1 project.</summary>
public static class LegacyThemeAdapter
{
    public static ThemeDocument Read(ThemeProject source)
    {
        source.Validate();
        var landscape = Convert(source.Id, "landscape", source.Landscape);
        var portrait = Convert(source.Id, "portrait", source.Portrait);
        return new(2, source.Id, 1, source.Name, source.Category, source.ModifiedAt,
            source.SelectedLayoutId ?? (source.Orientation == ThemeOrientation.Landscape ? landscape.LayoutId : portrait.LayoutId),
            [landscape, portrait, .. source.LayoutVersions.Select(v => Convert(source.Id, v.Id.ToString(), v.Layout) with { LayoutId = v.Id, Name = v.Name, Revision = v.Revision, DerivedFromLayoutId = v.DerivedFrom })]);
    }

    private static LayoutProfile Convert(Guid themeId, string role, ThemeLayout layout)
    {
        // Deterministic across machines; this is an identity, not a security signature.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"DeskDisplay.Layout.v1:{themeId:D}:{role}"));
        var id = new Guid(bytes.AsSpan(0, 16));
        var metadata = JsonSerializer.SerializeToElement(new
        {
            layout.ShowGrid, layout.GridSpacing, layout.VerticalGuides, layout.HorizontalGuides
        });
        return new(id, role, 1, new(layout.Width, layout.Height), layout.Background,
            layout.Layers.Select(LayerDefinition.FromLegacy).ToImmutableArray(), metadata);
    }
}
