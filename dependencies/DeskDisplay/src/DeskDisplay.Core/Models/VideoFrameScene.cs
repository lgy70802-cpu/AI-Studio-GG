namespace DeskDisplay.Core.Models;

/// <summary>Output-owned video snapshot. The same unrotated pixels feed preview and device encoding.</summary>
public sealed class VideoFrameScene(string path) : Scene
{
    public string Path { get; } = path;
    public ISceneImage? Frame { get; private set; }
    public void Update(ISceneImage frame) { Frame = frame; OnPropertyChanged(nameof(Frame)); }
}
