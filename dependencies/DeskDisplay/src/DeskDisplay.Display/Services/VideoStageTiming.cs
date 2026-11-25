namespace DeskDisplay.Display.Services;

/// <summary>Optional P0 measurements. NativeRead includes waiting and readback; stages do not imply parallel CPU utilization.</summary>
public sealed record VideoStageTiming(double NativeReadMs, double ManagedCopyMs, double BitmapCreateMs,
    double CompositionMs, double RotationMs, double JpegEncodeMs, double JpegCopyMs,
    int SourceWidth, int SourceHeight, bool Paused, bool ReusedJpeg, VideoNativeReadTiming? Native)
{ public VideoCompositionTiming? Composition { get; init; } public double StandaloneNormalizeMs {get;init;} }

public sealed record VideoCompositionTiming(bool FastPath,double OverlayRenderAndCopyMs,double InputCopyMs,double BlendMs,double BitmapCreateMs)
{ public int BlendVisitedPixels { get; init; } public OverlayRedrawCause RedrawCause { get; init; } public double BackgroundNormalizeMs { get; init; } }

[Flags]
public enum OverlayRedrawCause { None=0,ContentChanged=1,Clock=2,Animation=4 }

public sealed record VideoNativeReadTiming(double WaitMs, double SurfaceCopyMs, double ReadbackMs, double PixelCopyMs);
