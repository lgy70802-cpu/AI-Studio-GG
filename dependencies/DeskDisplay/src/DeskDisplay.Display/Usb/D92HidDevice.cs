using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DeskDisplay.Display.Usb;

public sealed record D92DeviceInfo(string Path, string SerialNumber, int ReportLength, string ModelKey = "D92");

/// <summary>Native HID boundary. Enumeration never sends device commands.</summary>
public static class D92HidDevice
{
    public static async Task WakeAsync(string serialNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        var matches = Enumerate().Where(d => d.SerialNumber == serialNumber).ToArray();
        if (matches.Length != 1) throw new IOException("Expected exactly one device matching the selected serial");
        await using var connection = Open(matches[0]);
        await connection.WakeAsync(cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<D92DeviceInfo> Enumerate() => EnumerateModel("D92");
    public static IReadOnlyList<D92DeviceInfo> EnumerateSupported() => EnumerateModels(["D92", "D69"]);
    internal static (ushort Vid, ushort Pid) ModelIdentity(string model) => model switch
    {
        "D92" => (0x5548, 0x1011), "D69" => (0x2100, 0x0027),
        _ => throw new NotSupportedException("Unsupported USB screen model")
    };
    internal static IReadOnlyList<D92DeviceInfo> EnumerateModel(string model) => EnumerateModels([model]);
    private static IReadOnlyList<D92DeviceInfo> EnumerateModels(IReadOnlyList<string> models)
    {
        foreach (var model in models) _ = ModelIdentity(model);
        Native.HidD_GetHidGuid(out var guid);
        var set = Native.SetupDiGetClassDevs(ref guid, null, 0, 18);
        if (set == new IntPtr(-1)) throw new Win32Exception();
        var devices = new List<D92DeviceInfo>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new Native.InterfaceData { Size = Marshal.SizeOf<Native.InterfaceData>() };
                if (!Native.SetupDiEnumDeviceInterfaces(set, 0, ref guid, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259) break;
                    throw new Win32Exception(error);
                }
                Native.SetupDiGetDeviceInterfaceDetail(set, ref data, 0, 0, out var required, 0);
                var detail = Marshal.AllocHGlobal(checked((int)required));
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, 0))
                        throw new Win32Exception();
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    var model = models.FirstOrDefault(candidate =>
                    {
                        var (vid, pid) = ModelIdentity(candidate);
                        return path.Contains($"vid_{vid:x4}&pid_{pid:x4}", StringComparison.OrdinalIgnoreCase);
                    });
                    if (model is null) continue;
                    using var handle = OpenHandle(path, write: false);
                    var caps = ReadCaps(handle);
                    if (model == "D69") VerifyUnnumberedReports(handle, caps);
                    var serial = Serial(handle);
                    if (caps.UsagePage == 0xffa0 && caps.Usage == 1 && caps.Output >= 33 && serial.Length > 0
                        && (model != "D69" || (caps.Input == 513 && caps.Output == 1025 && caps.Feature == 0)))
                        devices.Add(new(path, serial, caps.Output, model));
                }
                catch (Win32Exception) { /* Device may disappear while enumerating; retry on next snapshot. */ }
                catch (IOException) { /* An unsupported collection must not hide other connected devices. */ }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return devices;
    }

    internal static D92HidConnection Open(D92DeviceInfo device)
    {
        var (vid, pid) = ModelIdentity(device.ModelKey);
        var handle = OpenHandle(device.Path, write: true, exclusive: device.ModelKey == "D69");
        try
        {
            var attributes = new Native.Attributes { Size = Marshal.SizeOf<Native.Attributes>() };
            if (!Native.HidD_GetAttributes(handle, ref attributes)) throw new Win32Exception();
            if (attributes.VendorId != vid || attributes.ProductId != pid || Serial(handle) != device.SerialNumber)
                throw new IOException("Device identity changed while opening");
            var caps = ReadCaps(handle);
            if (caps.UsagePage != 0xffa0 || caps.Usage != 1 || caps.Output < 33)
                throw new IOException("Unexpected HID collection");
            if (device.ModelKey == "D69" && (caps.Input != 513 || caps.Output != 1025 || caps.Feature != 0))
                throw new IOException("Unexpected D69 HID report lengths");
            if (device.ModelKey == "D69") VerifyUnnumberedReports(handle, caps);
            return new D92HidConnection(new FileStream(handle, FileAccess.Write, 1, isAsync: true), caps.Output, device.ModelKey == "D69");
        }
        catch { handle.Dispose(); throw; }
    }

    private static SafeFileHandle OpenHandle(string path, bool write, bool exclusive = false)
    {
        // D69 has one writer, but permits read-only discovery handles while output is active.
        var handle = Native.CreateFile(path, write ? 0x40000000u : 0, exclusive ? 1u : 3u, 0, 3, write ? 0x40000000u : 0, 0);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }

    private static string Serial(SafeFileHandle handle)
    {
        var buffer = new byte[254];
        if (!Native.HidD_GetSerialNumberString(handle, buffer, buffer.Length)) throw new Win32Exception();
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private static void VerifyUnnumberedReports(SafeFileHandle handle, Native.Caps caps)
    {
        if (!Native.HidD_GetPreparsedData(handle, out var parsed)) throw new Win32Exception();
        try
        {
            for (var type = 0; type < 2; type++)
            {
                var seen = false;
                foreach (var values in new[] { true, false })
                {
                    ushort count = type == 0 ? (values ? caps.InputValues : caps.InputButtons) : (values ? caps.OutputValues : caps.OutputButtons);
                    if (count == 0) continue;
                    var items = new Native.ReportCapability[count];
                    var status = values ? Native.HidP_GetValueCaps(type, items, ref count, parsed)
                        : Native.HidP_GetButtonCaps(type, items, ref count, parsed);
                    if (status != 0x110000 || count > items.Length) throw new IOException("Cannot verify D69 report IDs");
                    for (var i = 0; i < count; i++)
                    {
                        if (items[i].ReportId != 0) throw new IOException("D69 numbered reports have not been verified");
                        seen = true;
                    }
                }
                if (!seen) throw new IOException("D69 report IDs are unknown");
            }
        }
        finally { Native.HidD_FreePreparsedData(parsed); }
    }

    private static Native.Caps ReadCaps(SafeFileHandle handle)
    {
        if (!Native.HidD_GetPreparsedData(handle, out var data)) throw new Win32Exception();
        try
        {
            if (Native.HidP_GetCaps(data, out var caps) != 0x110000) throw new IOException("Cannot read HID capabilities");
            return caps;
        }
        finally { Native.HidD_FreePreparsedData(data); }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Explicit, Size = 72)] internal struct ReportCapability { [FieldOffset(2)] public byte ReportId; }
        [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(int type, [Out] ReportCapability[] caps, ref ushort count, nint data);
        [DllImport("hid.dll")] internal static extern int HidP_GetButtonCaps(int type, [Out] ReportCapability[] caps, ref ushort count, nint data);
        [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData { public int Size; public Guid Class; public int Flags; public nint Reserved; }
        [StructLayout(LayoutKind.Sequential)] internal struct Attributes { public int Size; public ushort VendorId, ProductId, Version; }
        [StructLayout(LayoutKind.Sequential)] internal struct Caps
        {
            public ushort Usage, UsagePage, Input, Output, Feature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort Nodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
        }
        [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint SetupDiGetClassDevs(ref Guid guid, string? enumerator, nint parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] internal static extern bool SetupDiEnumDeviceInterfaces(nint set, nint device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool SetupDiGetDeviceInterfaceDetail(nint set, ref InterfaceData data, nint detail, uint size, out uint required, nint device);
        [DllImport("setupapi.dll")] internal static extern bool SetupDiDestroyDeviceInfoList(nint set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
        [DllImport("hid.dll", SetLastError = true)] internal static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
        [DllImport("hid.dll", SetLastError = true)] internal static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, int size);
        [DllImport("hid.dll", SetLastError = true)] internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out nint data);
        [DllImport("hid.dll")] internal static extern bool HidD_FreePreparsedData(nint data);
        [DllImport("hid.dll")] internal static extern int HidP_GetCaps(nint data, out Caps caps);
    }
}

internal sealed class D92HidConnection(FileStream stream, int reportLength, bool d69 = false) : IAsyncDisposable
{
    public Task WakeAsync(CancellationToken cancellationToken) =>
        stream.WriteAsync(D92FrameEncoder.WakeReport(reportLength, 0), cancellationToken).AsTask();
    public Task HeartbeatAsync(CancellationToken cancellationToken) =>
        stream.WriteAsync(D92FrameEncoder.HeartbeatReport(reportLength, 0), cancellationToken).AsTask();

    public async Task SendAsync(byte[] jpeg, CancellationToken cancellationToken)
    {
        if (jpeg.Length is < 4 or > 16 * 1024 * 1024 || jpeg[0] != 255 || jpeg[1] != 216 || jpeg[^2] != 255 || jpeg[^1] != 217)
            throw new ArgumentException("Expected a bounded complete JPEG", nameof(jpeg));
        if (d69)
        {
            using var source = new MemoryStream(jpeg, writable: false);
            var frame = new System.Windows.Media.Imaging.JpegBitmapDecoder(source,
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            if (frame.PixelWidth != 480 || frame.PixelHeight != 1280)
                throw new ArgumentException("D69 requires 480x1280 transport JPEG; frame was not sent", nameof(jpeg));
        }
        var header = D92FrameEncoder.Header(jpeg.Length, 0xb1, 0, 0, 0, 0);
        foreach (var report in D92FrameEncoder.Reports(header, jpeg, reportLength, 0))
            await stream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
    }
    public ValueTask DisposeAsync() => stream.DisposeAsync();
}
