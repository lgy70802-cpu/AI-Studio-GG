using System.Collections.Immutable;
using System.Text.Json;
using DeskDisplay.AdaptationTests;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;

var checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException(name);
    checks++; Console.WriteLine($"PASS: {name}");
}
void Throws<T>(Action action, string name) where T : Exception
{
    try { action(); } catch (T) { Check(true, name); return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}: {name}");
}
bool Near(double actual, double expected) => Math.Abs(actual - expected) < 1e-7;

var original = new ThemeProject
{
    Name = "Migration fixture",
    Landscape = new ThemeLayout
    {
        Width = 1920, Height = 480, ShowGrid = true, VerticalGuides = [100], HorizontalGuides = [50],
        Layers = [new ThemeLayer
        {
            Kind = ThemeElementKind.DigitalClock, X = 100, Y = 40, Width = 400, Height = 100,
            ShowSeconds = true, FontSize = 75, Spacing = 2, GroupId = Guid.NewGuid(), AssetId = Guid.NewGuid(),
            Path = "relative/clock.png", AssetOrigin = ThemeAssetOrigin.Cloud, CloudResourceId = "existing-cloud-id"
        }]
    },
    Portrait = new ThemeLayout { Width = 480, Height = 1920, Layers = [new ThemeLayer { Kind = ThemeElementKind.Text, Text = "Portrait" }] }
};
var originalJson = JsonSerializer.Serialize(original);
var theme = LegacyThemeAdapter.Read(original);
theme.Validate();
Check(theme.SchemaVersion == 2 && theme.ThemeId == original.Id && theme.Layouts.Length == 2, "V1 becomes one V2 document with two layouts");
Check(theme.Layouts[0].LayoutId == LegacyThemeAdapter.Read(original).Layouts[0].LayoutId, "Stable layout IDs across repeated adaptation");
Check(theme.Layouts[0].LayoutId != theme.Layouts[1].LayoutId, "Landscape and portrait have independent layout IDs");
Check(theme.DefaultLayoutId == theme.Layouts[0].LayoutId, "V1 orientation becomes default layout preference");
var portraitSource = original.Clone(); portraitSource.Orientation = ThemeOrientation.Portrait;
Check(LegacyThemeAdapter.Read(portraitSource).DefaultLayoutId == theme.Layouts[1].LayoutId, "Portrait default survives read adaptation");
Check(JsonSerializer.Serialize(theme.Layouts[0].Layers[0].CreateLegacyLayer()) == JsonSerializer.Serialize(original.Landscape.Layers[0]), "Every legacy layer field survives including cloud reference, font and group");
Check(theme.Layouts[0].EditorMetadata.GetProperty("VerticalGuides")[0].GetDouble() == 100, "Guides preserved as immutable metadata");
original.Landscape.Layers[0].Text = "changed after snapshot";
original.Landscape.VerticalGuides[0] = 999;
Check(theme.Layouts[0].Layers[0].CreateLegacyLayer().Text != "changed after snapshot", "Layer snapshot detached from mutable V1");
Check(theme.Layouts[0].EditorMetadata.GetProperty("VerticalGuides")[0].GetDouble() == 100, "Metadata detached from source collections");
original = JsonSerializer.Deserialize<ThemeProject>(originalJson)!;
var restored = JsonSerializer.Deserialize<ThemeDocument>(JsonSerializer.Serialize(theme))!; restored.Validate();
Check(restored.Layouts[0].LayoutId == theme.Layouts[0].LayoutId, "V2 contract JSON round trip");
Throws<InvalidDataException>(() => LegacyThemeAdapter.Read(WithVersion(original.Clone(), 99)), "Unknown legacy versions rejected");

