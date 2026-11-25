using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class CreatorDevicePreviewTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-preview-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var registry = new DeviceRegistry();
            DeviceDescriptor Device(string serial) => new(OutputDeviceKeys.D92(serial), "D92", DeviceTransport.UsbHid,
                new("test", serial, IdentityConfidence.VerifiedStable), new("offline-" + serial, "offline-" + serial, 0), true);
            var a = Device("preview-A"); var b = Device("preview-B"); registry.Replace(DeviceTransport.UsbHid, [a, b]);
            var leases = new InMemoryDeviceLeaseProvider(); var contexts = new Dictionary<string, OutputSessionContext>();
            await using var sessions = new OutputSessionManager(registry, leases, (context, _) =>
            { contexts[context.Device.DeviceId] = context; return ValueTask.FromResult<IOutputSessionDriver>(new Driver()); });
            using var display = new Displays();
            var factory = new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher, display, logger);
            var source = new ThemeProject { Name = "离线设备预览主题" };
            source.Landscape.Layers.Add(new() { Kind = ThemeElementKind.DigitalClock, Width = 600, Height = 160, FontSize = 80, ShowSeconds = true });
            source.Portrait.Layers.Add(new() { Kind = ThemeElementKind.Text, Text = "独立竖屏布局", Width = 360, Height = 150 });
            var original = JsonSerializer.Serialize(source);
            using var devices = new ManagedDevicesViewModel(registry, sessions, new DevicePreferenceStore(Path.Combine(directory, "prefs.json")), () => [source], () => [], _ => Task.CompletedTask, logger);
            devices.PreviewFactory = factory.CreatePreviewAsync; await devices.InitializeAsync();
            ThemeScene? homeOutput=null;var homeUpdates=0;
            devices.HomeOutputChanged=scene=>{homeOutput=scene;homeUpdates++;};
            var ca = devices.Cards.Single(c => c.DeviceId == a.DeviceId); ca.Width = "1920"; ca.Height = "462"; ca.Rotation = 90;
            var cb = devices.Cards.Single(c => c.DeviceId == b.DeviceId); cb.Width = "462"; cb.Height = "1920";
            await using var preview = new CreatorDevicePreviewViewModel(source, devices, logger);
            await preview.PendingPreview;
            Check(preview.PreviewScene is { PixelWidth: 1920, PixelHeight: 462 } && preview.Match.Contains("留边"), "Creator device preview uses actual target pixels and exact aspect comparison");
            Check(sessions.Snapshot.Length == 0, "Creating preview starts no output session");
            using (var free = leases.TryAcquire(a.DeviceId)) Check(free is not null, "Preview does not acquire or block a device lease");
            preview.Target = cb; await preview.PendingPreview;
            Check(preview.PreviewScene is { PixelWidth: 462, PixelHeight: 1920 } && preview.PreviewScene.Project.ActiveLayout.Layers.Single().Text == "独立竖屏布局", "Changing target selects independent portrait layout");
            preview.Target = ca; preview.Target = cb; preview.Target = ca; await preview.PendingPreview;
            Check(preview.PreviewScene is { PixelWidth: 1920 }, "Rapid target switching discards cancelled preview results");
            Check(JsonSerializer.Serialize(source) == original, "Device switches preserve source bytes including orientation and layout coordinates");
            preview.Adaptation = preview.Adaptations.Single(c => c.Mode == LayoutAdaptation.Cover); await preview.PendingPreview;
            Check(preview.PreviewScene!.ResolvedLayout!.Adaptation == LayoutAdaptation.Cover, "Explicit cover choice drives real resolved scene");
            var tick = preview.PreviewScene.CurrentTime; await Task.Delay(1100);
            Check(preview.PreviewScene.CurrentTime > tick, "Read-only device preview clock advances from real system time");
            Check(!preview.ApplyTargetCommand.CanExecute(null), "Preview alone never confirms the physical output target");
            ca.Confirmed = true;
            Check(preview.ApplyTargetCommand.CanExecute(null), "Confirmed connected target enables explicit apply");
            var resolved = preview.PreviewScene.ResolvedLayout;
            preview.ApplyTargetCommand.Execute(null); await preview.PendingApply;
            Check(homeOutput is {PixelWidth:1920,PixelHeight:462} && !ReferenceEquals(homeOutput,preview.PreviewScene),"Successful theme output publishes an independently owned live home preview with actual device geometry");
            var homeTick=homeOutput!.CurrentTime;await Task.Delay(1100);
            Check(homeOutput.CurrentTime>homeTick,"Home output preview clock continues ticking outside the apply dialog");
            Check(JsonSerializer.Serialize(contexts[a.DeviceId].Layout) == JsonSerializer.Serialize(resolved),
                "Application uses the same surface geometry and adaptation as device preview");
            await sessions.StopAsync(a.DeviceId); devices.Synchronize();
            ca.Selected = cb.Selected = true; ca.Confirmed = cb.Confirmed = true;
            preview.ApplySelectedCommand.Execute(null); await preview.PendingApply;
            Check(contexts[b.DeviceId].Layout.SurfaceSize == new PixelSize(462, 1920), "Selected-device apply resolves each target separately");
            Check(devices.SelectedTheme is null && JsonSerializer.Serialize(source) == original, "Creator application does not change device page theme selection or source project");

            var savedThemeSession=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId;
            var otherSavedThemeSession=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==b.DeviceId).Snapshot.SessionId;
            Check(preview.SaveSourceAsync is null && preview.ApplyTargetCommand.CanExecute(null) && preview.ApplyLabel=="切换到此设备", "Saved-theme entry enables direct owned-output switching without a save callback");
            preview.ApplyTargetCommand.Execute(null);await preview.PendingApply;
            Check(sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId!=savedThemeSession && sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==b.DeviceId).Snapshot.SessionId==otherSavedThemeSession && JsonSerializer.Serialize(source)==original,"Saved-theme switching replaces only the chosen session and preserves source bytes");
            var aBefore=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId;
            var bBefore=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==b.DeviceId).Snapshot.SessionId;
            preview.SaveSourceAsync=()=>Task.FromException(new IOException("save test failure"));
            Check(preview.ApplyTargetCommand.CanExecute(null),"Editor save-and-apply allows switching a confirmed owned session");
            preview.ApplyTargetCommand.Execute(null); await preview.PendingApply;
            Check(sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId==aBefore && preview.Status.Contains("保存失败"),"Failed save leaves the current device output running and reports no apply");
            var sourceStore=new ThemeProjectStore(Path.Combine(directory,"save-before-apply"));
            var savedSignal=new TaskCompletionSource();var continueApply=new TaskCompletionSource();
            preview.SaveSourceAsync=async()=>{await sourceStore.SaveAsync(source);savedSignal.SetResult();await continueApply.Task;};
            preview.ApplyTargetCommand.Execute(null);await savedSignal.Task;
            Check(!preview.CanConfigure && sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId==aBefore,"Old output remains running while the real source save completes");
            continueApply.SetResult();await preview.PendingApply;
            Check((await sourceStore.ListAsync()).Single().Id==source.Id && sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId!=aBefore,"Successful source save precedes replacing the owned target session");
            Check(sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==b.DeviceId).Snapshot.SessionId==bBefore,"Switching one target does not stop the other device session");
            using(var reloaded=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(directory,"prefs.json")),()=>[source],()=>[],_=>Task.CompletedTask,logger))
            {
                await reloaded.InitializeAsync();var known=reloaded.Cards.Single(c=>c.DeviceId==a.DeviceId);
                Check(known.Confirmed,"Verified stable device restores previously confirmed unchanged output parameters");
                known.Confirmed=false;reloaded.Synchronize();
                Check(!known.Confirmed,"Explicitly revoking device confirmation is respected rather than restored on every status tick");
                known.Confirmed=true;known.Width="1800";reloaded.Synchronize();
                Check(!known.Confirmed,"Restored confirmation is not reused when output geometry changes");
            }
            var switched=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId;
            Check(!await sessions.StopOwnedAsync(a.DeviceId,aBefore) && sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId==switched,"Stale session identity cannot stop a newer owned session");
            var brokenReplacement=source.Clone();brokenReplacement.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Image,Path=Path.Combine(directory,"missing-replacement.png")});
            var retainedHome=homeOutput;var updatesBeforeFailure=homeUpdates;
            await devices.ApplySourceAsync(brokenReplacement,[a.DeviceId],LayoutAdaptation.Contain,replaceOwned:true);
            Check(sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId==switched && ca.IsActive,"Missing replacement assets are rejected before stopping the existing session");
            Check(ReferenceEquals(homeOutput,retainedHome) && homeUpdates==updatesBeforeFailure,"Rejected replacement preserves the previous home output preview");
            var readyToChange=new TaskCompletionSource();var resume=new TaskCompletionSource();
            preview.SaveSourceAsync=async()=>{readyToChange.SetResult();await resume.Task;};
            preview.ApplyTargetCommand.Execute(null);await readyToChange.Task;ca.Width="1900";resume.SetResult();await preview.PendingApply;
            Check(sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==a.DeviceId).Snapshot.SessionId==switched && preview.Status.Contains("已保存"),"Changed device geometry after saving prevents switching the output");
            ca.Width="1920";ca.Confirmed=true;await preview.PendingPreview;
            preview.SaveSourceAsync=()=>sourceStore.SaveAsync(source);
            preview.SourceScene=new ThemeScene(source);
            preview.Target=null;await preview.PendingPreview;
            Check(ReferenceEquals(preview.PreviewScene,preview.SourceScene),"No-device workflow retains a genuine live source scene for preview and saving");
