using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

internal sealed class HomeCardGrid : Grid
{
    const int LandscapeColumns = 3;
    const double Gap = 12;
    const double PortraitGap = 18;
    readonly StackPanel rows = new();
    readonly int candidates;
    readonly Func<int, double, FrameworkElement> createCard;
    readonly Func<double, FrameworkElement>? createPlaceholder;
    readonly double? portraitCardWidth;
    double cardWidth;

    public int VisibleResourceCount { get; private set; }

    public HomeCardGrid(int candidates, double? portraitCardWidth, Func<int, double, FrameworkElement> createCard,
        Func<double, FrameworkElement>? createPlaceholder = null)
    {
        this.candidates = candidates;
        this.portraitCardWidth = portraitCardWidth;
        this.createCard = createCard;
        this.createPlaceholder = createPlaceholder;
        Children.Add(rows);
        Loaded += (_, _) => UpdateCards();
        SizeChanged += (_, change) => { if (change.WidthChanged) UpdateCards(); };
    }

    void UpdateCards()
    {
        if (portraitCardWidth is { } portraitWidth)
        {
            var row = rows.Children.Count == 0 ? AddRow() : (StackPanel)rows.Children[0];
            var count = CardRowFit.Count(ActualWidth + PortraitGap, portraitWidth + PortraitGap, candidates);
            if (row.Children.Count > VisibleResourceCount) row.Children.RemoveAt(VisibleResourceCount);
            while (row.Children.Count > count) row.Children.RemoveAt(row.Children.Count - 1);
            while (row.Children.Count < count) AddCard(row, createCard(row.Children.Count, portraitWidth));
            VisibleResourceCount = count;
            if (createPlaceholder != null && count == candidates)
            {
                var width = Math.Min(portraitWidth, ActualWidth);
                if (width > 0 && (candidates == 0 || FitsMore(count, portraitWidth)))
                    AddCard(row, createPlaceholder(width));
            }
            return;
        }

        var widthPerCard = Math.Floor((ActualWidth - Gap * (LandscapeColumns - 1)) / LandscapeColumns);
        if (widthPerCard <= 0 || cardWidth == widthPerCard) return;
        cardWidth = widthPerCard;
        rows.Children.Clear();
        for (var first = 0; first < candidates; first += LandscapeColumns)
        {
            var row = AddRow();
            for (var index = first; index < Math.Min(candidates, first + LandscapeColumns); index++)
                AddCard(row, createCard(index, cardWidth));
        }
        VisibleResourceCount = candidates;
        if (createPlaceholder != null && candidates < LandscapeColumns)
        {
            var row = rows.Children.Count == 0 ? AddRow() : (StackPanel)rows.Children[0];
            AddCard(row, createPlaceholder(cardWidth));
        }
    }

    bool FitsMore(int count, double width) =>
        CardRowFit.Count(ActualWidth + PortraitGap, width + PortraitGap, count + 1) > count;

    StackPanel AddRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, rows.Children.Count == 0 ? 0 : Gap, 0, 0)
        };
        rows.Children.Add(row);
        return row;
    }

    void AddCard(StackPanel row, FrameworkElement card)
    {
        if (row.Children.Count > 0) card.Margin = new Thickness(portraitCardWidth != null ? PortraitGap : Gap, 0, 0, 0);
        row.Children.Add(card);
    }
}
