using System.Collections.Immutable;

namespace DeskDisplay.Core.Devices;

/// <summary>Thread-safe connection snapshots. Each transport submits a complete, successful enumeration.</summary>
public sealed class DeviceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DeviceDescriptor> _devices = new(StringComparer.Ordinal);
    public ImmutableArray<DeviceDescriptor> Snapshot { get { lock (_gate) return [.. _devices.Values.OrderBy(d => d.DeviceId, StringComparer.Ordinal)]; } }
    public DeviceDescriptor? Find(string id) { lock (_gate) return _devices.GetValueOrDefault(id); }
    public bool ConfirmEndpoint(string id, long epoch)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(id, out var device) || !device.IsConnected || device.Endpoint.ConnectionEpoch != epoch) return false;
            _devices[id] = device with { Identity = device.Identity with { Confidence = IdentityConfidence.UserConfirmed } };
            return true;
        }
    }

    public void Replace(DeviceTransport transport, IEnumerable<DeviceDescriptor> devices)
    {
        var incoming = devices.ToArray();
        if (incoming.Any(d => d.Transport != transport || string.IsNullOrWhiteSpace(d.DeviceId))
            || incoming.Select(d => d.DeviceId).Distinct(StringComparer.Ordinal).Count() != incoming.Length)
            throw new ArgumentException("Invalid or duplicate discovery keys.");
        lock (_gate)
        {
            if (incoming.Any(d => _devices.TryGetValue(d.DeviceId, out var old) && old.Transport != transport))
                throw new ArgumentException("Transport identity collision.");
            var present = incoming.Select(d => d.DeviceId).ToHashSet(StringComparer.Ordinal);
            foreach (var old in _devices.Values.Where(d => d.Transport == transport && d.IsConnected && !present.Contains(d.DeviceId)).ToArray())
                _devices[old.DeviceId] = old with { IsConnected = false, Endpoint = old.Endpoint with { ConnectionEpoch = old.Endpoint.ConnectionEpoch + 1 } };
            foreach (var device in incoming)
            {
                _devices.TryGetValue(device.DeviceId, out var old);
                var changed = old is null || old.IsConnected != device.IsConnected
                    || old.Endpoint.EndpointId != device.Endpoint.EndpointId || old.Endpoint.Address != device.Endpoint.Address
                    || old.Identity.Namespace != device.Identity.Namespace || old.Identity.SerialNumber != device.Identity.SerialNumber;
                var epoch = old is null ? 1 : old.Endpoint.ConnectionEpoch + (changed ? 1 : 0);
                var identity = !changed && old?.Identity.Confidence == IdentityConfidence.UserConfirmed
                    ? device.Identity with { Confidence = IdentityConfidence.UserConfirmed } : device.Identity;
                _devices[device.DeviceId] = device with { Identity = identity, Endpoint = device.Endpoint with { ConnectionEpoch = epoch } };
            }
        }
    }
}

public static class OutputDeviceKeys
{
    public static string UsbScreen(string model, string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        return model switch
        {
            "D92" => D92(serial),
            "D69" => "usb:2100:0027:" + Uri.EscapeDataString(serial),
            _ => throw new NotSupportedException("Unsupported USB screen model")
        };
    }
    public static string D92(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        return "usb:5548:1011:" + Uri.EscapeDataString(serial);
    }
    public static string WindowsDisplay(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return "windows-display:" + id.ToUpperInvariant();
    }
}
