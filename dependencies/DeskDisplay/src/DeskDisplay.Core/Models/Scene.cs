using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeskDisplay.Core.Models;

/// <summary>Shared, bindable scene state. Dimensions describe the logical canvas, not a WPF window.</summary>
public abstract class Scene : INotifyPropertyChanged
{
    private int _pixelWidth = 1600;
    private int _pixelHeight = 900;
    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;
    public double AspectRatio => (double)_pixelWidth / _pixelHeight;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCanvasSize(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Scene dimensions must be positive.");
        if (_pixelWidth == width && _pixelHeight == height) return;
        _pixelWidth = width;
        _pixelHeight = height;
        OnPropertyChanged(nameof(PixelWidth));
        OnPropertyChanged(nameof(PixelHeight));
        OnPropertyChanged(nameof(AspectRatio));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
