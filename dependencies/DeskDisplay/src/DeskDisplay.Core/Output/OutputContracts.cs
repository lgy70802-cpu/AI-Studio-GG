using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Themes;

namespace DeskDisplay.Core.Output;

public enum OutputDesiredState { Stopped, Running }
public enum OutputActualState { Idle, Preparing, Starting, Streaming, WaitingForDevice, Suspended, Blocked, Failed, Stopping, Stopped }
public sealed record DeviceOutputAssignment(string DeviceId, Guid ThemeId, Guid? LayoutOverrideId,
    LayoutAdaptation Adaptation, bool ResumeAfterReconnect);
public sealed record OutputSessionSnapshot(Guid SessionId, string DeviceId, string EndpointId,
    long ConnectionEpoch, string DisplayProfileId, Guid ThemeId, long ThemeRevision,
    ResolvedLayout Layout, OutputDesiredState DesiredState, OutputActualState ActualState);

/// <summary>Platform adapter boundary. Implementations own their resources; no driver is introduced by this contract.</summary>
public interface IAdaptiveDisplayTarget : IAsyncDisposable
{
    DeviceDescriptor Device { get; }
    DisplayProfile Profile { get; }
    ValueTask PresentAsync(ResolvedLayout frame, DateTimeOffset dataTimestamp, CancellationToken cancellationToken);
}