var sessionsBeforeSave=sessions.Snapshot.Select(s=>s.Snapshot.SessionId).ToArray();
            preview.SaveOnlyCommand.Execute(null);await preview.PendingApply;
            Check(preview.Status.Contains("尚未应用") && sessions.Snapshot.Select(s=>s.Snapshot.SessionId).SequenceEqual(sessionsBeforeSave),"Save-only remains available without a target and never changes output sessions");
            preview.Target=ca;await preview.PendingPreview;
            preview.SourceScene?.Dispose();preview.SourceScene=null;
            Check(preview.SaveOnlyCommand.CanExecute(null) && preview.ApplyLabel.Contains("保存"),"Editor dialog exposes distinct save-only and save-and-apply actions");
            await sessions.StopAsync(a.DeviceId);devices.Synchronize();ca.Confirmed=true;
            using(var external=leases.TryAcquire(a.DeviceId))
            {
                preview.SaveSourceAsync=null;
                Check(external is not null,"External lease fixture owns the device before apply");
                preview.ApplyTargetCommand.Execute(null);await preview.PendingApply;
                using var secondOwner=leases.TryAcquire(a.DeviceId);
                Check(secondOwner is null && !ca.IsActive && ca.Message.Contains("已有输出"),"Save-and-apply does not steal or stop an external device owner");
                Check(preview.Results.Count==1 && preview.Results.Single().Message.Contains("已有输出"),"The dialog shows the real per-device conflict result directly");
            }
            var legacyRunning=true;var legacyStops=0;
            devices.HasLegacyOwnedOutput=c=>c.DeviceId==a.DeviceId && legacyRunning;
            devices.ReleaseLegacyOwnedOutputAsync=c=>{legacyStops++;legacyRunning=false;return Task.FromResult(true);};
            await devices.ApplySourceAsync(brokenReplacement,[a.DeviceId],LayoutAdaptation.Contain,replaceOwned:true);
            Check(legacyRunning && legacyStops==0,"Invalid replacement is rejected before stopping matching legacy USB output");
            preview.ApplyTargetCommand.Execute(null);await preview.PendingApply;
            Check(preview.SaveSourceAsync is null && !legacyRunning && legacyStops==1 && ca.IsActive && cb.IsActive && JsonSerializer.Serialize(source)==original,"Saved-theme apply transfers matching legacy USB output without saving or interrupting another device");
            preview.SaveSourceAsync=()=>sourceStore.SaveAsync(source);
            var window = new CreatorDevicePreview(preview) { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
            window.Show(); await Task.Delay(80); window.UpdateLayout();
            Check(((Button)window.FindName("ApplyTarget")).IsVisible && window.FindName("NextStep") is null, "Single-window usage exposes the primary action without wizard steps");
            Check(!((Expander)window.FindName("AdaptationOptions")).IsExpanded && !((Expander)window.FindName("BatchOptions")).IsExpanded, "Advanced adaptation and multi-device controls are collapsed by default");
            Check(ReferenceEquals(((Button)window.FindName("ApplyTarget")).Command,preview.ApplyTargetCommand) && !((CheckBox)window.FindName("TargetConfirmation")).IsVisible, "Primary button keeps actual checks and already confirmed device is not repeatedly prompted");            window.UpdateLayout(); Capture(window, "creator-device-landscape.png");
            preview.Target = cb; await preview.PendingPreview; window.UpdateLayout(); Capture(window, "creator-device-portrait.png");
            Check(window.ActualWidth >= 960 && preview.PreviewAspect < 1, "Real WPF device preview window renders portrait surface without rotating landscape UI");
            window.Close(); await preview.DisposeAsync();
            Check(sessions.Snapshot.Any(s => s.Snapshot.DesiredState == OutputDesiredState.Running), "Closing preview does not stop separately applied device output");
            foreach (var closeCase in new[] { "single", "repeated", "owner" })
            {
                await using var releasedModel = new CreatorDevicePreviewViewModel(source, devices, logger);
                await releasedModel.PendingPreview;
                await releasedModel.DisposeAsync(); // Forces the exact synchronous cleanup path that crashed.
                var releasedWindow = new CreatorDevicePreview(releasedModel)
                    { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var closeCount = 0;
                releasedWindow.Closed += (_, _) => { closeCount++; closed.TrySetResult(); };
                Window? closeOwner = null;
                if (closeCase == "owner")
                {
                    closeOwner = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
                    closeOwner.Show(); releasedWindow.Owner = closeOwner;
                }
                releasedWindow.Show();
                if (closeOwner is not null) closeOwner.Close();
                else { releasedWindow.Close(); if (closeCase == "repeated") releasedWindow.Close(); }
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(closeCount == 1, $"Completed preview cleanup closes once without reentrant Window.Close ({closeCase})");
            }
            var missingSource = source.Clone();
            missingSource.Landscape.Layers.Add(new ThemeLayer
            { Kind = ThemeElementKind.Wallpaper, Path = Path.Combine(directory, "missing-preview-wallpaper.jpg"), Width = 320, Height = 80 });
            await using (var failedAssetPreview = new CreatorDevicePreviewViewModel(missingSource, devices, logger))
            {
                await failedAssetPreview.PendingPreview;
                Check(failedAssetPreview.Status.Contains("无法预览"), "Missing preview asset stays a recoverable preview error");
                var missingWindow = new CreatorDevicePreview(failedAssetPreview)
                    { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
                var missingClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                missingWindow.Closed += (_, _) => missingClosed.TrySetResult();
                missingWindow.Show(); missingWindow.Close();
                await missingClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(!missingWindow.IsVisible, "Closing failed missing-asset preview does not terminate the application");
            }
            await devices.ReleaseForLegacyUsbAsync("preview-A");
            Check(!ca.IsActive && cb.IsActive,"Switching from a managed theme to wallpaper stops only the matching owned D92 session");
            using(var outside=leases.TryAcquire(a.DeviceId))
            {
                await devices.ReleaseForLegacyUsbAsync("preview-A");
                using var stillBlocked=leases.TryAcquire(a.DeviceId);
                Check(outside is not null && stillBlocked is null && cb.IsActive,"Reverse handoff never releases an external owner or unrelated output");
            }
            await sessions.StopAsync(a.DeviceId); await sessions.StopAsync(b.DeviceId);

            var ambiguousRegistry=new DeviceRegistry();var ambiguous=a with { DeviceId="ambiguous:confirmed-test",Identity=a.Identity with { Confidence=IdentityConfidence.Ambiguous } };
            ambiguousRegistry.Replace(DeviceTransport.UsbHid,[ambiguous]);
            var ambiguousStore=new DevicePreferenceStore(Path.Combine(directory,"ambiguous-prefs.json"));await ambiguousStore.SaveAsync([new(ambiguous.DeviceId,"标识不唯一",1920,462,0,"",true)]);
            await using(var ambiguousSessions=new OutputSessionManager(ambiguousRegistry,new InMemoryDeviceLeaseProvider(),(_,_)=>ValueTask.FromResult<IOutputSessionDriver>(new Driver())))
            using(var ambiguousPage=new ManagedDevicesViewModel(ambiguousRegistry,ambiguousSessions,ambiguousStore,()=>[source],()=>[],_=>Task.CompletedTask,logger))
            {
                await ambiguousPage.InitializeAsync();Check(!ambiguousPage.Cards.Single().Confirmed,"Ambiguous physical identity never inherits persisted device confirmation");
            }
            var missing = source.Clone(); missing.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = Path.Combine(directory, "missing.png") });
            await using var missingPreview = new CreatorDevicePreviewViewModel(missing, devices, logger); await missingPreview.PendingPreview;
            Check(missingPreview.PreviewScene is null && missingPreview.Issues.Count > 0 && !missingPreview.ApplyTargetCommand.CanExecute(null), "Missing material reports a real failure and disables applying the preview target");

            var font = source.Clone(); font.Landscape.Layers[0].FontFamily = "DeskDisplay-Uninstalled-Test-Font";
            await using var fontPreview = new CreatorDevicePreviewViewModel(font, devices, logger); await fontPreview.PendingPreview;
            Check(fontPreview.Issues.Any(i => i.Contains("字体")) && fontPreview.PreviewScene is not null, "Unavailable font is diagnosed without pretending it is installed");

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 16).ToArray(), 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); var imagePath = Path.Combine(directory, "material.png");
            using (var file = File.Create(imagePath)) encoder.Save(file);
            var stretch = source.Clone(); stretch.Landscape.Layers.Add(new() { Kind = ThemeElementKind.Image, Path = imagePath, Fill = ImageDisplayMode.Stretch });
            await using var stretchPreview = new CreatorDevicePreviewViewModel(stretch, devices, logger); await stretchPreview.PendingPreview;
            ca.Confirmed = true;
            Check(stretchPreview.Issues.Any(i => i.Contains("拉伸")) && !stretchPreview.ApplyTargetCommand.CanExecute(null), "Legacy stretch is visible for review but cannot be silently applied");

            using var deviceVm = new DevicesViewModel(display, new TargetDisplayService(new(), new Settings(), logger), logger);
            deviceVm.AttachManaged(devices);
            using var main = new MainViewModel(deviceVm, new ClockScene());
            var creator = new CreatorViewModel(main, logger, new ThemeProjectStore(Path.Combine(directory, "themes"))); main.SetCreator(creator);
            await creator.InitializeAsync(); creator.TemplateCommand.Execute(null); await creator.PendingOperation;
            creator.PreviewCommand.Execute(null); main.SelectedPage = AppPage.Creator;
            var bytesBeforeEntry = JsonSerializer.Serialize(creator.Project); var dirtyBefore = creator.IsDirty;
            var shell = new MainWindow { DataContext = main, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
            shell.Show(); await Task.Delay(80); shell.UpdateLayout();
            var entry = Children<Button>(shell).Single(b => Equals(b.Content, "按设备预览与应用") && b.IsVisible);
            var opened = false;
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(80);
                var dialog = PresentationSource.CurrentSources.Cast<PresentationSource>().OfType<HwndSource>()
                    .Select(s => s.RootVisual).OfType<CreatorDevicePreview>().Single();
                opened = dialog.DataContext is CreatorDevicePreviewViewModel; dialog.Close();
            });
            entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(opened && JsonSerializer.Serialize(creator.Project) == bytesBeforeEntry && creator.IsDirty == dirtyBefore,
                "Actual creator preview button opens the device dialog without changing source or dirty state");
            creator.EditCommand.Execute(null); shell.UpdateLayout(); var directlyOpened = false;
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(80);
                var dialog = PresentationSource.CurrentSources.Cast<PresentationSource>().OfType<HwndSource>().Select(s => s.RootVisual).OfType<CreatorDevicePreview>().Single();
                directlyOpened = true; dialog.Close();
            });
            Children<Button>(shell).Single(b => Equals(b.Content, "完成并使用") && b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(directlyOpened && creator.IsEditor && JsonSerializer.Serialize(creator.Project) == bytesBeforeEntry, "Unified editor action opens device preview directly without an intermediate page or source mutation");
            creator.SelectedLayer=creator.Project.ActiveLayout.Layers.First(l=>l.Kind==ThemeElementKind.DigitalClock);shell.UpdateLayout();
            var delayedFont=Children<ComboBox>(shell).First(c=>c.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding.Path.Path==nameof(CreatorViewModel.FontFamily) && c.IsVisible);
            delayedFont.SetCurrentValue(ComboBox.TextProperty,"Georgia");var committedSnapshot=false;
            _=Dispatcher.CurrentDispatcher.BeginInvoke(async()=>
            {
                await Task.Delay(80);
                var dialog=PresentationSource.CurrentSources.Cast<PresentationSource>().OfType<HwndSource>().Select(s=>s.RootVisual).OfType<CreatorDevicePreview>().Single();
                var model=(CreatorDevicePreviewViewModel)dialog.DataContext;await model.PendingPreview;
                committedSnapshot=model.PreviewScene?.Project.ActiveLayout.Layers.First(l=>l.Kind==ThemeElementKind.DigitalClock).FontFamily=="Georgia";
                dialog.Close();
            });
            Children<Button>(shell).Single(b=>Equals(b.Content,"完成并使用") && b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(committedSnapshot && creator.FontFamily=="Georgia","Opening use workflow commits delayed font edits before snapshotting the preview source");
            await using (var editingPreview = new CreatorDevicePreviewViewModel(creator.Project, devices, logger))
            {
                editingPreview.EditAdaptedLayout = creator.AddEditableLayout;
                await editingPreview.PendingPreview;
                var adapted = editingPreview.CreateEditableLayout();
                creator.AddEditableLayout(adapted); await creator.PendingOperation;
                Check(creator.IsEditor && creator.IsDirty && creator.Project.SelectedLayoutId == adapted.Id && creator.Scene.PixelWidth == 1920 && creator.Scene.PixelHeight == 462,
                    "Saving adaptation returns to real editor with separate editable actual-resolution layout");
                creator.ProjectName = "多布局离线验收"; creator.LayoutName = "桌面横屏";
                creator.Add(ThemeElementKind.Text); await creator.PendingOperation; creator.X = 77;
                Check(creator.Project.ActiveLayout.Layers.Last().X == 77 && creator.Project.Landscape.Layers.Count == 1,
                    "Manual adjustment edits adapted layout without modifying original layers");
                creator.CopyLayoutCommand.Execute(null); await creator.PendingOperation;
                Check(creator.Project.LayoutVersions.Count == 2 && creator.Project.SelectedLayoutId != adapted.Id, "Editor copies and selects independent layout version");
                creator.DeleteLayoutCommand.Execute(null); await creator.PendingOperation;
                creator.UndoCommand.Execute(null); await creator.PendingOperation;
                Check(creator.Project.LayoutVersions.Count == 2 && creator.Project.SelectedLayoutId is not null, "Editor undo restores deleted custom layout");
                creator.SelectedLayoutChoice = creator.LayoutChoices.First(v => v.Id == adapted.Id); await creator.PendingOperation;
                shell.Width = 1320; shell.Height = 900; shell.UpdateLayout(); await Task.Delay(80); Capture(shell, "creator-layout-editor-1320.png");
                shell.Width = 1100; shell.Height = 740; shell.UpdateLayout(); await Task.Delay(80); Capture(shell, "creator-layout-editor-1100.png");
                Check(Children<ComboBox>(shell).Any(c => c.ItemsSource == creator.LayoutChoices || c.Items.Cast<object>().OfType<EditorLayoutChoice>().Any(v => v.Id == adapted.Id)), "Real shell exposes saved layout selector at compact window size");
                creator.SaveCommand.Execute(null); await creator.PendingOperation;
                var saved = (await new ThemeProjectStore(Path.Combine(directory, "themes")).ListAsync()).Single(p => p.Id == creator.Project.Id);
                Check(saved.Version == 2 && saved.LayoutVersions.Any(v => v.Name == "桌面横屏"), "Creator normal save persists actual edited V2 layout versions");
                var deviceWindow = new CreatorDevicePreview(editingPreview) { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000 };
                deviceWindow.Show(); await Task.Delay(80); deviceWindow.UpdateLayout(); Capture(deviceWindow, "creator-layout-preview.png");
                ((Expander)deviceWindow.FindName("AdaptationOptions")).IsExpanded=true; deviceWindow.UpdateLayout();
                var adaptButton = Children<Button>(deviceWindow).Single(b => Equals(b.Content, "保存为布局并微调"));
                var count = creator.Project.LayoutVersions.Count;
                adaptButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await creator.PendingOperation;
                Check(creator.Project.LayoutVersions.Count == count + 1 && creator.IsEditor, "Real device preview action creates editable layout through editor callback");
            }
            ca.PhysicalGroup = cb.PhysicalGroup = "离线同屏"; await devices.SaveAsync(); ca.Confirmed = cb.Confirmed = true;
            await devices.ApplySourceAsync(creator.Project, [ca.DeviceId, cb.DeviceId], LayoutAdaptation.Contain);
            Check(ca.IsActive && !cb.IsActive && cb.Message.Contains("物理屏"), "Managed device batch reports same-screen conflict independently");
            cb.PhysicalGroup = "changed"; await devices.SaveAsync();
            Check(devices.Status.Contains("停止全部输出"), "Association edits cannot bypass group protection during active output");
            await sessions.StopAsync(ca.DeviceId); devices.Synchronize();
            registry.Replace(DeviceTransport.UsbHid, []); devices.Synchronize();
            await using (var offline = new CreatorDevicePreviewViewModel(creator.Project, devices, logger))
            {
                await offline.PendingPreview;
                Check(offline.Target is not null && offline.PreviewScene is not null && offline.Readiness.Contains("设备未连接") && !offline.ApplyTargetCommand.CanExecute(null), "Known disconnected target defaults to a real offline preview without enabling output");
            }
            shell.Close();
            await devices.PrepareForCloseAsync();
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream);
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T item) yield return item;
            foreach (var nested in Children<T>(child)) yield return nested;
        }
    }
    private sealed class Settings : ISettingsService
    {
        public Task<AppSettings> LoadAsync() => Task.FromResult(new AppSettings());
        public Task<bool> SaveAsync(AppSettings settings) => Task.FromResult(true);
    }
    private sealed class Driver : IOutputSessionDriver
    {
        public async Task RunAsync(Action<OutputActualState> report, CancellationToken ct) { report(OutputActualState.Streaming); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Displays : IDisplayManager
    {
        public event EventHandler? DisplaysChanged { add { } remove { } }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>([]);
        public void Dispose() { }
    }
}







