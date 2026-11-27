using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;
using System.IO;

namespace DeskDisplay.DisplayTests;

internal static class TargetDisplayTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-target-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsService(logger, path);
            var primary = new DisplayInfo(@"\\.\DISPLAY1", "Main", "main-id", 0, 0, 2560, 1440, true);
            var d92 = new DisplayInfo(@"\\.\DISPLAY2", "D92", "d92-stable-id", -1234, 100, 1234, 567, false);
            var other = new DisplayInfo(@"\\.\DISPLAY3", "Other", "other-id", 2560, -200, 1080, 1920, false);
            var target = new TargetDisplayService(await store.LoadAsync(), store, logger);
            target.UpdateDisplays([primary, d92, other]);
            Check(target.GetTargetDisplay() is null, "No automatic selection on first launch");
            Rejects(() => target.SetTargetDisplay(primary), "Primary display rejected");
            Rejects(() => target.SetTargetDisplay(d92 with { DeviceId = "missing" }), "Stale selection rejected");
            target.SetTargetDisplay(d92);
            Check(await target.SaveAsync(), "D92 selection saved to JSON");
            var stored = await store.LoadAsync();
            Check(stored.TargetDisplay is { DeviceId: "d92-stable-id", FriendlyName: "D92", LastWidth: 1234, LastHeight: 567 }, "All required identity and resolution fields persisted");

            var moved = d92 with { DeviceName = @"\\.\DISPLAY7", X = 4000, Y = -500, Width = 987, Height = 654 };
            var restarted = new TargetDisplayService(stored, store, logger);
            restarted.UpdateDisplays([other, moved, primary]);
            Check(restarted.GetTargetDisplay() == moved, "Restart restores by DeviceId despite ordering, name, bounds and resolution changes");
            Check(await restarted.SaveAsync() && (await store.LoadAsync()).TargetDisplay?.LastWidth == 987, "Last observed resolution updated on disk");
            var latest = await store.LoadAsync();
            restarted.UpdateDisplays([primary, other with { DeviceName = moved.DeviceName }]);
            Check(restarted.GetTargetDisplay() is null && restarted.SavedTarget?.FriendlyName == "D92" && !restarted.NeedsSave,
                "Disconnected D92 keeps saved identity and does not choose a reused display ordinal");
            restarted.UpdateDisplays([primary, moved]);
            Check(restarted.GetTargetDisplay() == moved, "Reconnect restores saved target");
            restarted.UpdateDisplays([moved with { IsPrimary = true }, primary with { IsPrimary = false }, other]);
            Check(restarted.GetTargetDisplay() is null, "Target becoming primary does not select another secondary");

            var fallback = new TargetDisplayService(latest, store, logger);
            fallback.UpdateDisplays([primary, moved with { DeviceId = null }]);
            Check(fallback.GetTargetDisplay() is not null && fallback.SavedTarget?.DeviceId == d92.DeviceId,
                "Corroborated DeviceName fallback preserves known ID when metadata is missing");
            fallback = new TargetDisplayService(latest, store, logger);
            var renamed = moved with { DeviceId = "new-port-id", DeviceName = @"\\.\DISPLAY9" };
            fallback.UpdateDisplays([primary, renamed, other]);
            Check(fallback.GetTargetDisplay() == renamed, "Unique friendly name fallback after ID and DeviceName change");
            fallback = new TargetDisplayService(latest, store, logger);
            fallback.UpdateDisplays([primary, renamed, renamed with { DeviceId = "duplicate", DeviceName = moved.DeviceName }]);
            Check(fallback.GetTargetDisplay() is null, "Ambiguous identical friendly names do not select a device");
            fallback.UpdateDisplays([primary, other with { Width = moved.Width, Height = moved.Height }]);
            Check(fallback.GetTargetDisplay() is null, "Resolution alone is never an identity");
            var genericSettings = new AppSettings { TargetDisplay = new TargetDisplaySettings
            {
                DeviceId = "old-generic-id", DeviceName = moved.DeviceName, FriendlyName = "Generic PnP Monitor"
            } };
            fallback = new TargetDisplayService(genericSettings, store, logger);
            fallback.UpdateDisplays([primary, moved with { DeviceId = "different-id", FriendlyName = "Generic PnP Monitor" }]);
            Check(fallback.GetTargetDisplay() is null, "Generic friendly name cannot override a conflicting identity");

            var legacy = new TargetDisplayService(new AppSettings { TargetDisplayId = d92.DeviceId }, store, logger);
            legacy.UpdateDisplays([primary, moved]);
            Check(legacy.GetTargetDisplay() == moved && await legacy.SaveAsync(), "Legacy target ID migrated");
            Check(!(await File.ReadAllTextAsync(path)).Contains("TargetDisplayId"), "New JSON omits legacy field");

            var failureStore = new ControlledSettingsService { Result = false };
            var rollback = new TargetDisplayService(latest, failureStore, logger);
            rollback.UpdateDisplays([primary, moved, other]);
            rollback.SetTargetDisplay(other);
            Check(!await rollback.SaveAsync() && rollback.GetTargetDisplay() == moved, "Failed selection write restores persisted target");
            rollback.ClearTargetDisplay();
            Check(!await rollback.SaveAsync() && rollback.GetTargetDisplay() == moved, "Failed clear write restores persisted target");
            failureStore.ThrowOnSave = true;
            rollback.ClearTargetDisplay();
            Check(!await rollback.SaveAsync() && rollback.SavedTarget is not null, "Thrown persistence exception is contained");

            using var manager = new FakeDisplayManager { Displays = [primary, d92, other] };
            var uiStore = new JsonSettingsService(logger, Path.Combine(directory, "ui-settings.json"));
            var uiTarget = new TargetDisplayService(await uiStore.LoadAsync(), uiStore, logger);
            using var vm = new DevicesViewModel(manager, uiTarget, logger);
            await vm.RefreshAsync();
            vm.SelectedDisplay = primary;
            Check(!vm.SetTargetCommand.CanExecute(null), "Set button disabled for primary display");
            vm.SelectedDisplay = d92;
            Check(vm.SetTargetCommand.CanExecute(null), "Set button enabled for non-primary display");
            await vm.SetSelectedTargetAsync();
            Check(vm.TargetDisplay == d92 && vm.TargetConnectionStatus == "状态：已连接" && (await uiStore.LoadAsync()).TargetDisplay?.DeviceId == d92.DeviceId,
                "Selection command persists and updates connected summary");
            var afterRestart = new TargetDisplayService(await uiStore.LoadAsync(), uiStore, logger);
            using (var reopened = new DevicesViewModel(manager, afterRestart, logger))
            {
                await reopened.RefreshAsync();
                Check(reopened.TargetDisplay == d92, "New view model and service restore selection from disk");
            }
            manager.Displays = [primary, other];
            await vm.RefreshAsync();
            Check(vm.TargetDisplay is null && vm.TargetName == "D92" && vm.TargetNotice == "目标副屏未连接" && vm.TargetResolution.Contains("1234 × 567"),
                "Disconnected summary retains saved name and last resolution");
            await vm.ClearTargetAsync();
            Check(!vm.HasSavedTarget && (await uiStore.LoadAsync()).TargetDisplay is null, "Cancel target persists even while disconnected");
            manager.Displays = [primary, d92];
            await vm.RefreshAsync();
            Check(vm.TargetDisplay is null, "Cleared target does not return after reconnect");
            vm.SelectedDisplay = d92;
            manager.Displays = [primary];
            await vm.SetSelectedTargetAsync();
            Check(vm.TargetDisplay is null && !vm.HasSavedTarget, "Unplug between selection and button press is revalidated");

            manager.Displays = [primary, d92];
            await vm.RefreshAsync();
            vm.SelectedDisplay = d92;
            await vm.SetSelectedTargetAsync();
            manager.Fail = true;
            await vm.RefreshAsync();
            Check(vm.HasSavedTarget && vm.TargetDisplay is null && !vm.SetTargetCommand.CanExecute(null), "Enumeration failure preserves preference but invalidates live target");

            var slowStore = new ControlledSettingsService { PendingWrite = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            using var slowManager = new FakeDisplayManager { Displays = [primary, d92] };
            using var closingVm = new DevicesViewModel(slowManager, new TargetDisplayService(new AppSettings(), slowStore, logger), logger);
            await closingVm.RefreshAsync();
            closingVm.SelectedDisplay = d92;
            var enumeration = new TaskCompletionSource<IReadOnlyList<DisplayInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
            slowManager.PendingEnumeration = enumeration.Task;
            var change = closingVm.SetSelectedTargetAsync();
            var closing = closingVm.PrepareForCloseAsync();
            Check(!closing.IsCompleted, "Close waits for accepted selection revalidation");
            enumeration.SetResult([primary, d92]);
            await slowStore.SaveStarted.Task;
            Check(!closing.IsCompleted && !closingVm.ClearTargetCommand.CanExecute(null), "Close waits for pending write and blocks new commands");
            slowStore.PendingWrite.SetResult(true);
            await change;
            await closing;
            Check(slowStore.Written?.TargetDisplay?.DeviceId == d92.DeviceId, "Immediate close completes target persistence");
            Console.WriteLine("All target-display tests passed.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Rejects(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { Check(true, name); return; }
        throw new InvalidOperationException("Expected rejection: " + name);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class FakeDisplayManager : IDisplayManager
    {
        public IReadOnlyList<DisplayInfo> Displays { get; set; } = Array.Empty<DisplayInfo>();
        public bool Fail { get; set; }
        public Task<IReadOnlyList<DisplayInfo>>? PendingEnumeration { get; set; }
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("Simulated unavailable display configuration") : PendingEnumeration ?? Task.FromResult(Displays);
        public void Dispose() { }
    }

    private sealed class ControlledSettingsService : ISettingsService
    {
        public bool Result { get; set; } = true;
        public bool ThrowOnSave { get; set; }
        public TaskCompletionSource<bool>? PendingWrite { get; set; }
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AppSettings? Written { get; private set; }
        public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings());
        public Task<bool> SaveAsync(AppSettings settings)
        {
            Written = settings;
            SaveStarted.TrySetResult();
            return ThrowOnSave ? throw new IOException("Simulated write exception") : PendingWrite?.Task ?? Task.FromResult(Result);
        }
    }
}
