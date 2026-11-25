namespace DeskDisplay.Core.Devices;

public enum DeviceTransport { UsbHid, SystemDisplay, Virtual }
public enum IdentityConfidence { VerifiedStable, UserConfirmed, Ambiguous, Ephemeral }
public enum DisplayOrientation { Landscape, Portrait }
public enum CapabilityEvidence { Unknown, Declared, HardwareVerified }

/// <summary>Pixel sizes are not HID report lengths or UI DIPs.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public double AspectRatio => (double)Width / Height;
    public DisplayOrientation Orientation => Width >= Height ? DisplayOrientation.Landscape : DisplayOrientation.Portrait;
    public void Validate()
    {
        if (Width <= 0 || Height <= 0) throw new ArgumentOutOfRangeException(nameof(Width), "Pixel dimensions must be positive.");
    }
}

public sealed record DeviceIdentity(string Namespace, string? SerialNumber, IdentityConfidence Confidence);
public sealed record DeviceEndpoint(string EndpointId, string Address, long ConnectionEpoch);
public sealed record DeviceDescriptor(string DeviceId, string ModelKey, DeviceTransport Transport,
    DeviceIdentity Identity, DeviceEndpoint Endpoint, bool IsConnected);

/// <summary>Unknown capabilities stay unknown; a working JPEG does not prove video support.</summary>
public sealed record DisplayCapabilities(CapabilityEvidence StillImage, CapabilityEvidence LiveScene,
    CapabilityEvidence Video, double? VerifiedFramesPerSecond = null);

public sealed record DisplayProfile(string ProfileId, PixelSize SurfaceSize, PixelSize TransportSize,
    int ContentToTransportRotation, DisplayCapabilities Capabilities)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileId);
        SurfaceSize.Validate(); TransportSize.Validate();
        if (ContentToTransportRotation is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(ContentToTransportRotation));
        var expected = ContentToTransportRotation is 90 or 270
            ? new PixelSize(SurfaceSize.Height, SurfaceSize.Width) : SurfaceSize;
        if (TransportSize != expected) throw new ArgumentException("Transport pixels must match the explicit surface rotation.");
        ArgumentNullException.ThrowIfNull(Capabilities);
        if (Capabilities.VerifiedFramesPerSecond is { } fps && (!double.IsFinite(fps) || fps <= 0))
            throw new ArgumentOutOfRangeException(nameof(Capabilities));
    }
}

/// <summary>Persisted user preferences, separate from a theme and from pixel geometry.</summary>
public sealed record DeviceProfile(string DeviceId, string Alias, string? PreferredDisplayProfileId,
    bool IdentityConfirmed);

/// <summary>No path-derived identity is promoted to a stable identity automatically.</summary>
public static class DeviceIdentityRules
{
    public static string? GetStableId(DeviceIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Namespace);
        return identity.Confidence == IdentityConfidence.VerifiedStable && !string.IsNullOrWhiteSpace(identity.SerialNumber)
            ? $"{Uri.EscapeDataString(identity.Namespace)}:{Uri.EscapeDataString(identity.SerialNumber)}" : null;
    }

    public static bool RequiresConfirmation(DeviceDescriptor device, IEnumerable<DeviceDescriptor> discovered)
    {
        if (device.IsConnected && device.Identity.Confidence == IdentityConfidence.UserConfirmed) return false;
        var stable = GetStableId(device.Identity);
        return stable is null || discovered.Count(d => GetStableId(d.Identity) == stable) != 1;
    }
}
