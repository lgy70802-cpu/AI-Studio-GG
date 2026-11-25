namespace DeskDisplay.App.ViewModels;

public sealed record HomeRecentEntry(string Name, object Source, DateTimeOffset UsedAt)
{
    public WallpaperItemViewModel? Wallpaper => Source as WallpaperItemViewModel;
    public ThemeCard? Theme => Source as ThemeCard;
    public bool IsTheme => Source is ThemeCard;
    public string UseLabel => IsTheme ? "使用主题" : Wallpaper?.Asset.IsVideo == true ? "使用视频" : "使用壁纸";
}
