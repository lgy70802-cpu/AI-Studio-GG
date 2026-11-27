using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class D69IntegrationTests
{
    private static DisplayProfile Profile => new("D69-experimental", new(1280, 480), new(480, 1280), 90, D69Compatibility.Capabilities);
    private static ThemeProject Project() => new()
    {
        Name = "D69 client session acceptance",
        Landscape = new ThemeLayout { Width = 1280, Height = 480, Background = "#152239", Layers =
        [new ThemeLayer { Kind = ThemeElementKind.Text, Text = "D69 STUDIO", X = 40, Y = 35, Width = 1200, Height = 90, FontSize = 56, Color = "#FFFFFF" },
         new ThemeLayer { Kind = ThemeElementKind.DigitalClock, X = 120, Y = 150, Width = 1040, Height = 220, FontSize = 140, ShowSeconds = true, Color = "#FFFFFF" }] }
    };

    public static async Task RunAsync(ILogger logger)
    {
        var devices = WindowsDeviceDiscovery.DescribeUsb([new("a", "same", 1025), new("b", "same", 1025, "D69")]);
        Check(devices.All(d => d.Identity.Confidence == IdentityConfidence.VerifiedStable), "Different model serial namespaces remain independent");
        Check(devices.Select(d => d.DeviceId).Distinct().Count() == 2, "D69 and D92 cannot share an output lease key");
        Check(WindowsDeviceDiscovery.DescribeUsb([new("a", "dup", 1025, "D69"), new("b", "dup", 1025, "D69")])
            .All(d => d.Identity.Confidence == IdentityConfidence.Ambiguous), "Duplicate D69 serials require confirmation");
        Check(OutputDeviceKeys.UsbScreen("D92", "x") == OutputDeviceKeys.D92("x"), "Existing D92 stable key unchanged");
        D69Compatibility.Validate(Profile);
        Reject(() => D69Compatibility.Validate(Profile with { ContentToTransportRotation = 0, TransportSize = new(1280, 480) }), "D69 rejects unrotated profile");
        Reject(() => D69Compatibility.Validate(Profile with { Capabilities = D69Compatibility.Capabilities with { Video = CapabilityEvidence.Declared } }), "D69 cannot declare video support");
        Reject(() => D69Compatibility.Validate(Profile with { Capabilities = D69Compatibility.Capabilities with { VerifiedFramesPerSecond = 30 } }), "D69 rejects unverified high rate");
        try
        {
            await new D92HidRenderTarget(logger, confirmedDevice: devices[1]).RunAsync("same", _ => throw new InvalidOperationException("Must not render"), TimeSpan.FromMilliseconds(100), default);
            throw new InvalidOperationException("D69 high frame rate accepted");
        }
        catch (NotSupportedException) { Check(true, "D69 target rejects high frame rate before enumeration or writes"); }
        var registry = new DeviceRegistry(); registry.Replace(DeviceTransport.UsbHid, [devices[1]]);
        using var displays = new Displays();
        var pull = new PullTarget();
        var factory = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, displays, logger, usbTargetFactory: _ => pull);
        await using var sessions = new OutputSessionManager(registry, new InMemoryDeviceLeaseProvider(), factory.CreateAsync);
        var preferencesPath = Path.Combine(Path.GetTempPath(), "D69-preferences-" + Guid.NewGuid().ToString("N") + ".json");
        using (var page = new ManagedDevicesViewModel(registry, sessions, new DevicePreferenceStore(preferencesPath),
            () => [], () => [], _ => Task.CompletedTask, logger,
            new UsbOutputSettings { SerialNumber = "same", Width = 1920, Height = 462, RotationDegrees = 90 }))
        {
            await page.InitializeAsync();
            var card = page.Cards.Single();
            Check(card.Width == "1280" && card.Height == "480" && card.Rotation == 90, "D69 defaults never inherit legacy D92 dimensions even with the same serial");
            Check(!card.Confirmed && card.CapabilityNotice.Contains("实验"), "D69 requires parameter confirmation and shows experimental limits");
            Check(page.GetProfile(card).Capabilities.Video == CapabilityEvidence.Unknown, "D69 device page cannot enable video capability");
            Check(!File.Exists(preferencesPath), "Discovering D69 does not overwrite user preferences");
        }
        var project = Project(); var before = JsonSerializer.Serialize(project);
        Check((await sessions.StartAsync(devices[1].DeviceId, Profile, LegacyThemeAdapter.Read(project))).Started, "D69 managed session accepts clock theme");
        var jpeg = await pull.First.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var input = new MemoryStream(jpeg);
        var bitmap = new JpegBitmapDecoder(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Check(bitmap.PixelWidth == 480 && bitmap.PixelHeight == 1280, "D69 production renderer preserves verified transport geometry");
        var transportFile = Path.Combine(Path.GetTempPath(), "D69-framing-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var renderer = new SceneJpegRenderer();
            var wrongScene = new ClockScene(); wrongScene.SetCanvasSize(1280, 480); wrongScene.UpdateTime(DateTimeOffset.Now);
            var wrongJpeg = renderer.Render(wrongScene, 0, 85);
            await using (var connection = new D92HidConnection(new FileStream(transportFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1, true), 1025, true))
            {
                try { await connection.SendAsync(wrongJpeg, default); throw new InvalidOperationException("Wrong JPEG accepted"); }
                catch (ArgumentException) { Check(new FileInfo(transportFile).Length == 0, "D69 rejects unsafe JPEG direction before writing any report"); }
                await connection.SendAsync(jpeg, default);
                Check(new FileInfo(transportFile).Length > 0 && new FileInfo(transportFile).Length % 1025 == 0, "D69 valid JPEG uses complete HID report framing");
            }
        }
        finally { if (File.Exists(transportFile)) File.Delete(transportFile); }
        Check(pull.Interval == TimeSpan.FromSeconds(1), "D69 managed session uses one-second frame interval");
        await sessions.StopAsync(devices[1].DeviceId);
        Check(JsonSerializer.Serialize(project) == before, "D69 application never rewrites theme source layout");
        Check(sessions.Snapshot.All(s => s.Snapshot.ActualState == OutputActualState.Stopped), "D69 stop releases managed session");
        var video = Project(); video.Landscape.Layers.Add(new ThemeLayer { Kind = ThemeElementKind.Wallpaper, Path = "not-opened.mp4", Width = 1280, Height = 480 });
        Check(!(await sessions.StartAsync(devices[1].DeviceId, Profile, LegacyThemeAdapter.Read(video))).Started, "D69 video rejected before loading file or opening hardware");
    }

    public static async Task RunHardwareAsync(ILogger logger)
    {
        foreach (var name in new[] { "DeskDisplay.App", "MiraBox Craft", "CRAFT" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length > 0) throw new IOException("Exit " + name + " before hardware acceptance"); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        var serial = Environment.GetEnvironmentVariable("D69_TEST_SERIAL") ?? throw new IOException("Explicit serial required");
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("D69_TEST_SECONDS"), out var requested) ? requested : 60;
        if (seconds is < 15 or > 120) throw new IOException("Hardware test duration must be 15..120 seconds");
        var directory = Environment.GetEnvironmentVariable("D69_TEST_EVIDENCE") ?? throw new IOException("New evidence path required");
        if (Directory.Exists(directory)) throw new IOException("Evidence already exists");
        var device = WindowsDeviceDiscovery.DescribeUsb(D92HidDevice.EnumerateSupported()).Single(d => d.ModelKey == "D69" && d.Identity.SerialNumber == serial);
        if (device.Identity.Confidence != IdentityConfidence.VerifiedStable) throw new IOException("Unique identity required");
        var peerSerial = Environment.GetEnvironmentVariable("D69_TEST_PEER_D92");
        var peer = string.IsNullOrEmpty(peerSerial) ? null :
            WindowsDeviceDiscovery.DescribeUsb(D92HidDevice.EnumerateSupported()).Single(d =>
                d.ModelKey == "D92" && d.Identity.SerialNumber == peerSerial && d.Identity.Confidence == IdentityConfidence.VerifiedStable);
        long peerFrames = 0;
        long peerFramesAfterD69Stop = 0;
        Directory.CreateDirectory(directory);
        var registry = new DeviceRegistry(); registry.Replace(DeviceTransport.UsbHid, peer is null ? [device] : [device, peer]);
        long frames = 0; var failures = new List<D92OutputState>(); var discoveryChecks = 0;
        var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var displays = new Displays();
        var factory = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, displays, logger, usbTargetFactory: context =>
        {
            var target = new D92HidRenderTarget(logger, context.Ownership, confirmedDevice: context.Device);
            target.FrameCompleted += _ => { if (context.Device.DeviceId == device.DeviceId) { Interlocked.Increment(ref frames); firstFrame.TrySetResult(); } else Interlocked.Increment(ref peerFrames); };
            target.StateChanged += state => { if (state.Status is "WaitingForDevice" or "Failed" or "BlockedByCraft") lock (failures) failures.Add(state); };
            return target;
        });
        await using var sessions = new OutputSessionManager(registry, WindowsDeviceLeaseProvider.Shared, factory.CreateAsync);
        var timer = Stopwatch.StartNew();
        var activeTimer = new Stopwatch();
        double startupSeconds = 0;
        try
        {
            if (!(await sessions.StartAsync(device.DeviceId, Profile, LegacyThemeAdapter.Read(Project()))).Started) throw new IOException("Managed start failed");
            if (peer is not null)
            {
                var peerProject = Project();
                peerProject.Name = "D92 dual session acceptance";
                peerProject.Landscape.Background = "#184A38";
                peerProject.Landscape.Layers[0].Text = "D92 STUDIO";
                var peerProfile = new DisplayProfile("D92-dual-acceptance", new(1920, 462), new(462, 1920), 90,
                    new(CapabilityEvidence.Declared, CapabilityEvidence.Declared, CapabilityEvidence.Unknown));
                if (!(await sessions.StartAsync(peer.DeviceId, peerProfile, LegacyThemeAdapter.Read(peerProject))).Started)
                    throw new IOException("D92 peer start failed");
            }
            await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(15));
            startupSeconds = timer.Elapsed.TotalSeconds; activeTimer.Start();
            while (activeTimer.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                await Task.Delay(1000);
                lock (failures) if (failures.Count > 0) throw new IOException("Device session changed unexpectedly");
                if ((int)activeTimer.Elapsed.TotalSeconds / 5 > discoveryChecks)
                {
                    var snapshot = await Task.Run(D92HidDevice.EnumerateSupported);
                    if (!snapshot.Any(d => d.ModelKey == "D69" && d.SerialNumber == serial)) throw new IOException("Discovery lost active D69");
                    discoveryChecks++;
                }
            }
        }
        finally
        {
            await sessions.StopAsync(device.DeviceId);
            if (peer is not null)
            {
                var before = Interlocked.Read(ref peerFrames);
                await Task.Delay(TimeSpan.FromSeconds(20));
                peerFramesAfterD69Stop = Interlocked.Read(ref peerFrames) - before;
                await sessions.StopAsync(peer.DeviceId);
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
            {
                HostWriteSucceeded = frames >= seconds - 5 && failures.Count == 0, Frames = frames, ElapsedSeconds = timer.Elapsed.TotalSeconds,
                ActiveSeconds = activeTimer.Elapsed.TotalSeconds, StartupSeconds = startupSeconds,
                DiscoveryChecks = discoveryChecks, PeerD92Frames = peerFrames, PeerD92FramesAfterD69Stop = peerFramesAfterD69Stop,
                Failures = failures, PhysicalDisplayConfirmed = false, VideoVerified = false, ReconnectVerified = false,
                StopBehavior = "Connection released; device may return to its logo"
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (peer is not null) Check(peerFrames >= seconds - 5 && peerFramesAfterD69Stop >= 4 && failures.Count == 0, "D92 continued writing frames independently after stopping D69");
        Check(frames >= seconds - 5, $"D69 production output chain sustained {seconds} seconds with discovery active");
    }

    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static void Reject(Action action, string name) { try { action(); } catch (NotSupportedException) { Check(true, name); return; } throw new InvalidOperationException(name); }
    private sealed class Displays : IDisplayManager
    {
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]);
        public void Dispose() { }
    }
    private sealed class PullTarget : ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;
        public TaskCompletionSource<byte[]> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TimeSpan Interval { get; private set; }
        public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken ct)
        {
            Interval = interval; First.TrySetResult(await render(ct)); StateChanged?.Invoke(new("Streaming", 1, 1));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }
}
