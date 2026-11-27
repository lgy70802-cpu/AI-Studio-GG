using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskDisplay.Core.Models;
using Microsoft.Win32;

namespace MiraBox.App;

internal sealed record StartupPreferences(bool Tray = true, bool Start = false, bool Restore = false,
    VideoPlaybackMode D92VideoMode = VideoPlaybackMode.Quality);

internal sealed class StartupPreferenceStore(string path)
{
    public StartupPreferences Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<StartupPreferences>(File.ReadAllText(path)) ??
          throw new InvalidDataException("启动偏好文件无效。")
        : new();

    public void Save(StartupPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal static class WorkspaceIdentity
{
    public const string Argument = "--workspace";
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(path).ToUpperInvariant())));
}

internal sealed class WindowsStartupRegistration(string workspace,
    string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    string ValueName => "MiraBoxStudioV67-" + WorkspaceIdentity.Hash(workspace)[..16];

    internal string Command(string executable) => Quote(Path.GetFullPath(executable)) + " " + WorkspaceIdentity.Argument +
        " " + Quote(WorkspaceIdentity.Normalize(workspace));

    static string Quote(string argument)
    {
        var suffix = argument.Length - argument.TrimEnd('\\').Length;
        return "\"" + argument + new string('\\', suffix) + "\"";
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        return key?.GetValue(ValueName) is string { Length: > 0 };
    }

    public void Set(bool enabled, string executable)
    {
        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(runKey)
            ?? throw new InvalidOperationException("无法打开当前用户的 Windows 启动设置。");
        key.SetValue(ValueName, Command(executable), RegistryValueKind.String);
    }
}
