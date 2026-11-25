using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class ManagedDevicePageTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-device-page-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "managed-devices.json"); var store = new DevicePreferenceStore(path);
            await store.SaveAsync([new("unplugged-saved", "已保存的另一台", 640, 160, 0)]);
            await new DevicePreferenceStore(path).SaveAsync([new("new-device", "新设备", 320, 80, 0)]);
            Check((await store.LoadAsync()).Count == 2, "Device sidecar merges other existing device entries");
            var invalid = Path.Combine(directory, "unknown.json"); var invalidBytes = "{\"Version\":99,\"Devices\":[]}";
            await File.WriteAllTextAsync(invalid, invalidBytes);
            try { await new DevicePreferenceStore(invalid).SaveAsync([]); throw new Exception("unknown settings overwritten"); }
            catch (InvalidDataException) { }
            Check(await File.ReadAllTextAsync(invalid) == invalidBytes, "Unknown device preferences remain byte-for-byte unchanged");

            var registry = new DeviceRegistry();
            DeviceDescriptor Device(string serial) => new(OutputDeviceKeys.D92(serial), "D92", DeviceTransport.UsbHid,
                new("usb:5548:1011", serial, IdentityConfidence.VerifiedStable), new("path-" + serial, "path-" + serial, 0), true);
            var a = Device("offline-A"); var b = Device("offline-B");
            registry.Replace(DeviceTransport.UsbHid, [a, b]);
            var source = new ThemeProject { Name = "离线测试主题" };
            var sourceBytes = System.Text.Json.JsonSerializer.Serialize(source);
            await using var sessions = new OutputSessionManager(registry, new InMemoryDeviceLeaseProvider(), (_, _) => ValueTask.FromResult<IOutputSessionDriver>(new Driver()));
            using var managed = new ManagedDevicesViewModel(registry, sessions, store, () => [source], () => [], _ => Task.CompletedTask, logger,
                new UsbOutputSettings { SerialNumber = "offline-A", Width = 1920, Height = 462, RotationDegrees = 90 });
            await managed.InitializeAsync();
            var usedIds = new List<Guid>(); managed.ThemeUseAccepted = id => { usedIds.Add(id); return Task.CompletedTask; };
            var ca = managed.Cards.Single(c => c.DeviceId == a.DeviceId); var cb = managed.Cards.Single(c => c.DeviceId == b.DeviceId);
            Check(ca.Width == "1920" && ca.Height == "462" && ca.Rotation == 90 && cb.Width == "0", "Legacy USB defaults import only to the matching serial without overwriting other devices");
            Check(!ca.ApplyCommand.CanExecute(null) && managed.SelectedTheme is null, "No implicit theme selection or output at page initialization");
            managed.SelectedTheme = managed.Themes.Single(); cb.Width = "640"; cb.Height = "160"; cb.Alias = "工作台 B";
            Check(!managed.ApplySelectedCommand.CanExecute(null), "Batch apply stays disabled until devices are selected");
            ca.Confirmed = true; cb.Confirmed = true;
            Check(ca.ApplyCommand.CanExecute(null) && cb.ApplyCommand.CanExecute(null), "Explicit theme and per-device confirmation enable apply");
            cb.Width = "800"; Check(!cb.Confirmed, "Changing output geometry clears confirmation"); cb.Width = "640"; cb.Confirmed = true;
            ca.Selected = cb.Selected = true;
            Check(managed.ApplySelectedCommand.CanExecute(null), "Selecting cards updates batch command availability immediately");
            ca.Width = "invalid"; ca.Confirmed = true;
            managed.ApplySelectedCommand.Execute(null); await managed.PendingOperation;
            await UntilAsync(() => sessions.Snapshot.Any(s => s.Snapshot.DeviceId == b.DeviceId && s.Snapshot.ActualState == OutputActualState.Streaming));
            managed.Synchronize();
            Check(!ca.IsActive && cb.IsActive && ca.Message.Contains("像素"), "One invalid device profile does not interrupt another device's batch application");
            Check(usedIds.SequenceEqual(new[] { source.Id }), "Usage records are emitted only for accepted output sessions, excluding failed device profiles");
            cb.StopCommand.Execute(null); await managed.PendingStops; managed.Synchronize();
            ca.Width = "1920"; ca.Confirmed = true; cb.Confirmed = true;
            managed.ApplySelectedCommand.Execute(null); await managed.PendingOperation;
            await UntilAsync(() => sessions.Snapshot.Count(s => s.Snapshot.ActualState == OutputActualState.Streaming) == 2);
            managed.Synchronize();
            Check(ca.OutputStatus == "正在输出" && cb.OutputStatus == "正在输出" && ca.AppliedTheme == source.Name, "Batch UI applies a real source through independent session manager entries");
            ca.StopCommand.Execute(null); await managed.PendingStops; managed.Synchronize();
            Check(!ca.IsActive && cb.IsActive, "Per-card stop leaves the other card streaming");
            Check(System.Text.Json.JsonSerializer.Serialize(source) == sourceBytes, "Device page never changes editable theme source");
            await managed.SaveAsync();
            var preferences = await store.LoadAsync();
            Check(preferences.Single(p => p.DeviceId == b.DeviceId).Alias == "工作台 B" && preferences.Any(p => p.DeviceId == "unplugged-saved"), "Saving names and dimensions preserves unrelated disconnected preferences");
            registry.Replace(DeviceTransport.UsbHid, [b]); managed.Synchronize();
            Check(!ca.Connected && !ca.Confirmed && cb.Connected, "Discovery removal clears only corresponding device confirmation");
            registry.Replace(DeviceTransport.UsbHid, [a, b]); managed.Synchronize();
            Check(ca.Connected && !ca.Confirmed && ca.Alias == "D92", "Replug retains name but requires fresh explicit confirmation");

            // Render actual WPF controls with clearly labelled offline test data, never as a real hardware claim.
            using var displayManager = new Displays();
            var settings = new MemorySettings();
            using var devices = new DevicesViewModel(displayManager, new DeskDisplay.Display.Services.TargetDisplayService(new(), settings, logger), logger);
            devices.AttachManaged(managed);
            // The remote desktop may clamp native windows to its work area. Test explicit logical surfaces,
            // without claiming a native 1320x900 window or changing the user's monitor/DPI settings.
            var view = new ManagedDevicePanel { DataContext = devices, Width = 1100, Height = 740 };
            var window = new Window { Content = view, Width = 1100, Height = 740, Background = new SolidColorBrush(Color.FromRgb(244, 247, 251)), ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
            window.Show(); await Task.Delay(100); window.UpdateLayout();
            Capture(view, "device-management-1100.png");
            window.Width = 1320; window.Height = 900; view.Width = 1320; view.Height = 900; await Task.Delay(100); window.UpdateLayout(); Capture(view, "device-management-1320.png");
            Check(view.ActualWidth == 1320 && view.ActualHeight == 900, "Managed device page lays out actual WPF controls on both explicit logical surfaces independently of desktop window limits");
            window.Close();
            using var main = new MainViewModel(devices, new ClockScene()); main.SelectedPage = AppPage.Devices;
            var shell = new MainWindow { DataContext = main, Width = 1320, Height = 900, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
            shell.Show(); await Task.Delay(100); shell.UpdateLayout();
            Check(Children<ManagedDevicePanel>(shell).Any(p => p.IsVisible && ReferenceEquals(p.DataContext, devices)),
                "Actual MainWindow device navigation binds visible cards to DevicesViewModel");
            Capture(shell, "device-management-shell-1320.png");
            Children<Button>(shell).Single(button => Equals(button.Content, "原有 D92 USB 输出")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50); shell.UpdateLayout();
            Check(Children<UsbOutputView>(shell).Any(p => p.IsVisible), "Original USB route remains accessible in the real main window");
            shell.Close();

            await managed.PrepareForCloseAsync(); await sessions.DisposeAsync();
            Check(sessions.Snapshot.All(s => s.Snapshot.DesiredState == OutputDesiredState.Stopped), "Page shutdown cancels active managed output and awaits operations");

            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var preparing = new OutputSessionManager(registry, new InMemoryDeviceLeaseProvider(), async (_, ct) =>
            {
                entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new Driver();
            });
            using var preparingPage = new ManagedDevicesViewModel(registry, preparing, store, () => [source], () => [], _ => Task.CompletedTask, logger);
            await preparingPage.InitializeAsync(); preparingPage.SelectedTheme = preparingPage.Themes.Single();
            var preparingCard = preparingPage.Cards.Single(c => c.DeviceId == a.DeviceId); preparingCard.Confirmed = true;
            preparingCard.ApplyCommand.Execute(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(4)); preparingPage.Synchronize();
            Check(preparingCard.StopCommand.CanExecute(null), "Per-device stop remains available while its asynchronous preparation is busy");
            preparingCard.StopCommand.Execute(null);
            await Task.WhenAll(preparingPage.PendingOperation, preparingPage.PendingStops).WaitAsync(TimeSpan.FromSeconds(4));
            Check(preparing.Snapshot.Single().Snapshot.ActualState == OutputActualState.Stopped, "Device page can cancel preparation and release its session without hanging");
            await preparingPage.PrepareForCloseAsync();

            var ambiguous = a with { DeviceId = "ambiguous-usb:offline-path", Identity = a.Identity with { Confidence = IdentityConfidence.Ambiguous } };
            var confirmation = new DeviceRegistry(); confirmation.Replace(DeviceTransport.UsbHid, [ambiguous]);
            var epoch = confirmation.Find(ambiguous.DeviceId)!.Endpoint.ConnectionEpoch;
            Check(!confirmation.ConfirmEndpoint(ambiguous.DeviceId, epoch + 1) && confirmation.ConfirmEndpoint(ambiguous.DeviceId, epoch), "Manual identity confirmation must match current observed endpoint epoch");
            confirmation.Replace(DeviceTransport.UsbHid, [ambiguous]);
            Check(confirmation.Find(ambiguous.DeviceId)!.Identity.Confidence == IdentityConfidence.UserConfirmed, "Unchanged endpoint snapshot preserves explicit confirmation");
            confirmation.Replace(DeviceTransport.UsbHid, []); confirmation.Replace(DeviceTransport.UsbHid, [ambiguous]);
            Check(confirmation.Find(ambiguous.DeviceId)!.Identity.Confidence == IdentityConfidence.Ambiguous, "Manual identity confirmation is revoked after disconnect and reconnect");
            var confirmed = ambiguous with { Identity = ambiguous.Identity with { Confidence = IdentityConfidence.UserConfirmed } };
            var candidates = new[] { new DeskDisplay.Display.Usb.D92DeviceInfo(confirmed.Endpoint.Address, "offline-A", 513), new DeskDisplay.Display.Usb.D92DeviceInfo("other-path", "offline-A", 513) };
            Check(DeskDisplay.Display.Usb.D92HidRenderTarget.SelectCandidates(candidates, "offline-A", confirmed).Single().Path == confirmed.Endpoint.Address,
                "Duplicate serial manual selection is restricted to the confirmed endpoint path");
            Check(DeskDisplay.Display.Usb.D92HidRenderTarget.SelectCandidates(candidates.Skip(1), "offline-A", confirmed).Length == 0,
                "Missing confirmed path never falls back to the other matching serial");
            confirmation.ConfirmEndpoint(ambiguous.DeviceId, confirmation.Find(ambiguous.DeviceId)!.Endpoint.ConnectionEpoch);
            confirmation.Replace(DeviceTransport.UsbHid, [ambiguous with { Identity = ambiguous.Identity with { SerialNumber = "changed-serial" } }]);
            Check(confirmation.Find(ambiguous.DeviceId)!.Identity.Confidence == IdentityConfidence.Ambiguous,
                "Identity change on same endpoint revokes prior manual confirmation");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream);
    }
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T item) yield return item;
            foreach (var nested in Children<T>(child)) yield return nested;
        }
    }
    private static async Task UntilAsync(Func<bool> ready) { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4)); while (!ready()) await Task.Delay(10, deadline.Token); }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private sealed class Driver : IOutputSessionDriver
    {
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken cancellationToken) { report(OutputActualState.Streaming); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Displays : IDisplayManager
    {
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]);
        public void Dispose() { }
    }
    private sealed class MemorySettings : ISettingsService
    {
        public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings());
        public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true);
    }
}
