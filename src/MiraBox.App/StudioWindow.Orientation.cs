namespace MiraBox.App;

public sealed partial class StudioWindow
{
    internal sealed record OrientationUse(string Serial, bool Portrait, string ThemeId, long UsedAt);
    readonly Dictionary<(string Serial, bool Portrait), SavedDevice> orientationOutputs = new();
    readonly List<OrientationUse> orientationHistory = new();

    void RememberOrientationOutputs()
    {
        foreach (var current in saved.Values)
        {
            var device = temporaryThemes.TryGetValue(current.Serial, out var preview) ? preview.Previous : current;
            if (device.ThemeId == null) continue;
            var key = (device.Serial, device.Portrait);
            var changed = !orientationOutputs.TryGetValue(key, out var previous) || previous.ThemeId != device.ThemeId;
            orientationOutputs[key] = device;
            if (!changed && orientationHistory.Any(h => h.Serial == device.Serial && h.Portrait == device.Portrait)) continue;
            orientationHistory.RemoveAll(h => h.Serial == device.Serial && h.Portrait == device.Portrait && h.ThemeId == device.ThemeId);
            orientationHistory.Insert(0, new(device.Serial, device.Portrait, device.ThemeId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            var overflow = orientationHistory.Where(h => h.Serial == device.Serial && h.Portrait == device.Portrait).Skip(30).ToArray();
            foreach (var item in overflow) orientationHistory.Remove(item);
        }
    }
}
