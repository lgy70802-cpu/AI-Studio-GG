using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

internal sealed class VirtualResourceGrid : Canvas
{
    readonly ScrollViewer scroll;
    readonly int count;
    double cellWidth, cellHeight;
    readonly double ratio;
    readonly Func<double>? responsiveWindowWidth;
    readonly Func<int,double,FrameworkElement>? responsiveCard;
    const double Gap=18;
    public VirtualResourceGrid(ScrollViewer scroll,int count,double ratio,Func<double> windowWidth,Func<int,double,FrameworkElement> create)
        : this(scroll,count,300,120,i=>create(i,282))
    { this.ratio=ratio; responsiveCard=create; responsiveWindowWidth=windowWidth; }
    readonly Func<int, FrameworkElement> createCard;
    readonly Dictionary<int, FrameworkElement> realized = [];
    bool pending;

    public VirtualResourceGrid(ScrollViewer scroll, int count, double cellWidth, double cellHeight,
        Func<int, FrameworkElement> createCard)
    {
        this.scroll = scroll;
        this.count = count;
        this.cellWidth = cellWidth;
        this.cellHeight = cellHeight;
        this.createCard = createCard;
        var estimatedColumns = Math.Max(1, CardRowFit.Count(Math.Max(0, scroll.ViewportWidth - 48), cellWidth, count));
        Height = (count + estimatedColumns - 1) / estimatedColumns * cellHeight;
        Loaded += (_, _) => { scroll.ScrollChanged += ScrollChanged; ScheduleUpdate(); };
        Unloaded += (_, _) => scroll.ScrollChanged -= ScrollChanged;
        SizeChanged += (_, change) => { if (change.WidthChanged) ScheduleUpdate(); };
    }

    void ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, scroll)) ScheduleUpdate();
    }

    void ScheduleUpdate()
    {
        if (pending) return;
        pending = true;
        _ = Dispatcher.InvokeAsync(() =>
        {
            pending = false;
            if (IsLoaded && IsDescendantOf(scroll)) RealizeViewport();
        }, DispatcherPriority.Loaded);
    }

    void RealizeViewport()
    {
        if (ActualWidth <= 0) return;
        var columns = Math.Max(1, CardRowFit.Count(ActualWidth, cellWidth, count));
        if (responsiveCard != null)
        {
            var windowWidth = responsiveWindowWidth?.Invoke() ?? ActualWidth;
            var landscapeColumns=windowWidth>1100?3:windowWidth>760?2:1;
            var span=Math.Max(1,(ActualWidth-Gap*(landscapeColumns-1))/landscapeColumns);
            var width=ratio<1?span*ratio:span;
            if(Math.Abs(cellWidth-width-Gap)>.01) { Children.Clear(); realized.Clear(); }
            cellWidth=width+Gap; cellHeight=width/ratio+Gap;
            columns=ratio<1?Math.Max(1,(int)((ActualWidth+Gap)/cellWidth)):landscapeColumns;
        }
        var rows = (count + columns - 1) / columns;
        var height = rows * cellHeight - (responsiveCard==null?0:Gap);
        if (Height != height) Height = height;
        var top = TransformToAncestor(scroll).Transform(new Point()).Y;
        var (firstRow, endRow) = CardRowFit.VisibleRows(scroll.ViewportHeight, top, cellHeight, rows);
        var first = firstRow * columns;
        var end = Math.Min(count, endRow * columns);
        foreach (var index in realized.Keys.Where(index => index < first || index >= end).ToArray())
        {
            Children.Remove(realized[index]);
            realized.Remove(index);
        }
        for (var index = first; index < end; index++)
        {
            if (!realized.TryGetValue(index, out var card))
            {
                card = responsiveCard==null?createCard(index):responsiveCard(index,cellWidth-Gap);
                realized.Add(index, card);
                Children.Add(card);
            }
            SetLeft(card, index % columns * cellWidth);
            SetTop(card, index / columns * cellHeight);
        }
    }
}
