namespace DeskDisplay.Core.Interfaces;

/// <summary>One UI-thread-owned playback session, shared by all scene hosts.</summary>
public interface IVideoPlaybackService : IDisposable
{
    string Status { get; }
    bool IsPlaying { get; }
    int FramesPerSecond { get; }
    event EventHandler? StateChanged;
    Task OpenAsync(string path, int framesPerSecond, CancellationToken cancellationToken = default);
    void Stop();
}
