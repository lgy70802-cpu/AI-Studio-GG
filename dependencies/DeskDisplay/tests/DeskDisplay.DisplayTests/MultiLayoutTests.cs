using System.IO;
using System.Text.Json;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Themes;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using DeskDisplay.Library;
using DeskDisplay.Infrastructure.Configuration;

namespace DeskDisplay.DisplayTests;

internal static class MultiLayoutTests
{
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-layouts-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var store = new ThemeProjectStore(directory);
            var old = new ThemeProject(); old.Landscape.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, FontSize = 100, X = 120, Width = 600, Height = 140, ShowSeconds = true });
            await store.SaveAsync(old);
            var path = Path.Combine(directory, old.Id.ToString("N") + ".theme.json"); var originalBytes = await File.ReadAllBytesAsync(path);
            var project = await store.ReadAsync(path); var document = LegacyThemeAdapter.Read(project);
            Check(project.Version == 1 && project.LayoutVersions.Count == 0 && originalBytes.SequenceEqual(await File.ReadAllBytesAsync(path)), "Reading V1 project preserves original file and default layouts");
            var target = new PixelSize(3840, 960); var resolved = LayoutResolver.Resolve(document.Layouts[0], target);
            var version = EditableLayoutAdapter.Capture(document, resolved, "4K横屏");
            Check(version.Layout.Layers[0].FontSize == 200 && version.Layout.Layers[0].X == 240 && version.Layout.Width == 3840, "Baked adaptation preserves uniform geometry and editable clock configuration");
            project.Version = 2; project.LayoutVersions.Add(version); project.SelectedLayoutId = version.Id; project.Validate();
            await store.SaveAsync(project); var restored = await store.ReadAsync(path);
            Check(restored.Version == 2 && restored.ActiveLayout.Width == 3840 && restored.LayoutVersions[0].Name == "4K横屏", "V2 layout identity name and active selection survive disk save and reopen");
            Check(restored.Landscape.Width == 1920 && restored.Landscape.Layers[0].FontSize == 100, "Saving adapted layout preserves original layout coordinates");
            var clone = restored.Clone(); clone.ActiveLayout.Layers[0].X = 999;
            Check(restored.ActiveLayout.Layers[0].X == 240, "Layout and layer snapshots do not alias editor history");
            var history = new ThemeEditSession(restored); history.Checkpoint(); restored.LayoutVersions.Clear(); restored.SelectedLayoutId = null; history.Undo();
            Check(history.Project.LayoutVersions.Count == 1 && history.Project.ActiveLayout.Width == 3840, "Undo restores removed layout and active layout identity");
            var converted = LegacyThemeAdapter.Read(history.Project);
            Check(converted.Layouts.Length == 3 && LayoutSelector.Select(converted, target).Layout.LayoutId == version.Id, "Output selector sees saved custom layouts and prefers selected matching layout");
            var state = new ThemeEditorViewport(project.Id, ThemeOrientation.Landscape, 1, .2, .3, [], LayoutId: version.Id);
            await store.SaveViewportAsync(state); await store.SaveViewportAsync(state with { LayoutId = null, Zoom = 2 });
            Check((await store.ReadViewportAsync(project.Id, ThemeOrientation.Landscape, version.Id))?.Zoom == 1 && (await store.ReadViewportAsync(project.Id, ThemeOrientation.Landscape))?.Zoom == 2, "Named and default layouts retain independent viewport state");
            var image = Path.Combine(directory, "source.png"); await File.WriteAllBytesAsync(image, [1, 2, 3]);
            project.ActiveLayout.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = image });
            Check(ThemeExportInspection.Inspect(project, new Dictionary<Guid, WallpaperAsset>()).Any(r => r.Layout == version.Name), "Export inspection includes assets in custom layout versions");
            var prefs = new DevicePreferenceStore(Path.Combine(directory, "devices.json"));
            await prefs.SaveAsync([new("a", "USB", 1920, 462, 90, "桌面屏"), new("b", "显示器", 1920, 462, 0, "桌面屏")]);
            Check((await prefs.LoadAsync()).All(p => p.PhysicalGroup == "桌面屏"), "Physical endpoint association survives preference save and load");
            var registry = new DeviceRegistry();
            DeviceDescriptor Device(string id) => new(id, "VirtualDisplayTarget", DeviceTransport.Virtual, new("test", id, IdentityConfidence.VerifiedStable), new(id, id, 0), true);
            registry.Replace(DeviceTransport.Virtual, [Device("a"), Device("b"), Device("c")]);
            var leases = new InMemoryDeviceLeaseProvider();
            await using var sessions = new OutputSessionManager(registry, leases, (_, _) => ValueTask.FromResult<IOutputSessionDriver>(new Driver()));
            var profile = new DisplayProfile("test", target, target, 0, new(CapabilityEvidence.Declared, CapabilityEvidence.Declared, CapabilityEvidence.Unknown));
            var first = await sessions.StartAsync("a", profile, converted, conflictGroupId: "desk");
            var conflict = await sessions.StartAsync("b", profile, converted, conflictGroupId: "DESK");
            var other = await sessions.StartAsync("c", profile, converted, conflictGroupId: "other");
            Check(first.Started && !conflict.Started && conflict.Code == "PhysicalDeviceBusy" && other.Started, "Physical group atomically excludes second endpoint without blocking another device");
            await sessions.StopAsync("a");
            Check((await sessions.StartAsync("b", profile, converted, conflictGroupId: "desk")).Started && sessions.Snapshot.Any(s => s.Snapshot.DeviceId == "c" && s.Snapshot.DesiredState == OutputDesiredState.Running), "Stopping endpoint releases group and preserves unrelated output");
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class Driver : IOutputSessionDriver
    {
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken ct) { report(OutputActualState.Streaming); await Task.Delay(Timeout.Infinite, ct); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
