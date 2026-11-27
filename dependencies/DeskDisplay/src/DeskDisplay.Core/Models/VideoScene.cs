using DeskDisplay.Core.Interfaces;

namespace DeskDisplay.Core.Models;

public sealed class VideoScene(string path, IVideoPlaybackService playback) : Scene
{
    public string Path { get; } = path;
    public IVideoPlaybackService Playback { get; } = playback;
}
