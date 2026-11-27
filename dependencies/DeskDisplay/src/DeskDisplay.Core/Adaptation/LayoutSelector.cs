using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.Core.Adaptation;

public enum LayoutMatch { Explicit, Exact, WithinTolerance, NeedsAdjustment }
public sealed record LayoutSelection(LayoutProfile Layout, LayoutMatch Match, double RelativeAspectError);

public static class LayoutSelector
{
    /// <summary>Explicit override wins over assignment. Invalid overrides fail instead of silently switching layouts.</summary>
    public static LayoutSelection Select(ThemeDocument theme, PixelSize target, Guid? explicitLayoutId = null,
        Guid? assignedLayoutId = null, double aspectTolerance = .01)
    {
        theme.Validate(); target.Validate();
        if (!double.IsFinite(aspectTolerance) || aspectTolerance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(aspectTolerance));
        double Error(LayoutProfile layout) => Math.Abs(target.AspectRatio / layout.DesignSize.AspectRatio - 1);
        if ((explicitLayoutId ?? assignedLayoutId) is { } requested)
        {
            var layout = theme.Layouts.SingleOrDefault(l => l.LayoutId == requested)
                ?? throw new InvalidOperationException("The requested layout does not exist.");
            if (layout.DesignSize.Orientation != target.Orientation)
                throw new InvalidOperationException("The requested layout has a different orientation; create or explicitly adapt a layout first.");
            return new(layout, LayoutMatch.Explicit, Error(layout));
        }
        var candidates = theme.Layouts.Where(l => l.DesignSize.Orientation == target.Orientation)
            .OrderBy(Error).ThenBy(l => l.LayoutId == theme.DefaultLayoutId ? 0 : 1).ThenBy(l => l.LayoutId).ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException("No layout of the requested orientation exists.");
        var best = candidates[0];
        var error = Error(best);
        return new(best, error <= 1e-12 ? LayoutMatch.Exact : error <= aspectTolerance
            ? LayoutMatch.WithinTolerance : LayoutMatch.NeedsAdjustment, error);
    }
}
