using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Library;
using Serilog;
namespace DeskDisplay.DisplayTests;
internal static class ContentExperienceTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root=Path.Combine(Path.GetTempPath(),"D92-content-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var initial=new AppSettings();var settings=new JsonSettingsService(logger,Path.Combine(root,"settings.json"));await settings.SaveAsync(initial);
            using var displays=new WorkspaceTests.Manager();using var windows=new WorkspaceTests.Output();using var devices=new DevicesViewModel(displays,new TargetDisplayService(initial,settings,logger),logger,windows);
            var clock=new ClockScene();using var main=new MainViewModel(devices,clock);main.ConfigureOutputExperience(settings,initial);
            main.SetImageEditor(new ImageSceneViewModel(devices,settings,initial,clock,main.PreviewScene,logger));main.SetVideoEditor(new VideoSceneViewModel(main,new WorkspaceTests.Playback(),logger));main.SetWorkspace(new WorkspaceViewModel(main,clock,new PerformanceScene(),windows,settings,new WorkspaceTests.Sensors(),initial,logger,false));await main.Workspace!.RestoreAsync();
            var red=Path.Combine(root,"red.png");WriteImage(red);var copy=Path.Combine(root,"copy.png");File.Copy(red,copy);var missing=Path.Combine(root,"missing.png");
            var a=new WallpaperAsset{Name="red",Path=red,Width=32,Height=8,Favorite=true,Category="test",Subcategory="red"};var duplicate=new WallpaperAsset{Name="copy",Path=copy,Width=32,Height=8};var lost=new WallpaperAsset{Name="lost",Path=missing,Width=32,Height=8,Favorite=true,Category="keep"};
            var catalog=new WallpaperCatalog(logger,Path.Combine(root,"Library"));await catalog.SaveAsync(new(){Assets=[a,duplicate,lost]});main.SetLibrary(new LibraryViewModel(main,catalog,logger));await main.Library!.InitializeAsync();
            var store=new ThemeProjectStore(Path.Combine(root,"Themes"));var good=new ThemeProject{Name="saved clock"};good.Landscape.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,ShowSeconds=true,FontSize=100,Width=800,Height=250});var bad=good.Clone();bad.Id=Guid.NewGuid();bad.Name="planned failure";var blocked=good.Clone();blocked.Id=Guid.NewGuid();blocked.Name="cancelled start";await store.SaveAsync(good);await store.SaveAsync(bad);await store.SaveAsync(blocked);
            var creator=new CreatorViewModel(main,logger,store);main.SetCreator(creator);await creator.InitializeAsync();await creator.OpenProjectAsync(good.Clone());creator.Add(ThemeElementKind.Text);creator.Text="unsaved editor";var editing=JsonSerializer.Serialize(creator.Project);
            var late=good.Clone();late.Id=Guid.NewGuid();late.Name="first frame failure";await store.SaveAsync(late);
            var registry=new DeviceRegistry();var descriptor=new DeviceDescriptor("content-target","D92",DeviceTransport.UsbHid,new("test","content-serial",IdentityConfidence.VerifiedStable),new("test","offline",1),true);registry.Replace(DeviceTransport.UsbHid,[descriptor]);var goodStarts=0;
            await using var sessions=new OutputSessionManager(registry,new InMemoryDeviceLeaseProvider(),async(context,ct)=>
            {if(context.Theme.ThemeId==bad.Id)throw new IOException("planned start failure");if(context.Theme.ThemeId==blocked.Id)await Task.Delay(Timeout.InfiniteTimeSpan,ct);if(context.Theme.ThemeId==good.Id)goodStarts++;return new Driver(context.Theme.ThemeId==late.Id);});
            using var managed=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(root,"devices.json")),()=>creator.Projects.Select(p=>p.Project).ToArray(),()=>[],_=>Task.CompletedTask,logger){Coordinator=main.Output};var factory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger);managed.PreviewFactory=factory.CreatePreviewAsync;managed.HomeOutputChanged=main.SetHomeOutputScene;devices.AttachManaged(managed);await managed.InitializeAsync();var card=managed.Cards.Single();card.Width="1920";card.Height="462";card.Confirmed=true;
            var acceptedSources=0;managed.ContentAccepted=(_,_)=>{acceptedSources++;return Task.CompletedTask;};await managed.ApplySourceAsync(good,[card.DeviceId],LayoutAdaptation.Contain,true);var accepted=acceptedSources;await managed.ApplySourceAsync(bad,[card.DeviceId],LayoutAdaptation.Contain,true);
            Check(card.IsActive&&card.AppliedTheme==good.Name&&acceptedSources==accepted&&goodStarts==2&&card.Message.Contains("重新启动原内容"),"Failed replacement startup restores the previous owned theme and does not report the failed theme as accepted");
            await managed.ApplySourceAsync(late,[card.DeviceId],LayoutAdaptation.Contain,true);Check(card.IsActive&&card.AppliedTheme==good.Name&&goodStarts==3&&acceptedSources==accepted,"Failure after driver creation but before its first frame also restores the previous owned source");
            var beforeStop=goodStarts;var pending=managed.ApplySourceAsync(blocked,[card.DeviceId],LayoutAdaptation.Contain,true);await Until(()=>sessions.Snapshot.Any(s=>s.Snapshot.ThemeId==blocked.Id&&s.Snapshot.ActualState==OutputActualState.Preparing));await managed.StopAsync(card);await pending;
            Check(!card.IsActive&&goodStarts==beforeStop,"Explicit stop cancels preparation without triggering automatic rollback restart");
            await managed.ApplySourceAsync(good,[card.DeviceId],LayoutAdaptation.Contain,true);var restoreOwned=managed.CaptureManagedRestore(descriptor.Identity.SerialNumber!)??throw new Exception("No owned restore captured");
            await managed.ReleaseForLegacyUsbAsync(descriptor.Identity.SerialNumber!);await restoreOwned();
            Check(card.IsActive&&card.AppliedTheme==good.Name,"A failed standalone-video handoff can restore the captured managed source on the same device");
            try{await restoreOwned();throw new Exception("Stale managed restore overwrote output");}catch(InvalidOperationException){Check(card.IsActive,"Captured managed rollback refuses to overwrite a newer accepted output");}await managed.StopAsync(card);
            await main.InitializeContentListsAsync(root);main.TogglePin(new("Theme",good.Id));main.TogglePin(new("Wallpaper",a.Id));await main.FlushContentListsAsync();
            Check(main.HomePinnedEntries.Count==2&&main.HomePinnedEntries.First().Theme?.Project.Id==good.Id,"Home pins resolve actual saved themes and wallpapers independently of recent-use order");
            var preferences=new ContentListStore(root);Check((await preferences.ReadAsync("home-pins.json",new List<ContentRef>())).Count==2,"Home pin identities persist independently of thumbnail paths");main.TogglePin(new("Theme",good.Id));Check(main.HomePinnedEntries.Count==1,"Unpin removes only the home shortcut and preserves its source project");
            var mix=main.MixedPlaylist;mix.Target=card;managed.ContentAccepted=(target,_)=>{mix.ObserveAccepted(target);return Task.CompletedTask;};managed.ExplicitStopRequested=mix.ObserveStop;
            mix.Add(new("Wallpaper",a.Id));mix.Add(new("Theme",good.Id));mix.Add(new("Wallpaper",lost.Id));mix.Add(new("Wallpaper",a.Id));Check(mix.Items.Count==3,"Mixed playlist stores resource references and rejects duplicate additions");
            mix.Items[0].Seconds=17;await mix.StartAsync(mix.Items[0].Entry.EntryId,true);Check(mix.IsRunning&&mix.IsPaused&&card.AppliedTheme=="red","Mixed playlist starts with a real wallpaper and preserves an initial paused state");
            await mix.NextAsync();Check(mix.IsRunning&&mix.IsPaused&&card.AppliedTheme==good.Name&&main.HomeScene is ThemeScene theme&&theme.Project.Landscape.Layers.Any(l=>l.Kind==ThemeElementKind.DigitalClock),"Manual next moves from wallpaper to an editable clock theme without replacing the editor");
            await mix.NextAsync();Check(card.AppliedTheme=="red"&&mix.IsRunning,"Mixed playlist skips a missing wallpaper and continues with available content");
            Check(JsonSerializer.Serialize(creator.Project)==editing&&creator.IsDirty,"Mixed playback leaves the unsaved editor and its project identity unchanged");
            await managed.StopAsync(card);Check(!mix.IsRunning&&!card.IsActive,"Stopping a device also stops its mixed scheduler instead of restarting output later");
            mix.Selected=mix.Items[1];mix.Move(-1);mix.Shuffle=true;await mix.FlushAsync();using(var restored=new MixedPlaylistViewModel(main,root)){await restored.InitializeAsync();Check(restored.Items[0].Entry.Content.Kind=="Theme"&&restored.Shuffle&&restored.Items[1].Seconds==17,"Mixed order, per-item duration and random mode survive reloading without auto-output");}
            var videoPath=await VideoExperienceTests.MakeVideoAsync(root);await main.Library.ImportAsync([videoPath]);var videoAsset=main.Library.AllItems.Single(i=>i.Asset.IsVideo);
            mix.Add(new("Wallpaper",videoAsset.Asset.Id));var videoRow=mix.Items.Single(i=>i.Entry.Content.Id==videoAsset.Asset.Id);videoRow.PlayToEnd=true;mix.Shuffle=false;
            await mix.StartAsync(videoRow.Entry.EntryId);Check(mix.IsRunning&&videoRow.Kind=="视频"&&card.AppliedTheme==videoAsset.Name,"Mixed playlist accepts a real video resource alongside wallpapers and saved themes");
            Check(mix.NextName==good.Name&&mix.CountdownText.Contains("等待视频播完"),"Mixed playback status resolves the actual next item and completion mode");
            await mix.VideoCompletedAsync("another-device",videoAsset.Asset.Id);Check(card.AppliedTheme==videoAsset.Name,"Another device's loop event cannot advance the selected mixed playlist");
            mix.TogglePause();await mix.VideoCompletedAsync(descriptor.Identity.SerialNumber!,videoAsset.Asset.Id);Check(card.AppliedTheme==videoAsset.Name&&mix.IsPaused,"A video finishing while switching is paused keeps the current output");
            mix.TogglePause();await Until(()=>card.AppliedTheme==good.Name);Check(mix.IsRunning,"Resuming after a completed video advances to the next saved theme");
            await managed.StopAsync(card);await mix.FlushAsync();using(var reloadedMix=new MixedPlaylistViewModel(main,root)){await reloadedMix.InitializeAsync();Check(reloadedMix.Items.Single(i=>i.Entry.Content.Id==videoAsset.Asset.Id).PlayToEnd,"The selected video completion mode survives mixed playlist reload");}
            foreach(var row in mix.Items.Where(i=>i.Entry.Content.Id!=videoAsset.Asset.Id).ToArray()){mix.Selected=row;mix.Remove();}
            await mix.StartAsync(videoRow.Entry.EntryId);var singleSession=sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==card.DeviceId).Snapshot.SessionId;await mix.VideoCompletedAsync(descriptor.Identity.SerialNumber!,videoAsset.Asset.Id);
            Check(mix.IsRunning&&sessions.Snapshot.Single(s=>s.Snapshot.DeviceId==card.DeviceId).Snapshot.SessionId==singleSession&&mix.CountdownText.Contains("单项循环"),"A single video loops without tearing down its decoder at every completed cycle");await managed.StopAsync(card);
            var issues=await ResourceAudit.ScanAsync([a,duplicate,lost]);Check(issues.Count(i=>i.Issue=="内容重复")==2&&issues.Single(i=>i.Issue=="文件缺失").AssetId==lost.Id,"Resource audit distinguishes actual byte-identical files from missing references without deleting data");
            var repair=Path.Combine(root,"repair");Directory.CreateDirectory(Path.Combine(repair,"one"));Directory.CreateDirectory(Path.Combine(repair,"two"));var replacement=Path.Combine(repair,"one","missing.png");WriteImage(replacement);WriteImage(Path.Combine(repair,"two","missing.png"));Check(ResourceAudit.FindFolderMatches([lost],repair).Count==0,"Folder repair refuses ambiguous same-name matches in different directories");File.Delete(Path.Combine(repair,"two","missing.png"));Check(ResourceAudit.FindFolderMatches([lost],repair).Single().Value==replacement,"Folder repair offers a unique existing same-name candidate");
            var reference=new ThemeProject{Name="reference"};reference.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Image,AssetId=lost.Id,Path=lost.Path});await store.SaveAsync(reference);await main.RelinkLibraryResourceAsync(lost.Id,replacement);
            Check(main.Library.AllItems.Single(i=>i.Asset.Id==lost.Id).Asset is {Favorite:true,Category:"keep"} repaired&&repaired.Path==replacement&&(await store.ReadSavedAsync(reference.Id)).Landscape.Layers[0].Path==replacement,"Relinking preserves metadata and resource ID while updating saved theme references");
            Check(JsonSerializer.Serialize(creator.Project)==editing,"Repairing saved references does not overwrite a dirty editor");
            Check(creator.EditorSaveSummary.Contains("未保存"),"Editor clearly labels changes that have not been saved as a formal project");await creator.PersistDraftAsync();Check(creator.EditorSaveSummary.Contains("自动草稿已保护"),"Successful automatic draft is distinguished from a formal save");
            var draft=(await store.ListDraftsAsync()).Single(d=>d.Project.Id==good.Id);var savedBefore=JsonSerializer.Serialize(await store.ReadSavedAsync(good.Id));
            creator.ConfirmDraftRecovery=_=>Task.FromResult(false);creator.RecoverDraftCommand.Execute(new CreatorDraft(draft));await creator.PendingOperation;Check(JsonSerializer.Serialize(creator.Project)==editing&&(await store.ListDraftsAsync()).Any(d=>d.Project.Id==good.Id),"Cancelling draft preview preserves editor contents and the recovery draft");
            using(var recovery=new CreatorViewModel(main,logger,store)){await recovery.InitializeAsync();await recovery.RecoverDraftAsync(draft);Check(recovery.Project.Id!=good.Id&&recovery.IsDirty&&JsonSerializer.Serialize(await store.ReadSavedAsync(good.Id))==savedBefore,"Draft recovery creates a dirty independent copy and preserves the formal source");}
            await main.FlushContentListsAsync();await main.Library.PrepareForCloseAsync();await main.Workspace.PrepareForCloseAsync();await main.Images!.PrepareForCloseAsync();await managed.PrepareForCloseAsync();await main.RuntimePending;
        }
        finally{Directory.Delete(root,true);}
    }
    private static void WriteImage(string path){var pixels=new byte[32*8*4];for(var i=0;i<pixels.Length;i+=4){pixels[i+2]=220;pixels[i+3]=255;}var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(32,8,96,96,PixelFormats.Bgra32,null,pixels,128)));using var file=File.Create(path);encoder.Save(file);}
    private static async Task Until(Func<bool> condition){var limit=DateTime.UtcNow.AddSeconds(8);while(!condition()){if(DateTime.UtcNow>limit)throw new TimeoutException();await Task.Delay(20);}}
    private static void Check(bool result,string message){if(!result)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private sealed class Driver(bool fail=false):IOutputSessionDriver{public async Task RunAsync(Action<OutputActualState> report,CancellationToken ct){if(fail)throw new IOException("first frame rejected");report(OutputActualState.Streaming);await Task.Delay(Timeout.InfiniteTimeSpan,ct);}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
}
