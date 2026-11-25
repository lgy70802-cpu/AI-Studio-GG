using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Rendering;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class MultiDeviceSessionTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var provider = new WindowsDeviceLeaseProvider(directory);
            var otherProvider = new WindowsDeviceLeaseProvider(directory);
            using (var first = provider.TryAcquire("cross-instance"))
            {
                Check(first is not null && otherProvider.TryAcquire("cross-instance") is null, "Windows handle lock excludes another provider instance");
                using var different = otherProvider.TryAcquire("another-device");
                Check(different is not null, "Windows locks allow different devices concurrently");
                var process = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                process.Environment["DESKDISPLAY_LEASE_PROBE_DIRECTORY"] = directory;
                using var child = Process.Start(process)!;
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Check(child.ExitCode == 42, "Second Windows process cannot acquire held device lock");
            }
            using (var free = otherProvider.TryAcquire("cross-instance")) Check(free is not null, "Released Windows handle lock can be reacquired");
            var serial = "offline-session-test";
            using (var ownership = WindowsDeviceLeaseProvider.Shared.TryAcquire(OutputDeviceKeys.D92(serial)))
            {
                var blocked = new D92HidRenderTarget(logger);
                var failed = false;
                try { await blocked.RunAsync(serial, _ => throw new Exception("must not render"), TimeSpan.FromSeconds(1), default); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed, "Legacy HID sender refuses managed ownership before hardware enumeration or rendering");
            }
            var monitor = new DisplayInfo("offline-monitor", "Test", "offline-monitor-id", 0, 0, 320, 80, false);
            using (var ownership = WindowsDeviceLeaseProvider.Shared.TryAcquire(OutputDeviceKeys.WindowsDisplay(monitor.DeviceId!)))
            {
                var created = false;
                using var legacy = new RenderWindowService(logger, new ClockScene(), () => { created = true; return new Surface(); });
                legacy.ShowTest(monitor);
                Check(!created && !legacy.IsOpen, "Legacy window cannot bypass managed device lease");
            }

            var usb = WindowsDeviceDiscovery.DescribeUsb([new("path-A", "serial-A", 513), new("path-B", "serial-B", 513)]);
            Check(usb.Count == 2 && usb.Select(d => d.DeviceId).Distinct().Count() == 2, "Windows discovery retains same-model different-serial devices");
            Check(WindowsDeviceDiscovery.DescribeUsb([new("one", "duplicate", 513), new("two", "duplicate", 513)])
                .All(d => d.Identity.Confidence == IdentityConfidence.Ambiguous), "Duplicate HID serials become ambiguous candidates");
            var mirrored = WindowsDeviceDiscovery.DescribeDisplays([monitor, monitor with { DeviceId = "second-id" }]);
            Check(mirrored.All(d => d.Identity.Confidence == IdentityConfidence.Ambiguous), "Mirrored Windows source is not advertised as independent output");

            var project = new ThemeProject
            {
                Name = "Isolated render test",
                Landscape = new ThemeLayout { Width = 320, Height = 80, Background = "#112233", Layers =
                [new ThemeLayer { Kind = ThemeElementKind.Rectangle, X = 0, Y = 0, Width = 80, Height = 80, Color = "#FF0000" },
                 new ThemeLayer { Kind = ThemeElementKind.DigitalClock, X = 100, Y = 10, Width = 200, Height = 60, FontSize = 20, ShowSeconds = true }] }
            };
            var theme = LegacyThemeAdapter.Read(project);
            var sourceBytes = JsonSerializer.Serialize(project);
            var registry = new DeviceRegistry(); registry.Replace(DeviceTransport.UsbHid, usb);
            // Starting is not first-frame completion: keep explicit per-target completion signals.
            var targets = usb.ToDictionary(d => d.DeviceId, _ => new PullTarget());
            using var fakeDisplays = new Displays();
            var factory = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, fakeDisplays, logger, usbTargetFactory: context =>
                targets[context.Device.DeviceId]);
            await using var sessions = new OutputSessionManager(registry, provider, factory.CreateAsync);
            var capabilities = new DisplayCapabilities(CapabilityEvidence.Declared, CapabilityEvidence.Declared, CapabilityEvidence.Unknown);
            var profileA = new DisplayProfile("offline-A", new(320, 80), new(80, 320), 90, capabilities);
            var profileB = new DisplayProfile("offline-B", new(640, 160), new(640, 160), 0, capabilities);
            Check((await sessions.StartAsync(usb[0].DeviceId, profileA, theme)).Started, "Windows factory starts first isolated session with fake transport");
            Check((await sessions.StartAsync(usb[1].DeviceId, profileB, theme)).Started, "Windows factory starts second isolated session with fake transport");
            var firstA = await targets[usb[0].DeviceId].First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var firstB = await targets[usb[1].DeviceId].First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(Size(firstA) == (80, 320) && Size(firstB) == (640, 160), "Real WPF JPEGs honor independent surface size and transport rotation");
            Check(IsRed(firstB, 40, 80), "Resolved layer geometry renders at uniformly scaled target coordinates");
            Check(JsonSerializer.Serialize(project) == sourceBytes, "Real session rendering never changes source project dimensions or layers");
            await sessions.StopAsync(usb[0].DeviceId);
            await Task.Delay(1100);
            targets[usb[1].DeviceId].Next.TrySetResult();
            await targets[usb[1].DeviceId].Second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(targets[usb[0].DeviceId].Frames == 1 && targets[usb[1].DeviceId].Frames == 2, "Stopping one real renderer session leaves peer able to render next JPEG");
            Check(!firstB.SequenceEqual(targets[usb[1].DeviceId].Last!), "Independent theme clock updates from real system time across frames");
            await sessions.StopAsync(usb[1].DeviceId);
            Check(sessions.Snapshot.All(s => s.Snapshot.ActualState == OutputActualState.Stopped), "Both isolated renderers cleanly stop and release resources");

            var badProject = project.Clone(); badProject.Landscape.Layers[0].IsVideo = true;
            Check(!(await sessions.StartAsync(usb[0].DeviceId, profileA, LegacyThemeAdapter.Read(badProject))).Started,
                "Managed D92 rejects video during preparation without sending a command");
            Check(!(await sessions.StartAsync(usb[0].DeviceId, profileA with { Capabilities = capabilities with { VerifiedFramesPerSecond = .5 } }, theme)).Started,
                "Slower verified throughput is rejected rather than exceeded by one-second sender");
            Check((await sessions.StartAsync(usb[0].DeviceId, profileA with { Capabilities = capabilities with { LiveScene = CapabilityEvidence.Unknown } }, theme)).Code == "CapabilityUnknown",
                "Still-image capability alone cannot authorize live-clock output");

            var monitorB = monitor with { DeviceId = "offline-monitor-B", DeviceName = "offline-source-B", Width = 640, Height = 160 };
            fakeDisplays.Snapshot = [monitor, monitorB];
            var monitorDevices = WindowsDeviceDiscovery.DescribeDisplays(fakeDisplays.Snapshot);
            registry.Replace(DeviceTransport.SystemDisplay, monitorDevices);
            var surfaces = new Dictionary<string, Surface>();
            var windows = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, fakeDisplays, logger,
                windowFactory: (context, scene) => new RenderWindowService(logger, scene, () =>
                {
                    var surface = new Surface(); surfaces[context.Device.DeviceId] = surface; return surface;
                }, context.Ownership));
            await using var windowSessions = new OutputSessionManager(registry, provider, windows.CreateAsync);
            await windowSessions.StartAsync(monitorDevices[0].DeviceId, profileA with { ContentToTransportRotation = 0, TransportSize = profileA.SurfaceSize }, theme);
            await windowSessions.StartAsync(monitorDevices[1].DeviceId, profileB, theme);
            await UntilAsync(() => surfaces.Count == 2);
            Check(!ReferenceEquals(surfaces[monitorDevices[0].DeviceId].Scene, surfaces[monitorDevices[1].DeviceId].Scene),
                "Two managed Windows outputs own distinct ThemeScene instances");
            Check(surfaces[monitorDevices[0].DeviceId].Scene!.PixelWidth == 320 && surfaces[monitorDevices[1].DeviceId].Scene!.PixelWidth == 640,
                "Windows session scenes preserve independent output dimensions");
            var oldSurface = surfaces[monitorDevices[0].DeviceId];
            fakeDisplays.Snapshot = [monitorB];
            await UntilAsync(() => windowSessions.Snapshot.Single(s => s.Snapshot.DeviceId == monitorDevices[0].DeviceId).Snapshot.ActualState == OutputActualState.WaitingForDevice);
            Check(oldSurface.Disposed && !surfaces[monitorDevices[1].DeviceId].Disposed, "Windows unplug disposes only disconnected target surface");
            using (var held = provider.TryAcquire(monitorDevices[0].DeviceId)) Check(held is null, "Windows waiting session keeps logical device ownership");
            fakeDisplays.Snapshot = [monitor, monitorB];
            await UntilAsync(() => !ReferenceEquals(oldSurface, surfaces[monitorDevices[0].DeviceId]));
            Check(!surfaces[monitorDevices[0].DeviceId].Disposed, "Windows simulated reconnect recreates only its own surface");
            await windowSessions.StopAsync(monitorDevices[0].DeviceId);
            Check(surfaces[monitorDevices[0].DeviceId].Disposed && !surfaces[monitorDevices[1].DeviceId].Disposed,
                "Explicit Windows stop leaves peer window session alive");
            await windowSessions.DisposeAsync();

            var imagePath = Path.Combine(directory, "test.png");
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 16).ToArray(), 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(imagePath)) encoder.Save(stream);
            var pool = new SessionImagePool();
            var image1 = await pool.AcquireAsync(imagePath, default); var image2 = await pool.AcquireAsync(imagePath, default);
            Check(ReferenceEquals(image1.Image, image2.Image), "Active sessions share immutable decoded image pixels");
            image1.Dispose();
            Check(((DeskDisplay.Display.DataProviders.WpfSceneImage)image2.Image.Image).Bitmap.IsFrozen, "Releasing one image reference preserves other session's frozen pixels");
            image2.Dispose();
            using var image3 = await pool.AcquireAsync(imagePath, default);
            Check(!ReferenceEquals(image2.Image, image3.Image), "Last reference release removes retained image entry");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static (int, int) Size(byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg); var bitmap = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        return (bitmap.PixelWidth, bitmap.PixelHeight);
    }
    private static bool IsRed(byte[] jpeg, int x, int y)
    {
        using var stream = new MemoryStream(jpeg);
        var source = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var rgb = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[rgb.PixelWidth * rgb.PixelHeight * 4]; rgb.CopyPixels(pixels, rgb.PixelWidth * 4, 0);
        var i = (y * rgb.PixelWidth + x) * 4;
        return pixels[i + 2] > 200 && pixels[i] < 60 && pixels[i + 1] < 60;
    }
    private static async Task UntilAsync(Func<bool> ready)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready()) await Task.Delay(10, deadline.Token);
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private sealed class PullTarget : ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;
        public TaskCompletionSource<byte[]> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Next { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Second { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Frames { get; private set; }
        public byte[]? Last { get; private set; }
        public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken ct)
        {
            var jpeg = await render(ct); Last = jpeg; Frames++; StateChanged?.Invoke(new("Streaming", Frames, 1)); First.TrySetResult(jpeg);
            await Next.Task.WaitAsync(ct); Last = await render(ct); Frames++; Second.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }
    private sealed class Displays : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Snapshot { get; set; } = [];
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
        public void Dispose() { }
    }
    private sealed class Surface : IRenderSurface
    {
        public Scene? Scene { get; private set; }
        public bool Disposed { get; private set; }
        public event EventHandler? Closed { add { } remove { } }
        public event Action<Exception>? Faulted { add { } remove { } }
        public event EventHandler? TopologyInvalidated { add { } remove { } }
        public void ShowOnDisplay(DisplayInfo target) { }
        public void Render(Scene scene) => Scene = scene;
        public void Dispose() => Disposed = true;
    }
}
