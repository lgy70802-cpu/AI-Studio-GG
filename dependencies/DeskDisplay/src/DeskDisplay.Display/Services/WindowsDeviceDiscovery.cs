using System.Security.Cryptography;
using System.Text;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.Display.Services;

/// <summary>Opt-in discovery worker. Failed snapshots are never interpreted as physical removal.</summary>
public sealed class WindowsDeviceDiscovery(DeviceRegistry registry, IDisplayManager displays, ILogger logger) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private DisplayInfo[] _displaySnapshot = [];
    public IReadOnlyList<DisplayInfo> DisplaySnapshot => Volatile.Read(ref _displaySnapshot);
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _refresh.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            try { registry.Replace(DeviceTransport.UsbHid, DescribeUsb(await Task.Run(D92HidDevice.EnumerateSupported, linked.Token).ConfigureAwait(false))); }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.Warning(ex, "Multi-device USB enumeration failed; retaining last snapshot"); }
            var snapshot = await displays.GetDisplaysAsync(linked.Token).ConfigureAwait(false);
            registry.Replace(DeviceTransport.SystemDisplay, DescribeDisplays(snapshot));
            Volatile.Write(ref _displaySnapshot, snapshot.ToArray());
        }
        finally { _refresh.Release(); }
    }
    private Task? _run;
    private Task? _dispose;
    private readonly object _lifetime = new();
    public void Start()
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_dispose is not null, this);
            _run ??= Task.Run(RunAsync);
        }
    }
    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try { await RefreshAsync(_stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.Warning(ex, "Multi-device display enumeration failed; retaining last snapshot"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), _stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
        }
    }

    public static IReadOnlyList<DeviceDescriptor> DescribeUsb(IReadOnlyList<D92DeviceInfo> devices) => devices.Select(d =>
    {
        var (vid, pid) = D92HidDevice.ModelIdentity(d.ModelKey);
        var unique = !string.IsNullOrWhiteSpace(d.SerialNumber) && devices.Count(other => other.ModelKey == d.ModelKey && other.SerialNumber == d.SerialNumber) == 1;
        var key = unique ? OutputDeviceKeys.UsbScreen(d.ModelKey, d.SerialNumber) : "ambiguous-usb:" + Hash(d.Path);
        return new DeviceDescriptor(key, d.ModelKey, DeviceTransport.UsbHid,
            new($"usb:{vid:x4}:{pid:x4}", d.SerialNumber, unique ? IdentityConfidence.VerifiedStable : IdentityConfidence.Ambiguous),
            new(d.Path, d.Path, 0), true);
    }).ToArray();

    public static IReadOnlyList<DeviceDescriptor> DescribeDisplays(IReadOnlyList<DisplayInfo> devices) => devices.Where(d => !d.IsPrimary).Select(d =>
    {
        var unique = !string.IsNullOrWhiteSpace(d.DeviceId) && devices.Count(other => string.Equals(other.DeviceId, d.DeviceId, StringComparison.OrdinalIgnoreCase)) == 1
            && devices.Count(other => other.DeviceName == d.DeviceName) == 1;
        // Windows path identity is platform-local, not an asserted cross-platform hardware serial.
        var key = unique ? OutputDeviceKeys.WindowsDisplay(d.DeviceId!) : "ambiguous-display:" + Hash(d.DeviceName + "|" + d.DeviceId);
        return new DeviceDescriptor(key, d.FriendlyName, DeviceTransport.SystemDisplay,
            new("windows-target-path", d.DeviceId?.ToUpperInvariant(), unique ? IdentityConfidence.VerifiedStable : IdentityConfidence.Ambiguous),
            new(d.DeviceName, d.DeviceId ?? d.DeviceName, 0), true);
    }).ToArray();

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            if (_dispose is not null) return new(_dispose);
            _stop.Cancel(); _dispose = FinishAsync(); return new(_dispose);
        }
    }
    private async Task FinishAsync() { if (_run is not null) await _run.ConfigureAwait(false); _stop.Dispose(); }
}
