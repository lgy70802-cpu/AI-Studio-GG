namespace DeskDisplay.Core.Models;

public sealed record ThemeTemplate(string Key, string Name, ThemeOrientation Orientation, string Style)
{
    public double Aspect => Orientation == ThemeOrientation.Portrait ? .25 : 4;
    public string OrientationLabel => Orientation == ThemeOrientation.Portrait ? "竖屏 1:4" : "横屏 4:1";
    public bool RequiresCamera => Style == "camera";
    public bool SupportsBackground => Style == "wallpaper";
    public string Contents => Style.StartsWith("digital:", StringComparison.Ordinal) ? "数字时间 · 日期 · 星期 · 可编辑装饰" : Style switch { "clock" => "时间 · 日期 · 星期", "wallpaper" => "自选壁纸 · 时间 · 日期", "work" => "时间 · 提醒文字 · 信息卡", "camera" => "复古相机插件 · 日期", "text" => "文字 · 强调形状", _ => "图片槽位 · 标题" };
    public ThemeProject Create()
    {
        if (Style.StartsWith("digital:", StringComparison.Ordinal)) return DigitalClockTemplates.Create(this);
        var project = new ThemeProject { Name = Name, Orientation = Orientation };
        foreach (var layout in new[] { project.Landscape, project.Portrait })
        {
            var portrait = layout.Height > layout.Width;
            if (Style == "clock")
            {
                layout.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Name = "数字时钟", X = layout.Width * .1, Y = portrait ? 680 : 55, Width = layout.Width * .8, Height = 240, FontSize = portrait ? 105 : 170, ShowSeconds = !portrait });
                layout.Layers.Add(new() { Kind = ThemeElementKind.Date, Name = "日期", X = layout.Width * .1, Y = portrait ? 920 : 300, Width = layout.Width * .8, Height = 90, FontSize = portrait ? 30 : 42 });
                layout.Layers.Add(new() { Kind = ThemeElementKind.Weekday, Name = "星期", X = layout.Width * .1, Y = portrait ? 1040 : 390, Width = layout.Width * .8, Height = 70, FontSize = 28 });
            }
            else if (Style == "text")
            {
                layout.Background = "#F4F7FB";
                layout.Layers.Add(new() { Kind = ThemeElementKind.Rectangle, Name = "强调线", X = layout.Width * .1, Y = portrait ? 700 : 140, Width = portrait ? 80 : 12, Height = portrait ? 12 : 180, Color = "#4C85F6" });
                layout.Layers.Add(new() { Kind = ThemeElementKind.Text, Name = "主题文字", Text = "专注当下", X = layout.Width * .18, Y = portrait ? 800 : 120, Width = layout.Width * .72, Height = 180, FontFamily = "Microsoft YaHei UI", FontSize = portrait ? 52 : 115, Color = "#202C43" });
            }
            else if (Style is "wallpaper" or "work" or "camera")
            {
                var w = layout.Width; var h = layout.Height;
                if (Style == "camera")
                {
                    var width = w * (portrait ? .9 : .82); var height = width / new RetroCameraClockScene().AspectRatio;
                    layout.Layers.Add(new() { Kind = ThemeElementKind.CameraClock, Name = "复古相机翻页时钟", X = (w - width) / 2, Y = portrait ? h * .2 : h * .04, Width = width, Height = height });
                    if (portrait) layout.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Name = "竖屏时间", X = w * .05, Y = h * .44, Width = w * .9, Height = h * .13, FontSize = w * .16, ShowSeconds = true });
                    layout.Layers.Add(new() { Kind = ThemeElementKind.Date, Name = "日期", X = w * .05, Y = portrait ? h * .62 : h * .87, Width = w * .9, Height = h * .08, FontSize = portrait ? w * .065 : h * .055 });
                }
                else
                {
                    layout.Background = Style == "work" ? "#F4F7FB" : "#152239";
                    var color = Style == "work" ? "#202C43" : "#FFFFFF";
                    layout.Layers.Add(new() { Kind = ThemeElementKind.Rectangle, Name = "信息底板", X = w * .04, Y = h * .08, Width = w * .92, Height = h * .84, Color = Style == "work" ? "#FFFFFF" : "#152239", Opacity = Style == "work" ? 1 : .6, Locked = true });
                    layout.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Name = "数字时钟", X = w * .08, Y = h * (portrait ? .26 : .12), Width = w * (portrait ? .84 : .5), Height = h * (portrait ? .15 : .48), FontSize = portrait ? w * .16 : h * .32, Color = color, ShowSeconds = true });
                    layout.Layers.Add(new() { Kind = ThemeElementKind.Date, Name = "日期", X = w * .08, Y = h * (portrait ? .43 : .64), Width = w * (portrait ? .84 : .5), Height = h * .09, FontSize = portrait ? w * .065 : h * .075, Color = color });
                    if (Style == "work")
                    {
                        layout.Layers.Add(new() { Kind = ThemeElementKind.Rectangle, Name = "分隔线", X = w * (portrait ? .12 : .62), Y = h * (portrait ? .59 : .2), Width = portrait ? w * .76 : w * .004, Height = portrait ? h * .003 : h * .6, Color = "#4C85F6" });
                        layout.Layers.Add(new() { Kind = ThemeElementKind.Text, Name = "工作提醒", Text = "专注当下", X = w * (portrait ? .1 : .66), Y = h * (portrait ? .66 : .3), Width = w * (portrait ? .8 : .26), Height = h * .18, FontSize = portrait ? w * .1 : h * .1, Color = color, FontFamily = "Microsoft YaHei UI" });
                    }
                }
            }
            else
            {
                layout.Layers.Add(new() { Kind = ThemeElementKind.Image, Name = "图片（待选择）", Width = layout.Width, Height = layout.Height, Fill = ImageDisplayMode.Fill, AssetOrigin = ThemeAssetOrigin.Local });
                layout.Layers.Add(new() { Kind = ThemeElementKind.Rectangle, Name = "文字底板", Y = layout.Height * .72, Width = layout.Width, Height = layout.Height * .28, Color = "#152239", Opacity = .7 });
                layout.Layers.Add(new() { Kind = ThemeElementKind.Text, Name = "标题", Text = "我的风景", X = layout.Width * .08, Y = layout.Height * .76, Width = layout.Width * .84, Height = layout.Height * .18, FontSize = portrait ? 54 : 76, FontFamily = "Microsoft YaHei UI" });
            }
            for (var i = 0; i < layout.Layers.Count; i++) layout.Layers[i].ZIndex = i;
        }
        project.Validate(); return project;
    }
}

public static class ThemeTemplates
{
    public static IReadOnlyList<ThemeTemplate> All { get; } = Enum.GetValues<ThemeOrientation>().SelectMany(o => new[]
    {
        new ThemeTemplate($"clock-{o}", "极简数字时钟", o, "clock"),
        new ThemeTemplate($"text-{o}", "极简文字", o, "text"),
        new ThemeTemplate($"photo-{o}", "图片与标题（需选择图片）", o, "photo"),
        new ThemeTemplate($"wallpaper-{o}", "壁纸时钟", o, "wallpaper"),
        new ThemeTemplate($"work-{o}", "工作信息卡", o, "work"),
        new ThemeTemplate($"camera-{o}", "复古相机时钟", o, "camera")
    }).Concat(DigitalClockTemplates.All).ToArray();
}

