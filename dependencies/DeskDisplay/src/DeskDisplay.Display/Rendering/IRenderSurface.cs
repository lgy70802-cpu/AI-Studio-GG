using DeskDisplay.Core.Models;

namespace DeskDisplay.Display.Rendering;

internal interface IRenderSurface : IDisposable
{
    event EventHandler? Closed;
    event Action<Exception>? Faulted;
    event EventHandler? TopologyInvalidated;
    void ShowOnDisplay(DisplayInfo target);
    void Render(Scene scene);
}
