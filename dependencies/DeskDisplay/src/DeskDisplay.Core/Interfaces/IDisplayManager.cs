using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Interfaces;

public interface IDisplayManager : IDisposable
{
    /// <summary>Raised on the creating UI thread after display configuration changes.</summary>
    event EventHandler? DisplaysChanged;

    /// <summary>Returns a new snapshot, or throws on failure. Callers should retain their last snapshot.</summary>
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default);
}
