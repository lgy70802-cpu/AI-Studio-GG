using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Serilog;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using DeskDisplay.Display.Services;

namespace DeskDisplay.Display.Usb;

public sealed record D92OutputState(string Status, long Frames, int Connections, string? Error = null);
public sealed record D92FrameTiming(long Frame, int JpegBytes, double RenderMilliseconds, double WriteMilliseconds);

public interface ID92RenderTarget
{
    event Action<D92OutputState>? StateChanged;
    Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render,
        TimeSpan frameInterval, CancellationToken cancellationToken);
}

/// <summary>One awaited frame at a time; no stale queue. Re-enumerates the same serial after disconnect.</summary>
public sealed class D92HidRenderTarget(ILogger logger, IDeviceLease? ownership = null,
    IDeviceLeaseProvider? leases = null, DeviceDescriptor? confirmedDevice = null,
    bool experimentalHighFrameRate = false, bool experimentalD69Video = false,
    bool skipRepeatedStillFrames = false) : ID92RenderTarget
{
    private int _running;
    public event Action<D92OutputState>? StateChanged;
    public event Action<D92FrameTiming>? FrameCompleted;

    public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render,
        TimeSpan frameInterval, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentNullException.ThrowIfNull(render);
        var model = confirmedDevice?.ModelKey ?? "D92";
        _ = D92HidDevice.ModelIdentity(model);
        var d69 = model == "D69";
        if (d69 && !experimentalD69Video && (experimentalHighFrameRate || frameInterval != TimeSpan.FromSeconds(1)))
            throw new NotSupportedException("D69 currently supports only the experimental one-second scene output");
        // Explicit diagnostic opt-in only; normal client callers retain the 10 FPS ceiling.
        var minimumInterval = d69 && experimentalD69Video ? TimeSpan.FromSeconds(1d / 24) :
            experimentalHighFrameRate ? TimeSpan.FromSeconds(1d / 30) : TimeSpan.FromMilliseconds(100);
        if (frameInterval < minimumInterval) throw new ArgumentOutOfRangeException(nameof(frameInterval));
        if (Interlocked.Exchange(ref _running, 1) != 0) throw new InvalidOperationException("Output already running");
        long frames = 0;
        var connections = 0;
        var heartbeat = new Stopwatch();
        byte[]? lastWrittenStill = null;
        string? previousState = null;
        D92HidConnection? connection = null;
        IDeviceLease? acquired = null;
        IDeviceLease? serialGuard = null;
        void Publish(string status, string? error = null, bool force = false)
        {
            if (!force && previousState == status) return;
            previousState = status;
            logger.Information("D92 output {Status}; Frames={Frames} Connections={Connections} Error={Error}", status, frames, connections, error);
            try { StateChanged?.Invoke(new(status, frames, connections, error)); }
            catch (Exception exception) { logger.Warning(exception, "D92 status subscriber failed"); }
        }
        try
        {
            var manualEndpoint = confirmedDevice?.Identity.Confidence == IdentityConfidence.UserConfirmed;
            if (confirmedDevice is not null && confirmedDevice.Identity.SerialNumber != serialNumber)
                throw new InvalidOperationException("Confirmed device serial does not match requested target.");
            var stableKey = OutputDeviceKeys.UsbScreen(model, serialNumber);
            var deviceId = manualEndpoint ? confirmedDevice!.DeviceId : stableKey;
            acquired = ownership is null ? (leases ?? WindowsDeviceLeaseProvider.Shared).TryAcquire(deviceId) : null;
            var lease = ownership ?? acquired;
            if (lease is null || lease.IsReleased || lease.DeviceId != deviceId)
                throw new InvalidOperationException("设备已有输出会话或无法取得设备锁；请先停止该设备的其他输出。");
            if (manualEndpoint && deviceId != stableKey)
            {
                serialGuard = (leases ?? WindowsDeviceLeaseProvider.Shared).TryAcquire(stableKey);
                if (serialGuard is null) throw new InvalidOperationException("相同序列号已有输出，请先停止后再确认目标。");
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                var timer = Stopwatch.StartNew();
                // Occupancy checks are part of the frame budget; otherwise every frame adds
                // this work on top of the requested interval even when generation is fast.
                if (IsCraftRunning()) { Publish("BlockedByCraft"); break; }
                try
                {
                    if (connection is null)
                    {
                        var devices = SelectCandidates(D92HidDevice.EnumerateModel(model), serialNumber, confirmedDevice);
                        if (devices.Length > 1) throw new InvalidOperationException("Multiple devices have the selected serial number");
                        if (devices.Length == 0)
                        {
                            if (manualEndpoint) throw new InvalidOperationException("手工确认的设备已断开，请重新核对后启动。");
                            Publish("WaitingForDevice");
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        connection = D92HidDevice.Open(devices[0]);
                        await SendWholeFrameAsync(connection.WakeAsync, cancellationToken).ConfigureAwait(false);
                        heartbeat.Reset();
                        lastWrittenStill = null;
                        logger.Information("D92 wake report sent for {SerialNumber}", serialNumber);
                        connections++;
                        Publish("Connected");
                    }
                    // Control packets share the frame writer, so they cannot split a JPEG.
                    if (!d69 && (!heartbeat.IsRunning || heartbeat.Elapsed >= TimeSpan.FromSeconds(10)))
                    {
                        await SendWholeFrameAsync(connection.HeartbeatAsync, cancellationToken).ConfigureAwait(false);
                        heartbeat.Restart();
                        logger.Debug("D92 heartbeat sent for {SerialNumber}", serialNumber);
                    }
                    var frameWatch = Stopwatch.StartNew();
                    var jpeg = await render(cancellationToken).ConfigureAwait(false);
                    if (skipRepeatedStillFrames && !d69 && ReferenceEquals(lastWrittenStill, jpeg))
                    {
                        var idleDelay = frameInterval - timer.Elapsed;
                        if (idleDelay > TimeSpan.Zero) await Task.Delay(idleDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    var renderMilliseconds = frameWatch.Elapsed.TotalMilliseconds;
                    frameWatch.Restart();
                    await SendWholeFrameAsync(ct => connection.SendAsync(jpeg, ct), cancellationToken).ConfigureAwait(false);
                    if (skipRepeatedStillFrames && !d69) lastWrittenStill = jpeg;
                    if (d69)
                        await SendWholeFrameAsync(connection.HeartbeatAsync, cancellationToken).ConfigureAwait(false);
                    frames++;
                    var timing = new D92FrameTiming(frames, jpeg.Length, renderMilliseconds, frameWatch.Elapsed.TotalMilliseconds);
                    try { FrameCompleted?.Invoke(timing); } catch (Exception metricsError) { logger.Warning(metricsError, "D92 frame metrics listener failed"); }
                    if (frames == 1 || previousState == "Connected" || frames % 30 == 0) Publish("Streaming", force: true);
                }
                catch (Exception exception) when (exception is IOException or Win32Exception ||
                    exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    if (connection is not null) { await connection.DisposeAsync().ConfigureAwait(false); connection = null; }
                    if (manualEndpoint) throw new InvalidOperationException("手工确认的设备连接已变化，请重新确认目标。", exception);
                    Publish("WaitingForDevice", exception.Message);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                var delay = frameInterval - timer.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { Publish("Failed", exception.Message); throw; }
        finally
        {
            try { if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                serialGuard?.Dispose(); acquired?.Dispose();
                Interlocked.Exchange(ref _running, 0);
                if (previousState is not ("Failed" or "BlockedByCraft")) Publish("Stopped", force: true);
            }
        }
    }

    internal static async Task SendWholeFrameAsync(Func<CancellationToken, Task> send, CancellationToken stop)
    {
        stop.ThrowIfCancellationRequested();
        // Finish a started JPEG before honoring normal stop; truncated HID frames may confuse
        // the receiver. A stalled or disconnected device still has an independent deadline.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await send(deadline.Token).ConfigureAwait(false);
    }

    internal static D92DeviceInfo[] SelectCandidates(IEnumerable<D92DeviceInfo> devices, string serial, DeviceDescriptor? confirmed) =>
        devices.Where(d => d.ModelKey == (confirmed?.ModelKey ?? "D92") && d.SerialNumber == serial && (confirmed?.Identity.Confidence != IdentityConfidence.UserConfirmed
            || d.Path == confirmed.Endpoint.Address)).ToArray();

    private static bool IsCraftRunning()
    {
        var processes = Process.GetProcessesByName("MiraBox Craft");
        try { return processes.Length != 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
