using System.Text.Json;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.AdaptationTests;

internal static class SessionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var registry = new DeviceRegistry();
        DeviceDescriptor Device(string serial, string address = "path") => new($"test:{serial}", "same-model", DeviceTransport.Virtual,
            new("test", serial, IdentityConfidence.VerifiedStable), new(address + serial, address + serial, 0), true);
        var a = Device("A"); var b = Device("B");
        registry.Replace(DeviceTransport.Virtual, [a, b]);
        check(registry.Find(a.DeviceId)!.Endpoint.ConnectionEpoch == 1, "Registry assigns initial connection epoch");
        registry.Replace(DeviceTransport.Virtual, [b, a]);
        check(registry.Find(a.DeviceId)!.Endpoint.ConnectionEpoch == 1, "Reordered enumeration does not create false reconnect");
        registry.Replace(DeviceTransport.Virtual, [b]);
        check(registry.Find(a.DeviceId) is { IsConnected: false, Endpoint.ConnectionEpoch: 2 } && registry.Find(b.DeviceId)!.IsConnected,
            "Registry disconnects only absent device");
        registry.Replace(DeviceTransport.Virtual, [Device("A", "new-path"), b]);
        check(registry.Find(a.DeviceId) is { IsConnected: true, Endpoint.ConnectionEpoch: 3 }, "Same identity recovers with new endpoint epoch");
        var leases = new InMemoryDeviceLeaseProvider();
        var drivers = new Dictionary<string, Driver>();
        var theme = LegacyThemeAdapter.Read(new ThemeProject { Name = "Session theme" });
        var themeBytes = JsonSerializer.Serialize(theme);
        DisplayProfile Profile(int width) => new("profile-" + width, new(width, width / 4), new(width, width / 4), 0,
            new(CapabilityEvidence.Declared, CapabilityEvidence.Declared, CapabilityEvidence.Unknown));
        await using var manager = new OutputSessionManager(registry, leases, (context, _) =>
        {
            var driver = new Driver(); drivers[context.Device.DeviceId] = driver;
            return ValueTask.FromResult<IOutputSessionDriver>(driver);
        });
        var starts = await Task.WhenAll(manager.StartAsync(a.DeviceId, Profile(1920), theme), manager.StartAsync(b.DeviceId, Profile(3840), theme));
        check(starts.All(s => s.Started), "Real session manager starts two virtual drivers independently");
        check(manager.Snapshot.Select(s => s.Snapshot.Layout.SurfaceSize.Width).Order().SequenceEqual([1920, 3840]), "Per-device layout dimensions do not overwrite each other");
        check((await manager.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "Busy", "Repeated start cannot replace active session implicitly");
        await using var rival = new OutputSessionManager(registry, leases, (_, _) => throw new InvalidOperationException("must not create"));
        check((await rival.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "Busy", "Two managers share the same device lease boundary");
        var driverA = drivers[a.DeviceId]; var driverB = drivers[b.DeviceId];
        await driverA.Running.Task; await driverB.Running.Task;
        driverA.Report(OutputActualState.WaitingForDevice); driverB.Report(OutputActualState.Streaming);
        check(manager.Snapshot.Single(s => s.Snapshot.DeviceId == a.DeviceId).Snapshot.ActualState == OutputActualState.WaitingForDevice
            && manager.Snapshot.Single(s => s.Snapshot.DeviceId == b.DeviceId).Snapshot.ActualState == OutputActualState.Streaming,
            "Waiting target does not stop streaming peer");
        check(leases.TryAcquire(a.DeviceId) is null, "Lease remains held while waiting for reconnect");
        driverA.Report(OutputActualState.Streaming);
        check(manager.Snapshot.Single(s => s.Snapshot.DeviceId == a.DeviceId).Snapshot.ActualState == OutputActualState.Streaming, "Driver reconnect state resumes its own session");
        driverA.HoldDisposal = true;
        var stopping = manager.StopAsync(a.DeviceId);
        await driverA.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check(!stopping.IsCompleted && leases.TryAcquire(a.DeviceId) is null, "Stop awaits resource cleanup before releasing device lease");
        driverA.ReleaseDisposal.TrySetResult(); await stopping;
        check(driverA.Disposed && !driverB.Disposed, "Stopping one session disposes only its driver");
        using (var free = leases.TryAcquire(a.DeviceId)) check(free is not null, "Stopped device lease can be reacquired");
        var restarted = await manager.StartAsync(a.DeviceId, Profile(1920), theme);
        await drivers[a.DeviceId].Running.Task;
        check(restarted.Started && restarted.SessionId != starts[0].SessionId, "Explicit restart has a new session identity");
        driverA.Report(OutputActualState.Failed);
        check(manager.Snapshot.Single(s => s.Snapshot.DeviceId == a.DeviceId).Snapshot.SessionId == restarted.SessionId
            && manager.Snapshot.Single(s => s.Snapshot.DeviceId == a.DeviceId).Snapshot.ActualState != OutputActualState.Failed,
            "Late callback from old session cannot corrupt replacement");
        driverB.Fail.TrySetResult();
        await UntilAsync(() => driverB.Disposed);
        await UntilAsync(() => manager.Snapshot.Single(s => s.Snapshot.DeviceId == b.DeviceId).Snapshot.ActualState == OutputActualState.Failed);
        check(manager.Snapshot.Single(s => s.Snapshot.DeviceId == b.DeviceId).Error is not null, "Driver failure records per-device error");
        check(!drivers[a.DeviceId].Disposed, "Peer survives another driver failure");
        await manager.DisposeAsync();
        check(drivers[a.DeviceId].Disposed && (await manager.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "Closing", "Manager shutdown awaits all and rejects new sessions");
        check(JsonSerializer.Serialize(theme) == themeBytes, "Session lifecycle leaves source theme unchanged");

        await using var failed = new OutputSessionManager(registry, leases, (_, _) => throw new IOException("prepare failed"));
        check((await failed.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "Failed", "Preparation failure is a returned device result");
        using (var free = leases.TryAcquire(a.DeviceId)) check(free is not null, "Preparation failure releases device lease");
        var preparationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var preparing = new OutputSessionManager(registry, leases, async (_, ct) =>
        {
            preparationEntered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new Driver();
        });
        var pending = preparing.StartAsync(a.DeviceId, Profile(1920), theme);
        await preparationEntered.Task; await preparing.StopAsync(a.DeviceId);
        check((await pending).Code == "Cancelled", "Stop cancels a preparing session without leaking its reservation");
        using (var free = leases.TryAcquire(a.DeviceId)) check(free is not null, "Cancelled preparation releases ownership");

        registry.Replace(DeviceTransport.Virtual, [a with { Identity = a.Identity with { Confidence = IdentityConfidence.Ambiguous } }, b]);
        check((await failed.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "IdentityNeedsConfirmation", "Ambiguous identity is rejected before driver creation");
        registry.Replace(DeviceTransport.Virtual, [b]);
        check((await failed.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "DeviceUnavailable", "Disconnected target never falls back to another device");
        registry.Replace(DeviceTransport.Virtual, [a, b]);
        await using var changed = new OutputSessionManager(registry, leases, (context, _) =>
        {
            registry.Replace(DeviceTransport.Virtual, [Device("A", "changed-during-prepare"), b]);
            return ValueTask.FromResult<IOutputSessionDriver>(new Driver());
        });
        check((await changed.StartAsync(a.DeviceId, Profile(1920), theme)).Code == "Failed", "Connection epoch revalidated after asynchronous preparation");
        check((await failed.StartAsync(a.DeviceId, Profile(1920) with { Capabilities = new(CapabilityEvidence.Unknown, CapabilityEvidence.Unknown, CapabilityEvidence.Unknown) }, theme)).Code == "CapabilityUnknown", "Unknown capability cannot be asserted supported");
        var leaseAttempts = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => leases.TryAcquire("atomic-test"))));
        check(leaseAttempts.Count(l => l is not null) == 1, "Sixteen simultaneous ownership attempts have one winner");
        foreach (var lease in leaseAttempts) lease?.Dispose();
        await using var badCleanup = new OutputSessionManager(registry, leases, (_, _) => ValueTask.FromResult<IOutputSessionDriver>(new Driver { ThrowOnDispose = true }));
        await badCleanup.StartAsync(a.DeviceId, Profile(1920), theme); await badCleanup.StopAsync(a.DeviceId);
        check(badCleanup.Snapshot.Single().Snapshot.ActualState == OutputActualState.Failed, "Disposal exception is recorded without hanging shutdown");
        using (var free = leases.TryAcquire(a.DeviceId)) check(free is not null, "Disposal failure still releases device ownership");
        await using var blocked = new OutputSessionManager(registry, leases, (_, _) =>
        {
            var driver = new Driver { InitialState = OutputActualState.Blocked }; driver.Fail.TrySetResult();
            return ValueTask.FromResult<IOutputSessionDriver>(driver);
        });
        await blocked.StartAsync(a.DeviceId, Profile(1920), theme);
        await UntilAsync(() => blocked.Snapshot.Single().Error is not null);
        check(blocked.Snapshot.Single().Snapshot is { ActualState: OutputActualState.Blocked, DesiredState: OutputDesiredState.Stopped },
            "External blocking remains blocked after cleanup, not streaming or automatic retry");
    }

    private static async Task UntilAsync(Func<bool> ready)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!ready()) await Task.Delay(5, deadline.Token);
    }

    private sealed class Driver : IOutputSessionDriver
    {
        private Action<OutputActualState>? _report;
        public TaskCompletionSource Running { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fail { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldDisposal { get; set; }
        public bool ThrowOnDispose { get; set; }
        public OutputActualState InitialState { get; set; } = OutputActualState.Streaming;
        public bool Disposed { get; private set; }
        public void Report(OutputActualState state) => _report?.Invoke(state);
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken ct)
        {
            _report = report; report(InitialState); Running.TrySetResult();
            await Fail.Task.WaitAsync(ct); throw new IOException("injected target failure");
        }
        public async ValueTask DisposeAsync()
        {
            Disposing.TrySetResult(); if (HoldDisposal) await ReleaseDisposal.Task;
            Disposed = true;
            if (ThrowOnDispose) throw new IOException("injected disposal failure");
        }
    }
}
