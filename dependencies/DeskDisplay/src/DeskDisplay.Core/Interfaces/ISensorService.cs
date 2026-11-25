using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Interfaces;

public interface ISensorService
{
    SensorSnapshot Current { get; }
    Task StartAsync();
    Task StopAsync();
}
