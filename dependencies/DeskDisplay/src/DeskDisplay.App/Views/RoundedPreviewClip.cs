using System.Windows;
using System.Windows.Media;

namespace DeskDisplay.App.Views;

public static class RoundedPreviewClip
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached("Radius", typeof(double), typeof(RoundedPreviewClip), new PropertyMetadata(0d, Changed));
    public static double GetRadius(DependencyObject value) => (double)value.GetValue(RadiusProperty);
    public static void SetRadius(DependencyObject value, double radius) => value.SetValue(RadiusProperty, radius);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not FrameworkElement element) return;
        element.SizeChanged -= Resized;
        element.SizeChanged += Resized;
        Update(element);
    }
    private static void Resized(object sender, SizeChangedEventArgs e) => Update((FrameworkElement)sender);
    private static void Update(FrameworkElement element)
    {
        var radius = GetRadius(element);
        if (!double.IsFinite(radius) || radius <= 0) { element.ClearValue(UIElement.ClipProperty); return; }
        element.Clip = new RectangleGeometry(new Rect(element.RenderSize), radius, radius);
    }
}
