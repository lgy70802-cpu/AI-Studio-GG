using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Interop;
using DeskDisplay.Display.Services;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_LEASE_PROBE_DIRECTORY") is { } leaseDirectory)
        {
            using var probe = new DeskDisplay.Display.Services.WindowsDeviceLeaseProvider(leaseDirectory).TryAcquire("cross-instance");
            return probe is null ? 42 : 0;
        }
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var task = RunAsync();
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        try { task.GetAwaiter().GetResult(); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static async Task RunAsync()
    {
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="rc-workflow"){ReleaseCandidateTests.Run();using var rcLogger=new LoggerConfiguration().CreateLogger();await CreatorWorkflowTests.RunAsync(rcLogger);await ThemeQuickUseTests.RunAsync(rcLogger);await OutputExperienceTests.RunAsync(rcLogger);await PlaylistRecoveryTests.RunAsync(rcLogger);await UserWorkflowTests.RunAsync(rcLogger);await ClientVideoTests.RunAsync();return;}
        ReleaseCandidateTests.Run();
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-modes"){VideoModeTests.Run();await ClientVideoTests.RunAsync();await VideoSmoothnessTests.RunAsync();return;}
        VideoModeTests.Run();
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-aspect-fixture")
        {var path=Environment.GetEnvironmentVariable("DESKDISPLAY_ASPECT_DIRECTORY")??throw new IOException("New aspect directory required");if(Directory.Exists(path))throw new IOException("Directory exists");var width=uint.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_ASPECT_WIDTH"),out var w)?w:640;var height=uint.TryParse(Environment.GetEnvironmentVariable("DESKDISPLAY_ASPECT_HEIGHT"),out var h)?h:360;if(width is <1 or >1920||height is <1 or >1920)throw new IOException("Fixture dimensions out of bounds");Directory.CreateDirectory(path);Console.WriteLine(await VideoExperienceTests.MakeVideoAsync(path,width,height));return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-smoothness"){await VideoSmoothnessTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-pixels"){await VideoPixelOptimizationTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-buffers"){await VideoBufferOwnershipTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-p0"){await VideoP0Tests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-experience"){await VideoExperienceTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-overlay-benchmark"){await VideoOverlayBenchmark.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-feature-hardware"){await VideoFeatureHardwareTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-release"){using var videoLogger=new LoggerConfiguration().CreateLogger();await VideoExperienceTests.RunAsync();await ClientVideoTests.RunAsync();await ContentExperienceTests.RunAsync(videoLogger);return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="client-video"){await ClientVideoTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="video-frames"){await VideoFramePrototypeTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="ui-refactor"){await UiRefactorTests.RunAsync();return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="content-experience"){using var contentLogger=new LoggerConfiguration().CreateLogger();await ContentExperienceTests.RunAsync(contentLogger);return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="user-workflow"){using var workflowLogger=new LoggerConfiguration().CreateLogger();await UserWorkflowTests.RunAsync(workflowLogger);return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="playlist-recovery"){using var playlistLogger=new LoggerConfiguration().CreateLogger();await PlaylistRecoveryTests.RunAsync(playlistLogger);return;}
        if(Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE")=="output-experience"){using var outputLogger=new LoggerConfiguration().CreateLogger();await OutputExperienceTests.RunAsync(outputLogger);return;}
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-ordering") { using var orderingLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await CreatorOrderingTests.RunAsync(orderingLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "background-adaptation") { using var backgroundLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await BackgroundAdaptationTests.RunAsync(backgroundLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "window-chrome") { await MainWindowChromeTests.RunAsync(); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "theme-quick-use") { using var quickUseLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await ThemeQuickUseTests.RunAsync(quickUseLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "content-contract") { ContentContractTests.Run(); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "studio-pipeline") { using var pipelineLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await StudioPipelineTests.RunAsync(pipelineLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-precision") { using var precisionLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await CreatorPrecisionTests.RunAsync(precisionLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-direct") { using var directLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger(); await CreatorDirectEditingTests.RunAsync(directLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "clock-components") { using var clockLogger = new LoggerConfiguration().CreateLogger(); await ClockComponentTests.RunAsync(clockLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "template-library") { using var templateLogger = new LoggerConfiguration().CreateLogger(); await TemplateLibraryTests.RunAsync(templateLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-usability") { using var usabilityLogger = new LoggerConfiguration().CreateLogger(); await CreatorUsabilityTests.RunAsync(usabilityLogger); return; }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-device")
        {
            using var previewLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await CreatorDevicePreviewTests.RunAsync(previewLogger); await MultiLayoutTests.RunAsync(); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") is "d69" or "d69-hardware")
        {
            using var d69Logger = new LoggerConfiguration().CreateLogger();
            if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "d69-hardware") await D69IntegrationTests.RunHardwareAsync(d69Logger);
            else await D69IntegrationTests.RunAsync(d69Logger);
            return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "device-page")
        {
            using var deviceLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await ManagedDevicePageTests.RunAsync(deviceLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "multi-device")
        {
            using var sessionLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await MultiDeviceSessionTests.RunAsync(sessionLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "client-experience")
        {
            using var experienceLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await ClientExperienceTests.RunAsync(experienceLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-productivity")
        {
            using var productivityLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await CreatorProductivityTests.RunAsync(productivityLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "studio")
        {
            using var studioLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await StudioApiTests.RunAsync(studioLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator-workbench")
        {
            using var workbenchLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await CreatorWorkbenchTests.RunAsync(workbenchLogger); return;
        }
        if (Environment.GetEnvironmentVariable("DESKDISPLAY_TEST_SUITE") == "creator")
        {
            using var creatorLogger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink()).CreateLogger();
            await CreatorTests.RunAsync(creatorLogger); await CreatorManagementTests.RunAsync(creatorLogger); await CreatorWorkflowTests.RunAsync(creatorLogger); return;
        }
        await UiRefactorTests.RunAsync();
        await MainWindowChromeTests.RunAsync();
        Check(Marshal.SizeOf<NativeMethods.MonitorInfo>() == 104, "MONITORINFOEXW layout");
        Check(Marshal.SizeOf<NativeMethods.DisplayDevice>() == 840, "DISPLAY_DEVICEW layout");
        Check(Marshal.SizeOf<NativeMethods.PathInfo>() == 72, "DISPLAYCONFIG_PATH_INFO layout");
        Check(Marshal.SizeOf<NativeMethods.ModeInfo>() == 64, "DISPLAYCONFIG_MODE_INFO layout");
        Check(Marshal.SizeOf<NativeMethods.SourceDeviceName>() == 84, "Source name layout");
        Check(Marshal.SizeOf<NativeMethods.TargetDeviceName>() == 420, "Target name layout");

        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using (var manager = new WindowsDisplayManager(logger))
        {
            var snapshot = await manager.GetDisplaysAsync();
            Check(snapshot.Count > 0, "Live native enumeration returns displays");
            Check(snapshot.All(display => display.Width > 0 && display.Height > 0 && !string.IsNullOrWhiteSpace(display.DeviceName)), "Live native dimensions and names");
            Check(snapshot.Any(display => display.IsPrimary), "Live primary display flag");
            foreach (var display in snapshot) Console.WriteLine($"LIVE: {display}");
        }

        using var fake = new FakeDisplayManager();
        using var viewModel = new DevicesViewModel(fake, new TargetDisplayService(new AppSettings(), new MemorySettingsService(), logger), logger);
        var fourDisplays = new[]
        {
            new DisplayInfo("DISPLAY9", "Main", "id-main", 0, 0, 2560, 1440, true),
            new DisplayInfo("DISPLAY3", "Left", "id-left", -1920, 0, 1920, 1080, false),
            new DisplayInfo("DISPLAY7", "Portrait", null, 2560, -600, 1080, 1920, false),
            new DisplayInfo("DISPLAY1", "Small", "id-small", 3640, 0, 800, 480, false)
        };
        fake.Next = _ => Task.FromResult<IReadOnlyList<DisplayInfo>>(fourDisplays);
        await viewModel.RefreshAsync();
        Check(viewModel.Displays.SequenceEqual(fourDisplays), "Four displays, negative coordinates and arbitrary dimensions preserved");
        Check(viewModel.Displays[2].DeviceId is null, "Missing device metadata accepted");

        var errorsBefore = sink.Errors;
        fake.Next = _ => throw new InvalidOperationException("Simulated hot-plug enumeration failure");
        await viewModel.RefreshAsync();
        Check(viewModel.Displays.SequenceEqual(fourDisplays) && viewModel.Status.Contains("刷新失败"), "Failed refresh retains last snapshot");
        Check(sink.Errors > errorsBefore, "Refresh failure logged");
        Check(viewModel.RefreshCommand.CanExecute(null), "Refresh remains available after failure");

        var pending = new TaskCompletionSource<IReadOnlyList<DisplayInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callsBefore = fake.Calls;
        fake.Next = _ => pending.Task;
        var refreshing = viewModel.RefreshAsync();
        Check(!viewModel.RefreshCommand.CanExecute(null), "Refresh button disabled during enumeration");
        fake.RaiseChanged();
        fake.RaiseChanged();
        fake.Next = _ => Task.FromResult<IReadOnlyList<DisplayInfo>>(fourDisplays.Take(1).ToArray());
        pending.SetResult(fourDisplays);
        await refreshing;
        Check(fake.Calls == callsBefore + 2 && viewModel.Displays.Count == 1, "Changes during enumeration coalesced into one follow-up refresh");

        fake.Next = _ => Task.FromResult<IReadOnlyList<DisplayInfo>>(Array.Empty<DisplayInfo>());
        fake.RaiseChanged();
        Check(viewModel.Displays.Count == 0, "Removing all displays clears the snapshot");

        var lateCompletion = new TaskCompletionSource<IReadOnlyList<DisplayInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.Next = _ => lateCompletion.Task;
        var finalRefresh = viewModel.RefreshAsync();
        viewModel.Dispose();
        lateCompletion.SetResult(fourDisplays);
        await finalRefresh;
        Check(viewModel.Displays.Count == 0 && !viewModel.RefreshCommand.CanExecute(null), "Late completion after disposal cannot change snapshot");
        callsBefore = fake.Calls;
        fake.RaiseChanged();
        Check(fake.Calls == callsBefore && fake.SubscriberCount == 0, "Display event subscription released");
        Console.WriteLine("All display tests passed.");
        await TargetDisplayTests.RunAsync(logger);
        await RenderWindowTests.RunAsync(logger);
        await SceneTests.RunAsync(logger);
        await ImageSceneTests.RunAsync(logger);
        await SensorTests.RunAsync(logger);
        await VideoTests.RunAsync(logger);
        await VideoFramePrototypeTests.RunAsync();
        await VideoExperienceTests.RunAsync();
        await VideoP0Tests.RunAsync();
        await VideoBufferOwnershipTests.RunAsync();
        await VideoPixelOptimizationTests.RunAsync();
        await VideoSmoothnessTests.RunAsync();
        await ClientVideoTests.RunAsync();
        await WorkspaceTests.RunAsync(logger);
        await DashboardTests.RunAsync(logger);
        await StockSceneTests.RunAsync();
        await RetroCameraClockTests.RunAsync();
        await LibraryTests.RunAsync(logger);
        await CreatorTests.RunAsync(logger);
        await CreatorManagementTests.RunAsync(logger);
        await CreatorWorkflowTests.RunAsync(logger);
        await ClientExperienceTests.RunAsync(logger);
        await CreatorOrderingTests.RunAsync(logger); await BackgroundAdaptationTests.RunAsync(logger); await ThemeQuickUseTests.RunAsync(logger); ContentContractTests.Run(); await StudioApiTests.RunAsync(logger);
        await UsbOutputTests.RunAsync(logger);
        await UsbLibraryTests.RunAsync(logger);
        await D69IntegrationTests.RunAsync(logger);
        await MultiDeviceSessionTests.RunAsync(logger);
        await ManagedDevicePageTests.RunAsync(logger);
        await CreatorDevicePreviewTests.RunAsync(logger); await MultiLayoutTests.RunAsync(); await CreatorUsabilityTests.RunAsync(logger); await CreatorPrecisionTests.RunAsync(logger); await CreatorDirectEditingTests.RunAsync(logger); await ClockComponentTests.RunAsync(logger); await TemplateLibraryTests.RunAsync(logger);await OutputExperienceTests.RunAsync(logger);await PlaylistRecoveryTests.RunAsync(logger);await UserWorkflowTests.RunAsync(logger);await ContentExperienceTests.RunAsync(logger);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine($"PASS: {description}");
    }

    private sealed class FakeDisplayManager : IDisplayManager
    {
        public event EventHandler? DisplaysChanged;
        public int SubscriberCount => DisplaysChanged?.GetInvocationList().Length ?? 0;
        public int Calls { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<DisplayInfo>>> Next { get; set; } = _ => Task.FromResult<IReadOnlyList<DisplayInfo>>(Array.Empty<DisplayInfo>());
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Next(cancellationToken);
        }
        public void RaiseChanged() => DisplaysChanged?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class CaptureSink : Serilog.Core.ILogEventSink
    {
        public int Errors { get; private set; }
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            if (logEvent.Level >= Serilog.Events.LogEventLevel.Error) Errors++;
            if (logEvent.Level >= Serilog.Events.LogEventLevel.Warning)
                Console.WriteLine($"LOG: {logEvent.RenderMessage()} {logEvent.Exception?.Message}");
        }
    }

    private sealed class MemorySettingsService : ISettingsService
    {
        public AppSettings Settings { get; private set; } = new();
        public Task<AppSettings> LoadAsync() => Task.FromResult(Settings);
        public Task<bool> SaveAsync(AppSettings settings)
        {
            Settings = settings;
            return Task.FromResult(true);
        }
    }
}
