using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Interfaces;

/// <summary>UI-thread-owned target selection. Mutations are synchronous; await SaveAsync to commit them.</summary>
public interface ITargetDisplayService
{
    TargetDisplaySettings? SavedTarget { get; }
    bool NeedsSave { get; }
    DisplayInfo? GetTargetDisplay();
    void SetTargetDisplay(DisplayInfo display);
    DisplayInfo? ResolveSavedTargetDisplay();
    void ClearTargetDisplay();
    void UpdateDisplays(IReadOnlyList<DisplayInfo> displays);
    void MarkDisplaysUnavailable();
    /// <summary>On failure the selection rolls back to its last persisted value.</summary>
    Task<bool> SaveAsync();
}
