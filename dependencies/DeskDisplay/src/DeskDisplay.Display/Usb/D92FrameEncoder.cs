using System.Buffers.Binary;

namespace DeskDisplay.Display.Usb;

// Experimental CRT/DRA branch reconstructed from SDLibrary1.dll.
// Header matches vendor fixtures and observed CRT/DRA headers; field12 semantics remain unknown.
// No hardware access, vendor DLL loading, or firmware/upload commands.
public static class D92FrameEncoder
{
    public const int HeaderLength = 32;

    // SDLibrary1.dll SHA256 533926...2c397, sendWakeUpScreenPack RVA 0x31310.
    // Report ID precedes CRT\0\0DIS; remaining bytes are zero (non-LMA branch).
    public static byte[] WakeReport(int reportLength, byte reportId)
    {
        if (reportLength is < 9 or > 65536) throw new ArgumentOutOfRangeException(nameof(reportLength));
        var report = new byte[reportLength];
        report[0] = reportId;
        "CRT\0\0DIS"u8.CopyTo(report.AsSpan(1));
        return report;
    }

    // Same vendor binary, addHeartbeatPack RVA 0x22830: CRT\0\0CONNECT payload.
    public static byte[] HeartbeatReport(int reportLength, byte reportId)
    {
        if (reportLength is < 13 or > 65536) throw new ArgumentOutOfRangeException(nameof(reportLength));
        var report = new byte[reportLength];
        report[0] = reportId;
        "CRT\0\0CONNECT"u8.CopyTo(report.AsSpan(1));
        return report;
    }

    public static byte[] Header(int payloadLength, byte field12, int width, int height, int x, int y)
    {
        if (payloadLength < 0 || payloadLength > int.MaxValue - HeaderLength)
            throw new ArgumentOutOfRangeException(nameof(payloadLength));
        if (width is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(height));
        if ((width == 0 || height == 0) && (width != 0 || height != 0 || x != 0 || y != 0))
            throw new ArgumentException("Full-frame geometry must have zero width, height, x and y");
        if (x is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(x));
        if (y is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(y));

        var header = new byte[HeaderLength];
        "CRT\0\0DRA"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), (uint)(payloadLength + HeaderLength));
        header[12] = field12;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(13), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(15), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(17), (ushort)x);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(19), (ushort)y);
        return header;
    }

    // Candidate unnumbered-chunk branch: header+payload stream split into reports.
    // Report ID and length must be supplied; no hard-coded D92 size or report ID.
    public static IEnumerable<byte[]> Reports(byte[] header, byte[] payload, int reportLength, byte reportId)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(payload);
        if (header.Length != HeaderLength || !header.AsSpan(0, 8).SequenceEqual("CRT\0\0DRA"u8))
            throw new ArgumentException("Expected a CRT/DRA header", nameof(header));
        if (BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8)) != (long)HeaderLength + payload.Length)
            throw new ArgumentException("Header payload length mismatch", nameof(payload));
        if (reportLength is < 2 or > 65536) throw new ArgumentOutOfRangeException(nameof(reportLength));
        var total = checked(HeaderLength + payload.Length);
        var capacity = reportLength - 1;
        for (var offset = 0; offset < total;)
        {
            var report = new byte[reportLength];
            report[0] = reportId;
            var count = Math.Min(capacity, total - offset);
            var headCount = offset < HeaderLength ? Math.Min(count, HeaderLength - offset) : 0;
            if (headCount > 0) Array.Copy(header, offset, report, 1, headCount);
            if (count > headCount)
                Array.Copy(payload, offset + headCount - HeaderLength, report, 1 + headCount, count - headCount);
            yield return report;
            offset += count;
        }
    }
}

