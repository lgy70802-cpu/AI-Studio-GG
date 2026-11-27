using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed record ThemeEditorViewport(Guid ProjectId, ThemeOrientation Orientation, double Zoom, double Horizontal, double Vertical, Guid[] Selection, Guid? FocusedLayer = null, Guid? LayoutId = null)
{
    public ThemeWorkbenchState? Workbench { get; init; }
    public void Validate()
    {
        Workbench?.Validate();
        if (ProjectId == Guid.Empty || !Enum.IsDefined(Orientation) || !double.IsFinite(Zoom) || Zoom is < .25 or > 3 || !double.IsFinite(Horizontal) || Horizontal is < 0 or > 1 || !double.IsFinite(Vertical) || Vertical is < 0 or > 1 || Selection is null || Selection.Length > 100 || Selection.Any(i => i == Guid.Empty) || FocusedLayer is Guid focus && !Selection.Contains(focus)) throw new InvalidDataException("编辑视图状态无效。");
    }
}
public sealed record ThemeWorkbenchState(bool LeftOpen, bool RightOpen, bool Guides, int LeftTab, string Material, string[] ExpandedSections, Guid[] CollapsedGroups)
{
    public void Validate()
    {
        if (LeftTab is < 0 or > 1 || Material is null || Material.Length > 40 || ExpandedSections is null || ExpandedSections.Length > 40 || ExpandedSections.Any(s => s is null || s.Length > 80) || CollapsedGroups is null || CollapsedGroups.Length > 100) throw new InvalidDataException("工作区状态无效。");
    }
}
public sealed partial class ThemeProjectStore
{
    private string ViewportPath(Guid id, ThemeOrientation orientation, Guid? layoutId = null) => Path.Combine(_directory, "EditorState", id.ToString("N") + "-" + (int)orientation + (layoutId is Guid layout ? "-" + layout.ToString("N") : "") + ".json");
    public async Task<ThemeEditorViewport?> ReadViewportAsync(Guid id, ThemeOrientation orientation, Guid? layoutId = null)
    {
        var path = ViewportPath(id, orientation, layoutId); await _gate.WaitAsync();
        try
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 16000) throw new InvalidDataException("编辑视图状态文件过大。");
            var state = JsonSerializer.Deserialize<ThemeEditorViewport>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("编辑视图为空。");
            state.Validate(); if (state.ProjectId != id || state.Orientation != orientation || state.LayoutId != layoutId) throw new InvalidDataException("编辑视图标识不符。"); return state;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { Serilog.Log.Warning(e, "Cannot read editor viewport {Path}; preserving file", path); return null; }
        finally { _gate.Release(); }
    }
    public async Task SaveViewportAsync(ThemeEditorViewport state)
    {
        state.Validate(); var json = JsonSerializer.Serialize(state); var path = ViewportPath(state.ProjectId, state.Orientation, state.LayoutId); string? temporary = null;
        await _gate.WaitAsync();
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; await File.WriteAllTextAsync(temporary, json); File.Move(temporary, path, true); }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }
}
