namespace DeskDisplay.App.ViewModels;

public enum AppPage
{
    Home,
    Carousel,
    Library,
    Online,
    Creator,
    Devices,
    Account,
    Settings,
    Plugins
}

public sealed record NavigationItem(AppPage Page, string Name, string Glyph);
