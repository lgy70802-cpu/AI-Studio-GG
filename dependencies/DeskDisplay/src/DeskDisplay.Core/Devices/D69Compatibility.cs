namespace DeskDisplay.Core.Devices;

/// <summary>Conservative initial profile from the 2026-10-31 physical clock acceptance.</summary>
public static class D69Compatibility
{
    public static DisplayCapabilities Capabilities => new(CapabilityEvidence.Declared,
        CapabilityEvidence.Declared, CapabilityEvidence.Unknown, 1);

    public static void Validate(DisplayProfile profile)
    {
        profile.Validate();
        if (profile.SurfaceSize != new PixelSize(1280, 480) || profile.TransportSize != new PixelSize(480, 1280)
            || profile.ContentToTransportRotation != 90)
            throw new NotSupportedException("D69 实验输出目前仅验证横屏1280×480、传输旋转90°；其他方向尚未开放。");
        if (profile.Capabilities.Video != CapabilityEvidence.Unknown || profile.Capabilities.VerifiedFramesPerSecond is > 1)
            throw new NotSupportedException("D69 目前仅开放每秒更新的静态与时钟输出，视频尚未验证。");
    }
}