var layout = theme.Layouts[0];
var themeJson = JsonSerializer.Serialize(theme);
var exact = LayoutSelector.Select(theme, new(3840, 960));
Check(exact.Match == LayoutMatch.Exact, "Same ratio different resolution is exact");
Check(LayoutSelector.Select(theme, new(1920, 462)).Match == LayoutMatch.NeedsAdjustment, "4:1 is not the actual D92 ratio within 1 percent");
var actualLayout = layout with { LayoutId = Guid.NewGuid(), DesignSize = new(1920, 462) };
var actualTheme = theme with { DefaultLayoutId = actualLayout.LayoutId, Layouts = [actualLayout, theme.Layouts[1]] };
Check(LayoutSelector.Select(actualTheme, new(2557, 615)).Match == LayoutMatch.WithinTolerance, "2557x615 matches actual D92 ratio within tolerance");
Check(LayoutSelector.Select(actualTheme, new(2557, 615), aspectTolerance: 0).Match == LayoutMatch.NeedsAdjustment, "Zero tolerance does not round unequal ratios to exact");
Check(LayoutSelector.Select(theme, new(480, 1920)).Layout.LayoutId == theme.Layouts[1].LayoutId, "Portrait selects independent portrait layout");
Check(LayoutSelector.Select(theme, new(1920, 480), layout.LayoutId).Match == LayoutMatch.Explicit, "Explicit layout override respected");
Check(LayoutSelector.Select(theme, new(1920, 480), layout.LayoutId, Guid.NewGuid()).Layout == layout, "Explicit override wins over saved assignment");
Throws<InvalidOperationException>(() => LayoutSelector.Select(theme, new(1920, 480), assignedLayoutId: Guid.NewGuid()), "Invalid saved assignment is not silently replaced");
Throws<InvalidOperationException>(() => LayoutSelector.Select(theme, new(1920, 480), Guid.NewGuid()), "Missing override cannot silently fall back");
Throws<InvalidOperationException>(() => LayoutSelector.Select(theme, new(1920, 480), theme.Layouts[1].LayoutId), "Wrong orientation override rejected");
Throws<ArgumentOutOfRangeException>(() => LayoutSelector.Select(theme, new(1920, 480), aspectTolerance: double.NaN), "NaN tolerance rejected");
Throws<ArgumentOutOfRangeException>(() => LayoutSelector.Select(theme, new(0, 480)), "Invalid pixel dimensions rejected");
var duplicate = layout with { LayoutId = Guid.NewGuid() };
var tied = theme with { DefaultLayoutId = duplicate.LayoutId, Layouts = [layout, duplicate] };
Check(LayoutSelector.Select(tied, new(1920, 480)).Layout.LayoutId == duplicate.LayoutId, "Default layout breaks equal ratio ties deterministically");

