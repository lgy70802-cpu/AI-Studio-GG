namespace DeskDisplay.Core.Models;

public enum DigitalClockAppearance { Standard, SevenSegment, SmallSeconds, Stacked }
public sealed record ClockComponentPreset(string Key, string Name, string Font, bool Bold = false, double Spacing = 0, DigitalClockAppearance Appearance = DigitalClockAppearance.Standard, bool Seconds = false, bool Hour12 = false, int Weight = 400, int Stretch = 5)
{
    public ThemeLayer Create() => new() { Kind = ThemeElementKind.DigitalClock, Name = Name, FontFamily = Font, Bold = Bold, FontWeightValue = Weight, FontStretchValue = Stretch, Spacing = Spacing, ClockAppearance = Appearance, ShowSeconds = Seconds, Hour12 = Hour12 };
}
public static class ClockComponentPresets
{
    public static IReadOnlyList<ClockComponentPreset> All { get; } =
    [
        new("mono", "极简等宽", "Consolas"),
        new("light", "纤细无衬线", "Segoe UI", Weight: 300),
        new("bold", "浑厚数字", "Arial", true, Weight: 800),
        new("serif", "优雅衬线", "Georgia"),
        new("narrow", "窄体数字", "Bahnschrift", Spacing: -3, Stretch: 3),
        new("wide", "宽距数字", "Consolas", Spacing: 8),
        new("segments", "数码管", "Consolas", Appearance: DigitalClockAppearance.SevenSegment),
        new("seconds", "大时分小秒", "Segoe UI", Weight: 300, Appearance: DigitalClockAppearance.SmallSeconds, Seconds: true),
        new("hour12", "十二小时制", "Segoe UI", Hour12: true),
        new("stacked", "竖排时分", "Bahnschrift", Appearance: DigitalClockAppearance.Stacked)
    ];
}

