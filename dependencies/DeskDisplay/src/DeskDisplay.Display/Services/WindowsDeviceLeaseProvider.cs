using System.IO;
using System.Security.Cryptography;
using System.Text;
using DeskDisplay.Core.Output;

namespace DeskDisplay.Display.Services;

/// <summary>Cooperating Studio processes use a handle lock, released by the OS after process exit.
/// This does not detect or exclude unrelated applications such as Craft.</summary>
public sealed class WindowsDeviceLeaseProvider : IDeviceLeaseProvider
{
    public static WindowsDeviceLeaseProvider Shared { get; } = new();
    private readonly InMemoryDeviceLeaseProvider _local = new();
    private readonly string _directory;
    public WindowsDeviceLeaseProvider(string? directory = null) => _directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskDisplay", "DeviceLocks");

    public IDeviceLease? TryAcquire(string deviceId)
    {
        var local = _local.TryAcquire(deviceId);
        if (local is null) return null;
        try
        {
            Directory.CreateDirectory(_directory);
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceId))) + ".lock";
            // Do not delete lock paths on release: deletion races can create two different locked files.
            var stream = new FileStream(Path.Combine(_directory, name), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new Lease(local, stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { local.Dispose(); return null; }
        catch { local.Dispose(); throw; }
    }
    private sealed class Lease(IDeviceLease local, FileStream stream) : IDeviceLease
    {
        private int _released;
        public string DeviceId => local.DeviceId;
        public bool IsReleased => Volatile.Read(ref _released) != 0;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            try { stream.Dispose(); } finally { local.Dispose(); }
        }
    }
}
