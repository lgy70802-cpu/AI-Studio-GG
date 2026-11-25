using DeskDisplay.Display.Usb;
namespace D92.ProtocolProbe;
// Existing diagnostic entrypoints now exercise the production display-layer encoder.
internal static class CandidateFrameEncoder
{
    public const int HeaderLength = D92FrameEncoder.HeaderLength;
    public static byte[] Header(int length, byte field12, int width, int height, int x, int y)
        => D92FrameEncoder.Header(length, field12, width, height, x, y);
    public static IEnumerable<byte[]> Reports(byte[] header, byte[] payload, int reportLength, byte reportId)
        => D92FrameEncoder.Reports(header, payload, reportLength, reportId);
}
