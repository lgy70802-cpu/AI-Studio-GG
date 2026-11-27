using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace D92.ProtocolProbe;

// Experimental RAM display and bounded clock sessions. No mode, brightness, firmware or flash commands.
internal static class DirectImageSender
{
    private sealed record Plan(string DevicePath, string ImagePath, byte Field12, byte ReportId, int Width, int Height,
        int HoldSeconds = 0, string DeviceModel = "D92", string SerialNumber = "", int SessionSeconds = 0,
        bool LiveClock = false);

    public static async Task Send(string planPath)
    {
        var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(planPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ArgumentException("Missing plan");
        var d69 = plan.DeviceModel == "D69";
        if (plan.DeviceModel != "D92" && !d69) throw new ArgumentException("Unsupported probe model");
        ushort vid = d69 ? (ushort)0x2100 : (ushort)0x5548;
        ushort pid = d69 ? (ushort)0x0027 : (ushort)0x1011;
        if (!plan.DevicePath.Contains($"vid_{vid:x4}&pid_{pid:x4}", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Device path does not match the explicitly selected probe model");
        if (d69 && (string.IsNullOrWhiteSpace(plan.SerialNumber) || plan.HoldSeconds != 0 ||
            !((plan.Width == 1280 && plan.Height == 480) || (plan.Width == 480 && plan.Height == 1280))))
            throw new ArgumentException("D69 probe requires an explicit serial and a 1280x480 or 480x1280 test JPEG");
        if (plan.Field12 != 0xb1 || plan.ReportId != 0)
            throw new ArgumentException("Only the observed full-frame JPEG variant is enabled");
        if (plan.HoldSeconds is < 0 or > 60) throw new ArgumentOutOfRangeException(nameof(plan.HoldSeconds));
        if (plan.SessionSeconds is < 0 or > 120 || (!d69 && plan.SessionSeconds != 0))
            throw new ArgumentException("Only the D69 diagnostic may hold a session, bounded to 120 seconds");
        if (plan.LiveClock && (!d69 || plan.SessionSeconds == 0 || plan.Width != 480 || plan.Height != 1280))
            throw new ArgumentException("Live clock requires a bounded D69 session with 480x1280 transport");
        var processes = Process.GetProcessesByName("MiraBox Craft");
        try { if (processes.Length != 0) throw new InvalidOperationException("Exit Craft before direct USB output"); }
        finally { foreach (var process in processes) process.Dispose(); }
        var studioProcesses = Process.GetProcessesByName("DeskDisplay.App");
        try { if (studioProcesses.Length != 0) throw new InvalidOperationException("Exit D92 Studio before direct USB output"); }
        finally { foreach (var process in studioProcesses) process.Dispose(); }
        var info = new FileInfo(plan.ImagePath);
        if (info.Length is < 4 or > 16 * 1024 * 1024) throw new ArgumentException("Invalid image size");
        var jpeg = File.ReadAllBytes(plan.ImagePath);
        if (jpeg[0] != 0xff || jpeg[1] != 0xd8 || jpeg[^2] != 0xff || jpeg[^1] != 0xd9)
            throw new ArgumentException("Expected a complete JPEG");
        using (var source = new MemoryStream(jpeg, writable: false))
        {
            var image = new JpegBitmapDecoder(source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            if (image.PixelWidth != plan.Width || image.PixelHeight != plan.Height)
                throw new ArgumentException("JPEG does not match the explicitly selected display dimensions");
        }
        await using var clock = plan.LiveClock ? new DiagnosticClockFrames() : null;
        if (clock is not null) jpeg = await clock.RenderAsync(DateTimeOffset.Now);
        // Experimental D69 access is exclusive: never take over an already open device.
        using var handle = CreateFile(plan.DevicePath, 0x40000000, d69 ? 0u : 3u, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var attributes = new Attributes { Size = Marshal.SizeOf<Attributes>() };
        if (!HidD_GetAttributes(handle, ref attributes)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (attributes.VendorId != vid || attributes.ProductId != pid)
            throw new InvalidOperationException("Device identity changed");
        if (!string.IsNullOrEmpty(plan.SerialNumber))
        {
            var serial = new byte[254];
            if (!HidD_GetSerialNumberString(handle, serial, serial.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (System.Text.Encoding.Unicode.GetString(serial).TrimEnd('\0') != plan.SerialNumber)
                throw new InvalidOperationException("Selected device serial changed");
        }
        if (!HidD_GetPreparsedData(handle, out var parsed)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Caps caps;
        try
        {
            var status = HidP_GetCaps(parsed, out caps);
            if (status != 0x110000) throw new InvalidOperationException($"HidP_GetCaps: {status:X}");
        }
        finally { HidD_FreePreparsedData(parsed); }
        if (caps.UsagePage != 0xffa0 || caps.Usage != 1 || caps.Output < 33)
            throw new InvalidOperationException("Unexpected HID interface");
        if (d69 && (caps.Input != 513 || caps.Output != 1025 || caps.Feature != 0))
            throw new InvalidOperationException("D69 capabilities differ from the inspected unit");

        using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 1, isAsync: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(plan.HoldSeconds + plan.SessionSeconds + 10));
        var timer = Stopwatch.StartNew();
        var count = 0;
        var frames = 0;
        async Task WriteFrame(byte[] payload)
        {
            using var frameDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            frameDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            var frameHeader = CandidateFrameEncoder.Header(payload.Length, plan.Field12, 0, 0, 0, 0);
            foreach (var report in CandidateFrameEncoder.Reports(frameHeader, payload, caps.Output, plan.ReportId))
            {
                await stream.WriteAsync(report, frameDeadline.Token);
                count++;
            }
            frames++;
        }
        // D69 evidence: vendor SDLibrary1 sendWakeUpScreenPack RVA 0x32b70.
        // Only volatile wake and images; no device mode, flash or firmware operations.
        if (d69)
            await stream.WriteAsync(DeskDisplay.Display.Usb.D92FrameEncoder.WakeReport(caps.Output, 0), deadline.Token);
        do
        {
            await WriteFrame(jpeg);
            if (frames == 1)
                Console.WriteLine(JsonSerializer.Serialize(new { result = "first_frame_written", craftRunning = false, holdSeconds = plan.HoldSeconds }));
            if (plan.HoldSeconds == 0) break;
            await Task.Delay(1000, deadline.Token);
        } while (timer.Elapsed.TotalSeconds < plan.HoldSeconds);
        var heartbeats = 0;
        if (plan.SessionSeconds > 0)
        {
            // Diagnostic cadence, not a claim about the firmware's required interval.
            // A single writer sends CONNECT between complete images. No retries or mode changes.
            var sessionTimer = Stopwatch.StartNew();
            while (sessionTimer.Elapsed.TotalSeconds < plan.SessionSeconds)
            {
                if (clock is not null && heartbeats > 0)
                {
                    var sampledTime = DateTimeOffset.Now;
                    jpeg = await clock.RenderAsync(sampledTime);
                    await WriteFrame(jpeg);
                    Console.WriteLine(JsonSerializer.Serialize(new { result = "clock_frame_written", frames,
                        sampledTime, jpegBytes = jpeg.Length, elapsedMs = timer.ElapsedMilliseconds }));
                }
                await stream.WriteAsync(DeskDisplay.Display.Usb.D92FrameEncoder.HeartbeatReport(caps.Output, 0), deadline.Token);
                heartbeats++;
                Console.WriteLine(JsonSerializer.Serialize(new { result = "session_heartbeat_written", heartbeats,
                    elapsedMs = timer.ElapsedMilliseconds, sessionSeconds = plan.SessionSeconds }));
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(1000,
                    Math.Max(1, plan.SessionSeconds * 1000 - sessionTimer.ElapsedMilliseconds))), deadline.Token);
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { result = "all_reports_written", jpegBytes = jpeg.Length,
            frames, reports = count, reportBytes = caps.Output, elapsedMs = timer.ElapsedMilliseconds,
            heartbeats, sessionSeconds = plan.SessionSeconds, liveClock = plan.LiveClock,
            craftRunning = false, deviceModel = plan.DeviceModel, serialNumber = plan.SerialNumber,
            visualConfirmation = "pending", coldStartVerified = false }));
    }

    [StructLayout(LayoutKind.Sequential)] private struct Attributes
    { public int Size; public ushort VendorId, ProductId, Version; }
    [StructLayout(LayoutKind.Sequential)] private struct Caps
    {
        public ushort Usage, UsagePage, Input, Output, Feature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort Nodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out Caps caps);
}
