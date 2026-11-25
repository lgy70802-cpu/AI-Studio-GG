using System.Windows;
using System.Windows.Controls;

namespace DeskDisplay.Display.Views;

/// <summary>Only constrains layout; all visible scene content belongs to the reusable SceneView.</summary>
public sealed class PreviewHost : Decorator
{
    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(
        nameof(AspectRatio), typeof(double), typeof(PreviewHost),
        new FrameworkPropertyMetadata(16d / 9, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double ratio && double.IsFinite(ratio) && ratio > 0);

    public double AspectRatio
    {
        get => (double)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var width = double.IsFinite(constraint.Width) ? constraint.Width : 640;
        var height = double.IsFinite(constraint.Height) ? constraint.Height : width / AspectRatio;
        var fit = Fit(new Size(width, height));
        Child?.Measure(fit);
        return fit;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var fit = Fit(arrangeSize);
        Child?.Arrange(new Rect(new Point((arrangeSize.Width - fit.Width) / 2, (arrangeSize.Height - fit.Height) / 2), fit));
        return arrangeSize;
    }

    private Size Fit(Size available)
    {
        var width = Math.Min(available.Width, available.Height * AspectRatio);
        return new Size(width, width / AspectRatio);
    }
}
