namespace DeskDisplay.Core.Storage;

/// <summary>One process-wide user data root. Device ownership locks are deliberately not redirected.</summary>
public static class StudioDataPaths
{
    private static readonly string? Override=Environment.GetEnvironmentVariable("D92_STUDIO_DATA_ROOT");
    public static bool IsIsolated=>!string.IsNullOrWhiteSpace(Override);
    public static string Root {get;}=ResolveRoot(Override);
    public static string ResolveRoot(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DeskDisplay");
        if(!Path.IsPathFullyQualified(value))throw new ArgumentException("隔离数据目录必须是绝对路径。");
        var path=Path.GetFullPath(value);
        if(path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)==Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar))throw new ArgumentException("隔离数据目录不能是磁盘根目录。");
        return path;
    }
}
