using System.Text.Json;

namespace DeskDisplay.Infrastructure.Configuration;

public sealed record ManagedDevicePreference(string DeviceId, string Alias, int Width, int Height, int Rotation, string PhysicalGroup = "", bool ProfileConfirmed = false);
public sealed record ManagedDevicePreferences(int Version, IReadOnlyList<ManagedDevicePreference> Devices);

/// <summary>Separate, explicitly saved preferences; incompatible files are preserved and reported.</summary>
public sealed class DevicePreferenceStore(string path)
{
    private readonly SemaphoreSlim _writes = new(1, 1);
    public async Task<IReadOnlyList<ManagedDevicePreference>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("设备设置文件过大，已保留原文件。");
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ManagedDevicePreferences>(stream, cancellationToken: ct)
            ?? throw new InvalidDataException("设备设置为空。");
        Validate(document); return document.Devices;
    }
    public async Task SaveAsync(IReadOnlyList<ManagedDevicePreference> devices, CancellationToken ct = default)
    {
        var document = new ManagedDevicePreferences(1, devices.ToArray()); Validate(document);
        await _writes.WaitAsync(ct);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var guard = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            // Preserve entries added by another process and refuse unknown/corrupt formats.
            var merged = (await LoadAsync(ct)).ToDictionary(d => d.DeviceId, StringComparer.Ordinal);
            foreach (var item in devices) merged[item.DeviceId] = item;
            document = new(1, merged.Values.ToArray()); Validate(document);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document), ct);
            ct.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _writes.Release(); }
    }
    private static void Validate(ManagedDevicePreferences document)
    {
        if (document.Version != 1 || document.Devices is null || document.Devices.Count > 256
            || document.Devices.Any(d => d is null || string.IsNullOrWhiteSpace(d.DeviceId) || d.DeviceId.Length > 2048
                || d.PhysicalGroup is null || d.PhysicalGroup.Length > 80 || string.IsNullOrWhiteSpace(d.Alias) || d.Alias.Length > 80 || d.Width < 0 || d.Height < 0
                || d.Width > 8192 || d.Height > 8192 || (long)d.Width * d.Height > 16_777_216 || d.Rotation is not (0 or 90 or 180 or 270))
            || document.Devices.Select(d => d.DeviceId).Distinct(StringComparer.Ordinal).Count() != document.Devices.Count)
            throw new InvalidDataException("设备设置版本或内容无效，已保留原文件。");
    }
}