var scaled = LayoutResolver.Resolve(layout, new(3840, 960));
Check(Near(scaled.UniformScale, 2) && scaled.OffsetX == 0 && scaled.OffsetY == 0, "Same aspect doubles all geometry uniformly");
Check(scaled.Layers[0].Bounds == new LayerBounds(200, 80, 800, 200), "Resolved layer position and dimensions in target pixels");
var contain = LayoutResolver.Resolve(layout, new(1920, 1080));
Check(contain.OffsetY == 300 && contain.UniformScale == 1, "Contain preserves all content with centered letterboxing");
var full = layout with { Layers = [LayerDefinition.FromLegacy(new ThemeLayer { Kind = ThemeElementKind.Image, Width = 1920, Height = 480 })] };
var cover = LayoutResolver.Resolve(full, new(1920, 1080), LayoutAdaptation.Cover);
Check(Near(cover.UniformScale, 2.25) && cover.OffsetX == -1200, "Cover uses uniform scaling and explicit centered crop");
Check(cover.Diagnostics.Any(d => d.Code == "ContentOutsideSurface"), "Cover reports clipped content");
Check(LayoutResolver.Resolve(layout, new(480, 1920)).Diagnostics.Any(d => d.Code == "OrientationMismatch"), "Cross orientation preview is explicitly diagnosed");
var anchored = layout with { Layers = [layout.Layers[0] with { Constraint = new(AxisAnchor.End, AxisAnchor.Start) }] };
var responsive = LayoutResolver.Resolve(anchored, new(2400, 480), LayoutAdaptation.Responsive);
Check(responsive.Layers[0].Bounds.X == 580 && responsive.Layers[0].Bounds.Y == 40, "Responsive end anchor preserves source right margin");
Check(responsive.Layers[0].Bounds.Width == 400 && responsive.Layers[0].Bounds.Height == 100, "Responsive content never stretches");
var relative = anchored with { Layers = [anchored.Layers[0] with { Constraint = new(AxisAnchor.Relative, AxisAnchor.Relative, 1, 0) }] };
Check(LayoutResolver.Resolve(relative, new(2400, 480), LayoutAdaptation.Responsive).Layers[0].Bounds.X == 2000, "Normalized relative position resolves against available space");
var impossible = anchored with { Layers = [anchored.Layers[0] with { Constraint = new(MinimumMargin: 500) }] };
Check(LayoutResolver.Resolve(impossible, new(1920, 480), LayoutAdaptation.Responsive).Diagnostics.Any(d => d.Code == "UnsatisfiedMinimumMargin"), "Impossible margins produce diagnostics");
var margin = relative with { Layers = [relative.Layers[0] with { Constraint = new(AxisAnchor.Relative, AxisAnchor.Relative, 0, 0, 20) }] };
Check(LayoutResolver.Resolve(margin, new(1920, 480), LayoutAdaptation.Responsive).Layers[0].Bounds.X == 20, "Feasible minimum margin clamps placement in design units");
var stretch = full with { Layers = [LayerDefinition.FromLegacy(new ThemeLayer { Kind = ThemeElementKind.Image, Fill = ImageDisplayMode.Stretch })] };
Check(LayoutResolver.Resolve(stretch, new(1920, 480)).Diagnostics.Any(d => d.Code == "LegacyStretchRequiresReview"), "Legacy stretch is preserved but flagged for review");
Check(stretch.Layers[0].CreateLegacyLayer().Fill == ImageDisplayMode.Stretch, "Adaptation never silently rewrites legacy fill");
var rotated = layout with { Layers = [LayerDefinition.FromLegacy(new ThemeLayer { Kind = ThemeElementKind.Text, Width = 100, Height = 100, Rotation = 45 })] };
Check(LayoutResolver.Resolve(rotated, new(1920, 480)).Diagnostics.Any(d => d.Code == "ContentOutsideSurface"), "Rotated extents included in clipping diagnostics");
Check(JsonSerializer.Serialize(theme) == themeJson && JsonSerializer.Serialize(original) == originalJson, "Selection and adaptation leave source bytes unchanged");
Throws<ArgumentOutOfRangeException>(() => LayoutResolver.Resolve(layout with { DesignSize = new(double.NaN, 480) }, new(1920, 480)), "Invalid logical dimensions rejected");
Throws<InvalidDataException>(() => (theme with { SchemaVersion = 99 }).Validate(), "Future document version rejected");
Throws<InvalidDataException>(() => (theme with { Layouts = [layout, layout] }).Validate(), "Duplicate layout IDs rejected");
Throws<InvalidDataException>(() => (layout with { Layers = [layout.Layers[0] with { Id = Guid.NewGuid() }] }).Validate(), "Mismatched configuration identity rejected");
var detached = layout.Layers[0].CreateLegacyLayer(); detached.Text = "detached";
Check(layout.Layers[0].CreateLegacyLayer().Text != "detached", "Legacy materialization never exposes mutable snapshot storage");

var capabilities = new DisplayCapabilities(CapabilityEvidence.HardwareVerified, CapabilityEvidence.Unknown, CapabilityEvidence.Unknown);
var profile = new DisplayProfile("actual-landscape", new(1920, 462), new(462, 1920), 90, capabilities);
profile.Validate();
Check(profile.Capabilities.Video == CapabilityEvidence.Unknown, "JPEG evidence never implies video capability");
Throws<ArgumentException>(() => (profile with { ContentToTransportRotation = 0 }).Validate(), "Encoded dimensions must agree with explicit rotation");
Throws<ArgumentOutOfRangeException>(() => (profile with { ContentToTransportRotation = 45 }).Validate(), "Unsupported rotation rejected");

DeviceDescriptor Device(string serial) => new($"virtual:{serial}", "D92-test", DeviceTransport.Virtual,
    new("virtual/D92", serial, IdentityConfidence.VerifiedStable), new($"ep:{serial}", "test-only", 1), true);
