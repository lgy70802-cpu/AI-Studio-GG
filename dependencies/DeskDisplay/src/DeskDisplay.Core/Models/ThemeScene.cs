using System.ComponentModel;
using DeskDisplay.Core.Adaptation;

namespace DeskDisplay.Core.Models;

/// <summary>Editable composition shared by editor, preview and physical outputs. Decoded children are backend owned.</summary>
public sealed class ThemeScene : Scene, IDisposable
{
    private readonly Dictionary<Guid, Scene> _children = [];
    public ThemeProject Project { get; private set; }
    public ResolvedLayout? ResolvedLayout { get; }
    public DateTimeOffset CurrentTime { get; private set; } = DateTimeOffset.Now;
    public IReadOnlyDictionary<Guid, Scene> Children => _children;
    public bool ContainsVideo => Project.ActiveLayout.Layers.Any(l => l.Visible && l.IsVideo);
    public ISceneImage? OutputFrame { get; private set; }
    public void SetOutputFrame(ISceneImage frame) { OutputFrame = frame; OnPropertyChanged(nameof(OutputFrame)); }
    public ThemeScene(ThemeProject project) { Project = project; SetCanvasSize((int)project.ActiveLayout.Width, (int)project.ActiveLayout.Height); }
    public ThemeScene(ThemeProject project, ResolvedLayout resolved) : this(project)
    {
        ResolvedLayout = resolved;
        SetCanvasSize(resolved.SurfaceSize.Width, resolved.SurfaceSize.Height);
    }
    public void UpdateProject(ThemeProject project) { Project = project; if (ResolvedLayout is null) SetCanvasSize((int)project.ActiveLayout.Width, (int)project.ActiveLayout.Height); OnPropertyChanged(nameof(Project)); }
    public void Tick(DateTimeOffset now) { CurrentTime = now; OnPropertyChanged(nameof(CurrentTime)); }
    public void SetChild(Guid id, Scene child)
    {
        if (_children.Remove(id, out var previous)) previous.PropertyChanged -= OnChildChanged;
        _children[id] = child; child.PropertyChanged += OnChildChanged; OnPropertyChanged(nameof(Children));
    }
    private void OnChildChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Children));
    public void ClearChildren() { foreach (var child in _children.Values) child.PropertyChanged -= OnChildChanged; _children.Clear(); OnPropertyChanged(nameof(Children)); }
    public void Dispose() { ClearChildren(); OutputFrame = null; }
}
