using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Interop;
using DeskDisplay.Display.Rendering;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Views;
using Serilog;
using DeskDisplay.Display.DataProviders;

namespace DeskDisplay.DisplayTests;

internal static class RenderWindowTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var primary = new DisplayInfo("PRIMARY", "Main", "main-id", 0, 0, 1920, 1080, true);
        var target = new DisplayInfo("EXTERNAL", "Secondary", "target-id", -2345, -80, 2345, 678, false);
        var clock = new TestClock();
        var scene = new ClockScene();
        using var provider = new ClockSceneDataProvider(scene, logger, clock);
        provider.Start();
        var created = new List<FakeSurface>();
        using var service = new RenderWindowService(logger, scene, () =>
        {
            var surface = new FakeSurface();
            created.Add(surface);
            return surface;
        });
        service.ShowTest(primary);
        Check(!service.IsOpen && created.Count == 0, "Primary target cannot open a render surface");
        service.ShowTest(target);
        service.ShowTest(target);
        Check(service.IsOpen && created.Count == 1 && created[0].ShowCount == 1, "Repeated testing uses one render instance");
        Check(ReferenceEquals(created[0].Scene, scene) && scene is { PixelWidth: 2345, PixelHeight: 678 }, "Renderer receives the shared ClockScene instance and target canvas size");
        clock.Now = clock.Now.AddSeconds(5);
        await Task.Delay(1150);
        Check(((ClockScene?)created[0].Scene)?.CurrentTime == clock.Now, "One clock provider updates the shared scene");
        var moved = target with { X = 3000, Y = -400, Width = 1600, Height = 900 };
        service.SynchronizeTarget(moved);
        Check(created.Count == 1 && created[0].Target == moved && created[0].Scene?.PixelWidth == 1600, "Resolution and arrangement changes reuse and reposition the render surface");
        created[0].InvalidateTopology();
        Check(service.IsOpen && service.Status.Contains("正在确认"), "Topology invalidation suspends output pending revalidation");
        service.SynchronizeTarget(moved);
        Check(created[0].ShowCount == 3, "Validated target resumes the suspended instance");
        service.SynchronizeTarget(null);
        Check(!service.IsOpen && created[0].Disposed, "Disconnect disposes external output");
        var renderCount = created[0].RenderCount;
        clock.Now = clock.Now.AddSeconds(1);
        await Task.Delay(1100);
        Check(created[0].RenderCount == renderCount && provider.IsRunning && scene.CurrentTime == clock.Now,
            "Closing external output leaves the shared preview clock running without rebinding the closed surface");
        service.ShowTest(target);
        created[^1].CloseExternally();
        Check(!service.IsOpen && created[^1].Disposed, "Unexpected surface close resets lifecycle state");
        service.ShowTest(target);
        created[^1].RaiseFault();
        Check(!service.IsOpen && service.Status.Contains("错误"), "Render fault closes only render output and reports error");
        service.ShowTest(target);
        Check(service.IsOpen, "Render output can reopen after a fault");
        service.SynchronizeTarget(target with { IsPrimary = true });
        Check(!service.IsOpen, "Target becoming primary closes output");
        service.ShowTest(target);
        service.SynchronizeTarget(target with { DeviceId = "another-target" });
        Check(!service.IsOpen, "Changing selected target closes old output instead of moving it silently");

        using var manager = new FakeManager { Snapshot = [primary, target] };
        var targetService = new TargetDisplayService(new AppSettings(), new MemorySettings(), logger);
        using var vm = new DevicesViewModel(manager, targetService, logger, service);
        await vm.RefreshAsync();
        Check(!vm.TestDisplayCommand.CanExecute(null), "Test button disabled without saved target");
        vm.SelectedDisplay = target;
        await vm.SetSelectedTargetAsync();
        Check(vm.TestDisplayCommand.CanExecute(null), "Test button enabled for connected saved secondary");
        manager.Snapshot = [primary];
        await vm.TestDisplayAsync();
        Check(!service.IsOpen && !vm.TestDisplayCommand.CanExecute(null), "Disconnect before test click is detected before window creation");
        manager.Snapshot = [primary, target];
        await vm.RefreshAsync();
        await vm.TestDisplayAsync();
        Check(service.IsOpen, "View model opens a validated target");
        manager.Snapshot = [primary];
        await vm.RefreshAsync();
        Check(!service.IsOpen, "Refreshed missing target closes existing window");
        manager.Snapshot = [primary, target];
        await vm.RefreshAsync();
        var pending = new TaskCompletionSource<IReadOnlyList<DisplayInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.Pending = pending.Task;
        var testing = vm.TestDisplayAsync();
        vm.CloseRender();
        pending.SetResult([primary, target]);
        await testing;
        Check(!service.IsOpen, "Close during pending test prevents a late window opening");
        manager.Pending = null;
        await vm.TestDisplayAsync();
        await vm.PrepareForCloseAsync();
        Check(!service.IsOpen, "Application close preparation disposes external output");

        await TestNativeWindowAsync(logger);
        Console.WriteLine("All render-window tests passed.");
    }

    private static async Task TestNativeWindowAsync(ILogger logger)
    {
        using var manager = new WindowsDisplayManager(logger);
        var display = (await manager.GetDisplaysAsync()).First();
        RejectBounds(() => RenderWindowNative.ResolveActiveBounds(display with { IsPrimary = true }), "Native validator rejects a primary target");
        RejectBounds(() => RenderWindowNative.ResolveActiveBounds(display with { IsPrimary = false, DeviceName = "missing-display" }), "Native validator rejects a disconnected target");
        // A single-monitor test machine cannot act as a real secondary. Bypass only device validation
        // for this test, so HWND styles, positioning, client bounds, DPI and rendering remain real.
        var pretendSecondary = display with { IsPrimary = false };
        var main = new Window { Title = "DeskDisplay main-position verification", Width = 500, Height = 300, Left = 60, Top = 80, ShowActivated = false, ShowInTaskbar = false };
        using var output = new RenderWindow(logger, item => new NativeMethods.Rect
        {
            Left = item.X, Top = item.Y, Right = item.X + item.Width, Bottom = item.Y + item.Height
        });
        try
        {
            main.Show();
            var mainHandle = new WindowInteropHelper(main).Handle;
            Check(RenderWindowNative.GetWindowRect(mainHandle, out var mainBefore), "Read main window position before output");
            var sharedScene = new ClockScene();
            sharedScene.SetCanvasSize(display.Width, display.Height);
            sharedScene.UpdateTime(new DateTimeOffset(2026, 9, 14, 12, 34, 56, TimeSpan.Zero));
            var previewView = new SceneView { Scene = sharedScene, DataContext = sharedScene };
            main.Content = new PreviewHost { AspectRatio = sharedScene.AspectRatio, Child = previewView };
            output.Render(sharedScene);
            output.ShowOnDisplay(pretendSecondary);
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => output.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            var hwnd = new WindowInteropHelper(output).Handle;
            Check(RenderWindowNative.GetWindowRect(hwnd, out var actual) && actual.Left == display.X && actual.Top == display.Y
                && actual.Right - actual.Left == display.Width && actual.Bottom - actual.Top == display.Height, "Live render HWND exactly covers physical monitor bounds");
            Check(RenderWindowNative.GetClientRect(hwnd, out var client) && client.Right == display.Width && client.Bottom == display.Height,
                "Live client area has no title-bar or frame inset");
            Check(RenderWindowNative.AreDpiAwarenessContextsEqual(RenderWindowNative.GetWindowDpiAwarenessContext(hwnd), new nint(-4)), "Live RenderWindow is Per-Monitor V2");
            Check(output.WindowStyle == WindowStyle.None && output.ResizeMode == ResizeMode.NoResize && !output.ShowInTaskbar
                && output.Topmost && output.WindowState == WindowState.Normal, "Render window uses borderless non-resizable topmost fullscreen styles");
            Check(RenderWindowNative.GetWindowRect(mainHandle, out var mainAfter) && mainBefore.Equals(mainAfter), "Main window position and dimensions remain unchanged");

            var renderer = (SceneView)output.Content;
            Check(ReferenceEquals(previewView.Scene, renderer.Scene) && ReferenceEquals(previewView.DataContext, renderer.DataContext),
                "Live preview and RenderWindow have the same Scene and ViewModel references");
            sharedScene.UpdateTime(sharedScene.CurrentTime.AddSeconds(1));
            Check(System.Windows.Automation.AutomationProperties.GetName(previewView) == System.Windows.Automation.AutomationProperties.GetName(renderer),
                "Both live SceneViews observe identical updated time/date/weekday data");
            var dpi = VisualTreeHelper.GetDpi(renderer);
            var image = new RenderTargetBitmap(display.Width, display.Height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            image.Render(renderer);
            var pixels = new byte[display.Width * display.Height * 4];
            image.CopyPixels(pixels, display.Width * 4, 0);
            bool Accent(int x, int y)
            {
                var offset = (y * display.Width + x) * 4;
                return pixels[offset] == 0xEF && pixels[offset + 1] == 0xC7 && pixels[offset + 2] == 0x74;
            }
            Check(Accent(0, display.Height / 2) && Accent(1, display.Height / 2) && !Accent(2, display.Height / 2)
                && Accent(display.Width - 1, display.Height / 2) && Accent(display.Width - 2, display.Height / 2)
                && Accent(display.Width / 2, 0) && Accent(display.Width / 2, 1) && !Accent(display.Width / 2, 2)
                && Accent(display.Width / 2, display.Height - 1) && Accent(display.Width / 2, display.Height - 2), "All four edges render an exact 2-physical-pixel border at live DPI");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(image));
            var imagePath = Path.Combine(AppContext.BaseDirectory, "clock-scene-render.png");
            using (var file = File.Create(imagePath)) png.Save(file);
            Console.WriteLine("RENDER IMAGE: " + imagePath);

            output.ShowOnDisplay(pretendSecondary with { X = -900, Y = -700, Width = 777, Height = 555 });
            Check(RenderWindowNative.GetWindowRect(hwnd, out actual) && actual.Left == -900 && actual.Top == -700
                && actual.Right - actual.Left == 777 && actual.Bottom - actual.Top == 555, "Native placement preserves signed coordinates and arbitrary dimensions");
            output.Close();
            Check(main.IsVisible, "Closing RenderWindow leaves main window alive");
            sharedScene.UpdateTime(sharedScene.CurrentTime.AddSeconds(1));
            Check(renderer.Scene is null && System.Windows.Automation.AutomationProperties.GetName(previewView).Contains("12:34:58"),
                "Closing output detaches its scene while preview continues receiving clock updates");
        }
        finally { output.Dispose(); main.Close(); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine("PASS: " + description);
    }

    private static void RejectBounds(Action action, string description)
    {
        try { action(); }
        catch (InvalidOperationException) { Check(true, description); return; }
        throw new InvalidOperationException("Expected rejection: " + description);
    }

    private sealed class FakeSurface : IRenderSurface
    {
        public event EventHandler? Closed;
        public event Action<Exception>? Faulted;
        public event EventHandler? TopologyInvalidated;
        public DisplayInfo? Target { get; private set; }
        public Scene? Scene { get; private set; }
        public int ShowCount { get; private set; }
        public int RenderCount { get; private set; }
        public bool Disposed { get; private set; }
        public void ShowOnDisplay(DisplayInfo target) { Target = target; ShowCount++; }
        public void Render(Scene scene) { Scene = scene; RenderCount++; }
        public void CloseExternally() => Closed?.Invoke(this, EventArgs.Empty);
        public void RaiseFault() => Faulted?.Invoke(new InvalidOperationException("Simulated render failure"));
        public void InvalidateTopology() => TopologyInvalidated?.Invoke(this, EventArgs.Empty);
        public void Dispose() => Disposed = true;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeManager : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Snapshot { get; set; } = Array.Empty<DisplayInfo>();
        public Task<IReadOnlyList<DisplayInfo>>? Pending { get; set; }
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Pending ?? Task.FromResult(Snapshot);
        public void Dispose() { }
    }

    private sealed class MemorySettings : ISettingsService
    {
        public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings());
        public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true);
    }
}
