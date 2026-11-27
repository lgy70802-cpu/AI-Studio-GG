using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Interfaces;

/// <summary>UI-thread-owned lifecycle for a single external render window.</summary>
public interface IRenderWindowService : IDisposable
{
    bool IsOpen { get; }
    Scene? CurrentScene { get; }
    string Status { get; }
    event EventHandler? StateChanged;
    void ShowTest(DisplayInfo target);
    void ShowScene(DisplayInfo target, Scene scene);
    void SynchronizeTarget(DisplayInfo? target);
    void Close();
}