var da = Device("A"); var db = Device("B");
Check(DeviceIdentityRules.GetStableId(da.Identity) != DeviceIdentityRules.GetStableId(db.Identity), "Same model different serials have distinct stable identities");
Check(!DeviceIdentityRules.RequiresConfirmation(da, [db, da]), "Discovery order does not affect unique identity");
Check(DeviceIdentityRules.RequiresConfirmation(da, [da, da with { Endpoint = new("other", "other", 1) }]), "Duplicate serial requires manual confirmation");
Check(DeviceIdentityRules.RequiresConfirmation(da with { Identity = da.Identity with { SerialNumber = null } }, [da, db]), "Missing serial never picks first device");
Check(DeviceIdentityRules.GetStableId(da.Identity with { Confidence = IdentityConfidence.UserConfirmed }) is null, "User alias does not fabricate stable hardware identity");

await using var a = new VirtualDisplayTarget(da, new("wide", new(1920, 480), new(1920, 480), 0, capabilities));
await using var b = new VirtualDisplayTarget(db, new("large", new(3840, 960), new(3840, 960), 0, capabilities));
var frameA = LayoutResolver.Resolve(layout, a.Profile.SurfaceSize);
var frameB = LayoutResolver.Resolve(layout, b.Profile.SurfaceSize);
var timestamp = DateTimeOffset.UnixEpoch;
await Task.WhenAll(a.PresentAsync(frameA, timestamp, default).AsTask(), b.PresentAsync(frameB, timestamp, default).AsTask());
Check(a.Frames == 1 && b.Frames == 1 && a.LastFrame!.SurfaceSize != b.LastFrame!.SurfaceSize, "Two virtual devices present same theme at independent resolutions");
var otherProject = original.Clone(); otherProject.Id = Guid.NewGuid(); otherProject.Landscape.Layers[0].Kind = ThemeElementKind.Text; otherProject.Landscape.Layers[0].Text = "Different theme";
var otherTheme = LegacyThemeAdapter.Read(otherProject);
await a.PresentAsync(LayoutResolver.Resolve(otherTheme.Layouts[0], a.Profile.SurfaceSize), timestamp, default);
Check(a.LastFrame!.LayoutId != b.LastFrame!.LayoutId && b.LastFrame.LayoutId == layout.LayoutId, "Different themes on virtual devices do not overwrite each other");
a.SetConnected(false);
Throws<IOException>(() => a.PresentAsync(frameA, timestamp, default).GetAwaiter().GetResult(), "Virtual unplug fails only its own target");
await b.PresentAsync(frameB, timestamp.AddSeconds(1), default);
Check(b.Frames == 2 && a.Frames == 2, "Other virtual target continues after unplug");
a.SetConnected(true);
await a.PresentAsync(frameA, timestamp.AddSeconds(2), default);
Check(a.Frames == 3 && a.Device.Endpoint.ConnectionEpoch == 3, "Manual virtual reconnect advances endpoint epoch");
await a.DisposeAsync();
Throws<ObjectDisposedException>(() => a.PresentAsync(frameA, timestamp, default).GetAwaiter().GetResult(), "Stopped virtual target cannot present");
for (var tick = 0; tick < 10000; tick++) await b.PresentAsync(frameB, timestamp.AddSeconds(tick), default);
Check(b.Frames == 10002 && ReferenceEquals(b.LastFrame, frameB), "Ten thousand simulated ticks retain only last frame, not a queue");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
Throws<OperationCanceledException>(() => b.PresentAsync(frameB, timestamp, cancelled.Token).GetAwaiter().GetResult(), "Virtual cancellation respected");
Check(b.Frames == 10002, "Cancellation and stopping A leave B state intact");
Throws<ArgumentException>(() => b.PresentAsync(frameA, timestamp, default).GetAwaiter().GetResult(), "Target rejects frames for another resolution");
Check(JsonSerializer.Serialize(theme) == themeJson, "Virtual multi-target output preserves theme snapshot");
await SessionTests.RunAsync(Check);
Console.WriteLine($"All {checks} adaptation/session checks passed. Virtual targets only; no HID, real rendering or wall-clock long-run validation.");

// Preserve validation behavior without editing any real project or settings file.
static ThemeProject WithVersion(ThemeProject source, int version) { source.Version = version; return source; }
