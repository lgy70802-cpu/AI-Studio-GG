namespace DeskDisplay.Core.Output;

public interface IDeviceLease : IDisposable
{
    string DeviceId { get; }
    bool IsReleased { get; }
}
public interface IDeviceLeaseProvider
{
    IDeviceLease? TryAcquire(string deviceId);
}

/// <summary>Shared provider instance is required for cooperating managers. Platform adapters add process locks.</summary>
public sealed class InMemoryDeviceLeaseProvider : IDeviceLeaseProvider
{
    private readonly object _gate = new();
    private readonly HashSet<string> _owned = new(StringComparer.Ordinal);
    public IDeviceLease? TryAcquire(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        lock (_gate) return _owned.Add(deviceId) ? new Lease(this, deviceId) : null;
    }
    private sealed class Lease(InMemoryDeviceLeaseProvider owner, string id) : IDeviceLease
    {
        private int _released;
        public string DeviceId => id;
        public bool IsReleased => Volatile.Read(ref _released) != 0;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) lock (owner._gate) owner._owned.Remove(id); }
    }
}
