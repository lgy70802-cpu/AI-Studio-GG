namespace DeskDisplay.Core.Models;

/// <summary>Portable editable templates with independent relative landscape/portrait layouts.</summary>
public static class DigitalClockTemplates
{
    private sealed record Style(string Key, string Name, string Background, string Ink, string Accent, int Layout, bool Seconds = false, bool Hour12 = false, bool Bold = false);
    private static readonly Style[] Styles =
    [
        new("paper", "极简白", "#F4F7FB", "#202C43", "#4C85F6", 0),
        new("midnight", "午夜蓝", "#111C32", "#EDF4FF", "#70A7FF", 0, true),
        new("mint", "薄荷绿", "#EAF5EF", "#214D42", "#4C9B7C", 1),
        new("cream", "奶油暖色", "#F6EEDF", "#66513F", "#C7945F", 2, false, true),
        new("coral", "珊瑚粉", "#FFF0ED", "#70443F", "#DD7E72", 0, false, false, true),
        new("amber", "琥珀终端", "#211E19", "#F4C26D", "#BF8C3C", 1, true),
        new("violet", "紫色夜航", "#211D35", "#ECE6FF", "#AD96E3", 2, true),
        new("focus", "专注分栏", "#F1F4F8", "#24384D", "#5F83A8", 1, false, false, true),
        new("calendar", "日历卡片", "#EAEFF5", "#2A3D54", "#DA8264", 2, false, false, true),
        new("dawn", "清晨青蓝", "#EAF5F7", "#205262", "#54A6B8", 0, true)
    ];
    public static IReadOnlyList<ThemeTemplate> All { get; } = Styles.SelectMany(s => Enum.GetValues<ThemeOrientation>().Select(o => new ThemeTemplate($"digital-{s.Key}-{o}", s.Name, o, "digital:" + s.Key))).ToArray();
    public static ThemeProject Create(ThemeTemplate template)
    {
        var style = Styles.Single(s => template.Style == "digital:" + s.Key);
        var project = new ThemeProject { Name = template.Name, Orientation = template.Orientation, Category = "数字时钟" };
        foreach (var layout in project.AllLayouts)
        {
            var w = layout.Width; var h = layout.Height; var portrait = h > w;
            layout.Background = style.Background;
            void Shape(string name, double x, double y, double width, double height, string color) => layout.Layers.Add(new() { Kind = ThemeElementKind.Rectangle, Name = name, X = x * w, Y = y * h, Width = width * w, Height = height * h, Color = color });
            void Info(ThemeElementKind kind, string name, double x, double y, double width, double height, double font, string color) => layout.Layers.Add(new() { Kind = kind, Name = name, X = x * w, Y = y * h, Width = width * w, Height = height * h, FontSize = font, FontFamily = "Microsoft YaHei UI", Color = color });
            if (style.Layout == 2) Shape("日期底板", .05, portrait ? .56 : .63, .9, portrait ? .17 : .31, style.Accent);
            if (style.Layout == 1) Shape("分隔线", portrait ? .18 : .65, portrait ? .53 : .24, portrait ? .64 : .002, portrait ? .002 : .52, style.Accent);
            else Shape("强调线", portrait ? .36 : .46, portrait ? .3 : .17, portrait ? .28 : .08, portrait ? .003 : .012, style.Accent);
            var font = portrait ? w * (style.Hour12 || style.Seconds ? .085 : .13) : h * (style.Hour12 || style.Seconds ? .25 : .34);
            var count = (style.Seconds ? 8 : 5) + (style.Hour12 ? 3 : 0);
            var cw = font * (.62 * count + .12); var ch = Math.Ceiling(font * 1.4 + 8);
            var cx = portrait || style.Layout != 1 ? w * .5 : w * .34;
            var cy = h * (portrait ? .42 : style.Layout == 1 ? .48 : .43);
            layout.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Name = "数字时钟", X = cx - cw / 2, Y = cy - ch / 2, Width = cw, Height = ch, FontSize = font, FontFamily = "Consolas", Color = style.Ink, ShowSeconds = style.Seconds, Hour12 = style.Hour12, Bold = style.Bold });
            var ix = portrait ? .1 : style.Layout == 1 ? .68 : .1;
            var iw = portrait ? .8 : style.Layout == 1 ? .26 : .8;
            var infoColor = style.Layout == 2 ? "#202C43" : style.Ink;
            Info(ThemeElementKind.Date, "日期", ix, portrait ? .58 : style.Layout == 1 ? .32 : .66, iw, portrait ? .045 : .12, portrait ? w * .046 : h * .065, infoColor);
            Info(ThemeElementKind.Weekday, "星期", ix, portrait ? .65 : style.Layout == 1 ? .56 : .8, iw, portrait ? .04 : .09, portrait ? w * .05 : h * .055, style.Layout == 2 ? "#202C43" : style.Accent);
            for (var i = 0; i < layout.Layers.Count; i++) layout.Layers[i].ZIndex = i;
        }
        project.Validate(); return project;
    }
}

