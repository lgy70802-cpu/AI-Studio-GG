namespace DeskDisplay.Core.Interfaces;

/// <summary>Optional transport controls; consumers do not depend on a particular media backend.</summary>
public interface IVideoTransport
{
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    bool IsPaused { get; }
    void Pause();
    void Resume();
    void Seek(TimeSpan position);
}
