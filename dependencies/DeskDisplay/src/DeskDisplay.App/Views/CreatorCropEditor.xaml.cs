using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;

namespace DeskDisplay.App.Views;

public partial class CreatorCropEditor : Window
{
    private readonly BitmapSource _image;
    private readonly double _originalAspect;
    private bool _dragging;
    private Point _last;
    private double _renderWidth, _renderHeight;
    public double Zoom => ZoomSlider.Value;
    public double CropX { get; private set; }
    public double CropY { get; private set; }
    public bool RestoreAspect { get; private set; }
    public CreatorCropEditor(ThemeLayer layer, BitmapSource image)
    {
        InitializeComponent(); _image = image; _originalAspect = layer.Width / layer.Height; Picture.Source = image; CropX = layer.CropX; CropY = layer.CropY; Host.AspectRatio = layer.Width / layer.Height; ZoomSlider.Value = layer.ImageZoom;
        Closed += (_, _) => { Picture.Source = null; }; Viewport.LostMouseCapture += (_, _) => _dragging = false;
        Loaded += (_, _) => LayoutImage();
    }
    private void LayoutImage()
    {
        if (_image is null || Viewport is null || ZoomSlider is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0) return;
        var scale = Math.Max(Viewport.ActualWidth / _image.PixelWidth, Viewport.ActualHeight / _image.PixelHeight) * Zoom;
        _renderWidth = _image.PixelWidth * scale; _renderHeight = _image.PixelHeight * scale;
        Picture.Width = _renderWidth; Picture.Height = _renderHeight;
        System.Windows.Controls.Canvas.SetLeft(Picture, (Viewport.ActualWidth - _renderWidth) * CropX);
        System.Windows.Controls.Canvas.SetTop(Picture, (Viewport.ActualHeight - _renderHeight) * CropY);
    }
    private void Refresh(object sender, SizeChangedEventArgs e) => LayoutImage();
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => LayoutImage();
    private void Down(object sender, MouseButtonEventArgs e) { _dragging = true; _last = e.GetPosition(Viewport); if (!Viewport.CaptureMouse()) { _dragging = false; return; } e.Handled = true; }
    public void PanImage(Vector delta)
    {
        if (!double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) return;
        if (_renderWidth > Viewport.ActualWidth + .01) CropX = Math.Clamp(CropX - delta.X / (_renderWidth - Viewport.ActualWidth), 0, 1);
        if (_renderHeight > Viewport.ActualHeight + .01) CropY = Math.Clamp(CropY - delta.Y / (_renderHeight - Viewport.ActualHeight), 0, 1);
        LayoutImage();
    }
    private void Move(object sender, MouseEventArgs e) { if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return; var p = e.GetPosition(Viewport); PanImage(p - _last); _last = p; }
    private void Up(object sender, MouseButtonEventArgs e) { _dragging = false; Viewport.ReleaseMouseCapture(); }
    private void Reset(object sender, RoutedEventArgs e) { RestoreAspect = false; Host.AspectRatio = _originalAspect; CropX = CropY = .5; ZoomSlider.Value = 1; LayoutImage(); }
    private void OriginalAspect(object sender, RoutedEventArgs e) { Reset(sender, e); RestoreAspect = true; Host.AspectRatio = (double)_image.PixelWidth / _image.PixelHeight; LayoutImage(); }
    private void Apply(object sender, RoutedEventArgs e) => DialogResult = true;
}
