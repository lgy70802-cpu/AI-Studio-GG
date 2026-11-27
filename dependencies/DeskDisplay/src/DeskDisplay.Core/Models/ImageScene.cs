namespace DeskDisplay.Core.Models;

public enum ImageDisplayMode { Fill, Uniform, Stretch, Center }

/// <summary>Renderer-neutral image dimensions. The display backend owns decoded pixels.</summary>
public interface ISceneImage
{
    int OriginalWidth { get; }
    int OriginalHeight { get; }
}

public sealed class ImageScene(string path, ISceneImage image, ImageDisplayMode mode) : Scene
{
    public string Path { get; } = path;
    public ISceneImage Image { get; } = image;
    public ImageDisplayMode Mode { get; } = mode;
}
