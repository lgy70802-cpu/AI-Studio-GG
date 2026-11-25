namespace MiraBox.App;

public sealed record StudioPlaylistEntry(string ThemeId, double DurationSeconds);
public sealed record StudioPlaylistDefinition(string Id, string DeviceId, bool Portrait, bool Shuffle, StudioPlaylistEntry[] Items);

public sealed class StudioPlaylistProgress
{
    public string Serial { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public string? ThemeId { get; set; }
    public int Index { get; set; }
    public bool Running { get; set; }
    public double RemainingSeconds { get; set; }
    public double DurationSeconds { get; set; } = 30;
    public string? Message { get; set; }

    // Same duration bounds and non-repeating random selection used by V67.
    public static double Duration(double value) => double.IsFinite(value) ? Math.Clamp(value, 10, 86400) : 30;
    public int Choose(StudioPlaylistDefinition playlist, ISet<string> available, int direction, Func<int, int>? random = null)
    {
        var eligible = Enumerable.Range(0, playlist.Items.Length).Where(i => available.Contains(playlist.Items[i].ThemeId)).ToArray();
        if (eligible.Length == 0) return -1;
        var current = Array.FindIndex(playlist.Items, item => item.ThemeId == ThemeId);
        if (playlist.Shuffle)
        {
            var candidates = eligible.Where(i => i != current).ToArray();
            if (candidates.Length == 0) return eligible[0];
            return candidates[(random ?? Random.Shared.Next)(candidates.Length)];
        }
        if (current < 0)
        {
            var position = Math.Clamp(Index, 0, Math.Max(0, playlist.Items.Length - 1));
            return eligible.FirstOrDefault(i => i >= position, eligible[0]);
        }
        return Enumerable.Range(1, playlist.Items.Length)
            .Select(offset => (current + Math.Sign(direction) * offset + playlist.Items.Length * 2) % playlist.Items.Length)
            .First(eligible.Contains);
    }
    public void Commit(StudioPlaylistDefinition playlist, int index)
    {
        Index = index;
        ThemeId = playlist.Items[index].ThemeId;
        DurationSeconds = Duration(playlist.Items[index].DurationSeconds);
        RemainingSeconds = DurationSeconds;
        Message = null;
    }
    public void ReconcileDuration(double duration)
    {
        duration = Duration(duration);
        if (duration == DurationSeconds) return;
        var elapsed = Math.Max(0, DurationSeconds - RemainingSeconds);
        DurationSeconds = duration;
        RemainingSeconds = Math.Max(0, duration - elapsed);
    }
    public void Advance(double seconds, bool canAdvance)
    {
        if (Running && canAdvance && double.IsFinite(seconds) && seconds > 0)
            RemainingSeconds = Math.Max(0, RemainingSeconds - seconds);
    }
}
