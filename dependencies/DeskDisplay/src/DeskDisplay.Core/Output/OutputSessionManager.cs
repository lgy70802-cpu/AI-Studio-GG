using System.Collections.Immutable;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.Core.Output;

public interface IOutputSessionDriver : IAsyncDisposable
{
    Task RunAsync(Action<OutputActualState> report, CancellationToken cancellationToken);
}
public interface IVideoOutputControl
{
    bool HasVideo { get; }
    bool BackgroundPaused { get; }
    Task SetBackgroundPausedAsync(bool paused);
}
public sealed record OutputSessionContext(DeviceDescriptor Device, DisplayProfile Profile, ThemeDocument Theme,
    ResolvedLayout Layout, IDeviceLease Ownership);
public sealed record OutputStartResult(bool Started, string Code, Guid? SessionId = null);
public sealed record ManagedOutputStatus(OutputSessionSnapshot Snapshot, string? Error);

/// <summary>Independent tasks, cancellation and leases. Drivers pull frames; this manager never queues pixels.</summary>
public sealed class OutputSessionManager(DeviceRegistry registry, IDeviceLeaseProvider leases,
    Func<OutputSessionContext, CancellationToken, ValueTask<IOutputSessionDriver>> createDriver) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ManagedOutputStatus> _last = new(StringComparer.Ordinal);
    private bool _closing;
    private Task? _disposeTask;
    public ImmutableArray<ManagedOutputStatus> Snapshot { get { lock (_gate) return [.. _last.Values]; } }

    public async Task<OutputStartResult> StartAsync(string deviceId, DisplayProfile profile, ThemeDocument theme,
        Guid? layoutId = null, LayoutAdaptation adaptation = LayoutAdaptation.Contain,
        CancellationToken cancellationToken = default, string? conflictGroupId = null)
    {
        cancellationToken.ThrowIfCancellationRequested(); profile.Validate(); theme.Validate();
        var device = registry.Find(deviceId);
        if (device is null || !device.IsConnected) return new(false, "DeviceUnavailable");
        if (DeviceIdentityRules.RequiresConfirmation(device, registry.Snapshot.Where(d => d.IsConnected))) return new(false, "IdentityNeedsConfirmation");
        if (profile.Capabilities.StillImage == CapabilityEvidence.Unknown) return new(false, "CapabilityUnknown");
        var selection = LayoutSelector.Select(theme, profile.SurfaceSize, layoutId);
        if (profile.Capabilities.Video == CapabilityEvidence.Unknown && selection.Layout.Layers.Any(l => l.Visible && l.CreateLegacyLayer().IsVideo)) return new(false, "CapabilityUnknown");
        if (profile.Capabilities.LiveScene == CapabilityEvidence.Unknown && selection.Layout.Layers.Any(l => l.Visible
            && l.Kind is Models.ThemeElementKind.DigitalClock or Models.ThemeElementKind.Date or Models.ThemeElementKind.Weekday
                or Models.ThemeElementKind.StockPlugin or Models.ThemeElementKind.CameraClock))
            return new(false, "CapabilityUnknown");
        var layout = LayoutResolver.Resolve(selection.Layout, profile.SurfaceSize, adaptation);
        // Explicitly reviewed Cover/Responsive may crop. Stretch and orientation cannot be accepted silently.
        if (layout.Diagnostics.Any(d => d.Code is "LegacyStretchRequiresReview" or "OrientationMismatch" or "UnsatisfiedMinimumMargin"))
            return new(false, "LayoutNeedsAdjustment");
        Entry entry;
        lock (_gate)
        {
            if (_closing) return new(false, "Closing");
            if (_active.ContainsKey(deviceId)) return new(false, "Busy");
            IDeviceLease? groupLease = null;
            if (!string.IsNullOrWhiteSpace(conflictGroupId))
            {
                groupLease = leases.TryAcquire("physical-group:" + conflictGroupId.Trim().ToUpperInvariant());
                if (groupLease is null) return new(false, "PhysicalDeviceBusy");
            }
            IDeviceLease? lease;
            try { lease = leases.TryAcquire(deviceId); }
            catch { groupLease?.Dispose(); throw; }
            if (lease is null) { groupLease?.Dispose(); return new(false, "Busy"); }
            var current = registry.Find(deviceId);
            if (current is null || !current.IsConnected || current.Endpoint.ConnectionEpoch != device.Endpoint.ConnectionEpoch)
            { lease.Dispose(); groupLease?.Dispose(); return new(false, "ConnectionChanged"); }
            entry = new(new(device, profile, theme, layout, lease), cancellationToken);
            entry.GroupLease = groupLease;
            _active.Add(deviceId, entry);
            Update(entry, OutputActualState.Preparing);
        }
        _ = RunEntryAsync(entry);
        return await entry.Started.Task.ConfigureAwait(false);
    }

    private async Task RunEntryAsync(Entry entry)
    {
        var context = entry.Context;
        IOutputSessionDriver? driver = null;
        string? error = null;
        var terminal = OutputActualState.Stopped;
        try
        {
            entry.Stop.Token.ThrowIfCancellationRequested();
            driver = await createDriver(context, entry.Stop.Token).ConfigureAwait(false);
            lock (_gate) entry.Driver = driver;
            entry.Stop.Token.ThrowIfCancellationRequested();
            var current = registry.Find(context.Device.DeviceId);
            if (current?.IsConnected != true || current.Endpoint.ConnectionEpoch != context.Device.Endpoint.ConnectionEpoch)
                throw new InvalidOperationException("Connection changed while preparing output.");
            Update(entry, OutputActualState.Starting);
            entry.Started.TrySetResult(new(true, "Started", entry.Id));
            // Native discovery/open/write must never inherit the caller's UI synchronization context.
            await Task.Run(() => driver.RunAsync(state => Update(entry, state), entry.Stop.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (entry.Stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            error = ex.Message;
            lock (_gate) terminal = _last.GetValueOrDefault(context.Device.DeviceId)?.Snapshot.ActualState == OutputActualState.Blocked
                ? OutputActualState.Blocked : OutputActualState.Failed;
        }
        finally
        {
            try { if (driver is not null) await driver.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { error = ex.Message; terminal = OutputActualState.Failed; }
            try { context.Ownership.Dispose(); }
            catch (Exception ex) { error = ex.Message; terminal = OutputActualState.Failed; }
            try { entry.GroupLease?.Dispose(); }
            catch (Exception ex) { error = ex.Message; terminal = OutputActualState.Failed; }
            lock (_gate)
            {
                Update(entry, terminal, error);
                _active.Remove(context.Device.DeviceId);
                entry.Stop.Dispose();
            }
            entry.Started.TrySetResult(new(false, error is null ? "Cancelled" : "Failed", entry.Id));
            entry.Completed.TrySetResult();
        }
    }

    private void Update(Entry entry, OutputActualState state, string? error = null)
    {
        lock (_gate)
        {
            if (!_active.TryGetValue(entry.Context.Device.DeviceId, out var current) || !ReferenceEquals(current, entry)) return;
            if (entry.Stop.IsCancellationRequested && state is not (OutputActualState.Stopped or OutputActualState.Failed)) state = OutputActualState.Stopping;
            var c = entry.Context;
            _last[c.Device.DeviceId] = new(new(entry.Id, c.Device.DeviceId, c.Device.Endpoint.EndpointId,
                c.Device.Endpoint.ConnectionEpoch, c.Profile.ProfileId, c.Theme.ThemeId, c.Theme.Revision,
                c.Layout, entry.Stop.IsCancellationRequested || state is OutputActualState.Stopped or OutputActualState.Failed or OutputActualState.Blocked
                    ? OutputDesiredState.Stopped : OutputDesiredState.Running, state), error);
        }
    }

    public async Task StopAsync(string deviceId)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_active.TryGetValue(deviceId, out entry)) return;
            entry.Stop.Cancel(); Update(entry, OutputActualState.Stopping);
        }
        await entry.Completed.Task.ConfigureAwait(false);
    }
    public IVideoOutputControl? GetVideoControl(string deviceId)
    { lock (_gate) return _active.TryGetValue(deviceId, out var entry) && !entry.Stop.IsCancellationRequested ? entry.Driver as IVideoOutputControl : null; }

    /// <summary>Stops only the exact session owned by this manager; never a replacement or external owner.</summary>
    public async Task<bool> StopOwnedAsync(string deviceId, Guid expectedSessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_active.TryGetValue(deviceId, out entry) || entry.Id != expectedSessionId) return false;
            entry.Stop.Cancel(); Update(entry, OutputActualState.Stopping);
        }
        await entry.Completed.Task.ConfigureAwait(false);
        return true;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            _closing = true;
            var entries = _active.Values.ToArray();
            foreach (var entry in entries) entry.Stop.Cancel();
            _disposeTask = Task.WhenAll(entries.Select(e => e.Completed.Task));
            return new(_disposeTask);
        }
    }

    private sealed class Entry(OutputSessionContext context, CancellationToken cancellationToken)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public IOutputSessionDriver? Driver { get; set; }
        public IDeviceLease? GroupLease { get; set; }
        public OutputSessionContext Context { get; } = context;
        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        public TaskCompletionSource<OutputStartResult> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
