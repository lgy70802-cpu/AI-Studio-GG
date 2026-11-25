namespace DeskDisplay.Infrastructure;

public static class AppPaths
{
    public static string DataDirectory => DeskDisplay.Core.Storage.StudioDataPaths.Root;
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
