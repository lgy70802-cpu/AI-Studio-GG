using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace MiraBox.App;

public partial class MainWindow
{
    void AddHomeArtworkOverlay(Grid card, string name, string type, double? loss, bool stacked = false)
    {
        var shade = new Border
        {
            Height = card.Height / 2, VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(Color.FromArgb(199, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90),
            IsHitTestVisible = false
        };
        card.Children.Add(shade);
        var nameLabel = HomeText(name, 13, bold: true);
        nameLabel.TextWrapping = TextWrapping.NoWrap;
        nameLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        nameLabel.Foreground = Brushes.White;
        var typeLabel = HomeText(type, 12);
        typeLabel.Foreground = Brushes.White;
        typeLabel.Margin = stacked ? new Thickness(0, 3, 0, 0) : new Thickness(8, 0, 0, 0);
        FrameworkElement header;
        if (stacked)
        {
            var labels = new StackPanel();
            labels.Children.Add(nameLabel);
            labels.Children.Add(typeLabel);
            header = labels;
        }
        else header = HomeHeader(nameLabel, typeLabel);
        header.Margin = new Thickness(10, 8, 10, 0);
        header.VerticalAlignment = VerticalAlignment.Top;
        header.IsHitTestVisible = false;
        card.Children.Add(header);
        if (loss is null || loss > .05)
        {
            var label = HomeText(loss == null ? "尺寸未知" : $"留边约 {loss:P0}", 12);
            card.Children.Add(new Border
            {
                Child = label, CornerRadius = new CornerRadius(11), Background = Brushes.White,
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(8, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            });
        }
    }

    FrameworkElement HomeRecentPlaceholder(double width, double height)
    {
        var dash = new Rectangle
        {
            RadiusX = 18, RadiusY = 18, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 5, 4 }, Fill = (Brush)FindResource("SearchFillBrush")
        };
        dash.SetResourceReference(Shape.StrokeProperty, "PlaceholderLineBrush");
        var label = HomeText("使用过的内容会出现在这里", 13, muted: true);
        label.TextAlignment = TextAlignment.Center;
        label.Margin = new Thickness(10, 0, 10, 0);
        var card = new Grid { Width = width, Height = height };
        card.Children.Add(dash);
        card.Children.Add(label);
        return card;
    }

    void AddCardFocusOutline(Grid card)
    {
        var ring = new Border
        {
            BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(14),
            Margin = new Thickness(4), IsHitTestVisible = false
        };
        ring.SetResourceReference(Border.BorderBrushProperty, "SearchFocusBrush");
        var halo = new Border
        {
            BorderThickness = new Thickness(4), CornerRadius = new CornerRadius(18),
            Child = ring, IsHitTestVisible = false, Visibility = Visibility.Collapsed
        };
        halo.SetResourceReference(Border.BorderBrushProperty, "FocusHaloBrush");
        card.Children.Add(halo);
        card.IsKeyboardFocusedChanged += (_, _) =>
            halo.Visibility = card.IsKeyboardFocused ? Visibility.Visible : Visibility.Collapsed;
    }

    Border FrameHomeArtwork(Grid card)
    {
        var movement = new TranslateTransform();
        var shadow = ((DropShadowEffect)FindResource("PanelShadow")).Clone();
        var frame = new Border { Child = card, Effect = shadow, RenderTransform = movement };
        frame.MouseEnter += (_, _) => Animate(true);
        frame.MouseLeave += (_, _) => Animate(false);
        return frame;

        void Animate(bool raised)
        {
            var duration = TimeSpan.FromMilliseconds(180);
            movement.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(raised ? -2 : 0, duration));
            shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(raised ? 12 : 6, duration));
            shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(raised ? 28 : 18, duration));
            shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(raised ? .20 : .10, duration));
        }
    }
}
