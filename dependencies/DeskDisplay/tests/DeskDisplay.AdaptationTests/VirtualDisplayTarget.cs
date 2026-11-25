using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;

namespace DeskDisplay.AdaptationTests;

/// <summary>Test-only target: checks contract isolation, not real rendering, transport, reconnect or a production scheduler.</summary>
internal sealed class VirtualDisplayTarget(DeviceDescriptor device, DisplayProfile profile) : IAdaptiveDisplayTarget
{
    public DeviceDescriptor Device { get; private set; } = device;
    public DisplayProfile Profile { get; } = profile;
    public bool Disposed { get; private set; }
    public int Frames { get; private set; }
    public ResolvedLayout? LastFrame { get; private set; }
    public DateTimeOffset? LastTimestamp { get; private set; }

    public void SetConnected(bool connected) => Device = Device with
    {
        IsConnected = connected,
        Endpoint = Device.Endpoint with { ConnectionEpoch = Device.Endpoint.ConnectionEpoch + 1 }
    };

    public ValueTask PresentAsync(ResolvedLayout frame, DateTimeOffset dataTimestamp, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (!Device.IsConnected) throw new IOException("Virtual device disconnected.");
        Profile.Validate();
        if (frame.SurfaceSize != Profile.SurfaceSize) throw new ArgumentException("Wrong target resolution.");
        LastFrame = frame; LastTimestamp = dataTimestamp; Frames++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true; LastFrame = null;
        return ValueTask.CompletedTask;
    }
}
