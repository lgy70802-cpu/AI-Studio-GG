using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class UsbOutputTests
{
    public static async Task RunAsync(ILogger logger)
    {
        foreach (var length in new[] { 65, 513, 1025 })
        {
            var wake = D92FrameEncoder.WakeReport(length, 0);
            var heartbeat = D92FrameEncoder.HeartbeatReport(length, 0);
            Check(wake.Length == length && wake.AsSpan(0, 9).SequenceEqual(Convert.FromHexString("004352540000444953"))
                && wake.Skip(9).All(b => b == 0), $"Vendor wake report and padding match at {length} bytes");
            Check(heartbeat.Length == length && heartbeat.AsSpan(0, 13).SequenceEqual(Convert.FromHexString("004352540000434F4E4E454354"))
                && heartbeat.Skip(13).All(b => b == 0), $"Vendor heartbeat report and padding match at {length} bytes");
        }
        using (var stop = new CancellationTokenSource())
        {
            var frameComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var writeToken = CancellationToken.None;
            var sending = D92HidRenderTarget.SendWholeFrameAsync(ct => { writeToken = ct; return frameComplete.Task; }, stop.Token);
            stop.Cancel();
            Check(!writeToken.IsCancellationRequested && !sending.IsCompleted, "Normal USB stop waits for the in-flight frame instead of truncating it");
            frameComplete.SetResult(true);
            await sending;
            var started = false;
            try { await D92HidRenderTarget.SendWholeFrameAsync(_ => { started = true; return Task.CompletedTask; }, stop.Token); }
            catch (OperationCanceledException) { }
            Check(!started, "USB stop prevents the next frame from starting");
        }
        var settings = new Settings();
        using var manager = new Manager();
        using var devices = new DevicesViewModel(manager, new TargetDisplayService(settings.Value, settings, logger), logger);
        using var main = new MainViewModel(devices, new ClockScene());
        var target = new Target();
        IReadOnlyList<D92DeviceInfo> found = [new("path-b", "other-screen", 1025)];
        using var usb = new UsbOutputViewModel(main, settings, settings.Value, logger, target, () => found);
        main.SetUsb(usb);
        await usb.RefreshAsync();
        Check(usb.SelectedSerial is null, "USB enumeration never automatically chooses the first screen");
        await usb.StartAsync();
        Check(target.Calls == 0 && usb.Status.Contains("选择"), "USB requires explicit device selection");
        usb.SelectedSerial = "saved-screen"; usb.Width = "8192"; usb.Height = "8192";
        await usb.StartAsync();
        Check(target.Calls == 0 && settings.Writes == 0, "USB rejects oversized canvas before persistence or hardware access");
        usb.Width = "800"; usb.Height = "240"; usb.Rotation = 90;
        settings.Success = false;
        await usb.StartAsync();
        Check(!usb.IsRunning && target.Calls == 0, "USB save failure does not start output");
        settings.Success = true;
        await usb.RefreshAsync();
        Check(usb.SelectedSerial == "saved-screen" && usb.Devices.Contains("saved-screen"), "Disconnected saved serial survives refresh alongside a different device");
        usb.ReleaseManagedOutputAsync=_=>Task.FromException(new InvalidOperationException("handoff blocked"));
        await usb.StartAsync();
        Check(target.Calls==0 && !usb.IsRunning && usb.Status.Contains("handoff blocked"),"Rejected managed-to-wallpaper handoff never opens USB and reports the concrete reason");
        var handedOff=false;
        usb.ReleaseManagedOutputAsync=serial=>{handedOff=serial=="saved-screen" && target.Calls==0;return Task.CompletedTask;};
        Check(main.Output.HasRetry,"A failed USB handoff exposes an explicit retry action");
        await main.Output.RetryAsync();
        await target.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(usb.IsRunning && ReferenceEquals(usb.Scene, main.Scene) && main.Scene.PixelWidth == 800 && main.Scene.PixelHeight == 240,
            "USB and preview share the same scene and arbitrary canvas");
        Check(handedOff,"USB waits for matching managed output handoff before opening the wallpaper transport");
        var jpeg = BitmapDecoder.Create(new MemoryStream(target.Jpeg!), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Check(jpeg.PixelWidth == 240 && jpeg.PixelHeight == 800 && target.Serial == "saved-screen",
            "USB rotation affects JPEG only and uses selected serial");
        // Captured from d92-cold-start-test.jpg, confirmed on the physical device.
        // A JPEG can decode correctly in Windows while failing on this receiver.
        Check(QuantizationTables(target.Jpeg!).SequenceEqual(new[]
        {
            "000302020302020303030304030304050805050404050A070706080C0A0C0C0B0A0B0B0D0E12100D0E110E0B0B1016101113141515150C0F171816141812141514",
            "0103040405040509050509140D0B0D1414141414141414141414141414141414141414141414141414141414141414141414141414141414141414141414141414"
        }), "Default USB JPEG preserves both device-verified quantization tables");
        usb.Width = "99";
        main.UpdatePreview();
        Check(usb.Width == "800" && main.Scene.PixelWidth == 800, "Running USB locks configuration and survives Windows preview resizing");
        var next = new PerformanceScene();
        main.PreviewScene(next);
        Check(!ReferenceEquals(usb.Scene,next) && ReferenceEquals(main.HomeScene,usb.Scene),"Local browsing preserves the running USB scene and the home output preview");
        await usb.ApplyCurrentAsync();
        Check(ReferenceEquals(usb.Scene, next) && next.PixelWidth == 800, "Explicit apply switches USB to the selected scene with configured canvas");
        var unsupported=new UnsupportedScene();main.PreviewScene(unsupported);await usb.ApplyCurrentAsync();
        Check(usb.IsRunning && ReferenceEquals(usb.Scene,next) && usb.Status.Contains("不支持"),"Unsupported local preview cannot stop or replace a valid running USB scene");
        main.PreviewScene(next);
        Check(!await usb.StopOwnedSerialAsync("other-screen") && usb.IsRunning,"Serial-specific switching never stops output owned for a different device");
        Check(await usb.StopOwnedSerialAsync("saved-screen"),"Serial-specific switching releases only the matching legacy USB run");
        Check(!usb.IsRunning && target.Active == 0 && usb.CanEdit && next.PixelWidth == 1600,
            "USB stop awaits transport release and restores Windows preview canvas");
        Check(settings.Value.UsbOutput is { Width: 800, Height: 240, RotationDegrees: 90, SerialNumber: "saved-screen" }
            && settings.Value.LastScene == "保留", "USB settings preserve unrelated application preferences");

        var saveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        settings.Gate = saveGate;
        var starting = usb.StartAsync();
        var closing = usb.PrepareForCloseAsync();
        saveGate.SetResult(true);
        await Task.WhenAll(starting, closing).WaitAsync(TimeSpan.FromSeconds(5));
        Check(target.Calls == 1 && !usb.IsRunning && !usb.StartCommand.CanExecute(null),
            "Closing during settings write prevents delayed USB start");

        var view = new UsbOutputView { DataContext = usb, Width = 1000, Height = 650 };
        view.Measure(new Size(1000, 650)); view.Arrange(new Rect(0, 0, 1000, 650)); view.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1000, 650, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "usb-control-preview.png")); png.Save(file);
        Console.WriteLine("All USB control tests passed.");
    }
    private static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
    private static IEnumerable<string> QuantizationTables(byte[] jpeg)
    {
        for (var offset = 2; offset + 4 <= jpeg.Length && jpeg[offset] == 0xff;)
        {
            var marker = jpeg[offset + 1];
            if (marker == 0xda) yield break;
            var length = (jpeg[offset + 2] << 8) | jpeg[offset + 3];
            if (length < 2 || offset + 2 + length > jpeg.Length) throw new InvalidDataException("Truncated JPEG marker");
            if (marker == 0xdb) yield return Convert.ToHexString(jpeg, offset + 4, length - 2);
            offset += length + 2;
        }
    }
    private sealed class Target : ID92RenderTarget
    {
        public event Action<D92OutputState>? StateChanged;
        public int Calls, Active;
        public string? Serial;
        public byte[]? Jpeg;
        public TaskCompletionSource<bool> Rendered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RunAsync(string serialNumber, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken token)
        {
            Calls++; Active++; Serial = serialNumber;
            try
            {
                Jpeg = await render(token);
                StateChanged?.Invoke(new("Streaming", 1, 1));
                Rendered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
            }
            finally { Active--; }
        }
    }
    private sealed class Settings : ISettingsService
    {
        public AppSettings Value = new() { LastScene = "保留" };
        public bool Success = true;
        public int Writes;
        public TaskCompletionSource<bool>? Gate;
        public Task<AppSettings> LoadAsync() => Task.FromResult(Value);
        public Task<bool> SaveAsync(AppSettings value) { Writes++; Value = value; return Gate?.Task ?? Task.FromResult(Success); }
    }
    private sealed class UnsupportedScene : Scene { }
    private sealed class Manager : IDisplayManager
    {
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]);
        public void Dispose() { }
    }
}
