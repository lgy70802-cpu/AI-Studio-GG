using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.Views;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class OutputExperienceTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root=Path.Combine(Path.GetTempPath(),"D92-output-experience-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var coordinator=new OutputCoordinator();var gate=new TaskCompletionSource();var first=coordinator.RunAsync("first",()=>gate.Task);
            try {await coordinator.RunAsync("second",()=>Task.CompletedTask);throw new Exception("Concurrent transition accepted");}
            catch(InvalidOperationException){Check(coordinator.IsBusy,"Unified coordinator rejects overlapping transitions without queuing stale requests");}
            gate.SetResult();await first;
            Check(!coordinator.IsBusy && coordinator.CompletedTransitions==1 && coordinator.LastTransitionMilliseconds>=0,"Transition completion releases busy state and measures actual elapsed time");
            try{await coordinator.RunAsync("failure",()=>Task.FromException(new IOException("actual cause")));}catch(IOException){}
            Check(coordinator.IsError && coordinator.Message.Contains("actual cause") && !coordinator.IsBusy,"Unified feedback exposes real failure reason and remains usable after errors");
            var nested=false;await coordinator.RunAsync("outer",()=>coordinator.RunAsync("handoff",()=>{nested=true;return Task.CompletedTask;}));
            Check(nested&&!coordinator.IsBusy && coordinator.CompletedTransitions==2,"Owned nested handoffs share one transition while unrelated requests remain excluded");

            var initial=new AppSettings{LastScene="保留原设置"};var settings=new JsonSettingsService(logger,Path.Combine(root,"settings.json"));await settings.SaveAsync(initial);
            using var displays=new WorkspaceTests.Manager();using var windows=new WorkspaceTests.Output();
            using var devices=new DevicesViewModel(displays,new TargetDisplayService(initial,settings,logger),logger,windows);
            using var main=new MainViewModel(devices,new ClockScene());main.ConfigureOutputExperience(settings,initial);
            Check(!main.CloseToTray&&!main.LaunchAtLogin&&!main.RestoreD92OnStartup,"Resident operation, login launch and device restoration are all opt-in for existing installations");
            var registrationCalls=0;main.SetLoginStartupAsync=_=>{registrationCalls++;return Task.CompletedTask;};
            main.CloseToTray=true;main.LaunchAtLogin=true;await main.RuntimePending;
            main.RememberOutput(new("A","Theme",ThemeId:Guid.NewGuid()),"A");main.RememberOutput(new("B","Image","local.png"),"B");await main.RuntimePending;
            var saved=await settings.LoadAsync();
            Check(saved.LastScene==initial.LastScene && saved.CloseToTray && saved.LaunchAtLogin && saved.D92ResumeContents.Count==2,"Runtime preferences merge without overwriting existing scene settings or other devices");
            Check(registrationCalls==1,"Recording output does not repeatedly rewrite Windows startup registration");
            var restoreCalls=0;await main.RestoreRememberedOutputsAsync(_=>{restoreCalls++;return Task.CompletedTask;});
            Check(restoreCalls==0,"Disabled startup restoration never invokes output or hardware callbacks");
            main.RestoreD92OnStartup=true;await main.RuntimePending;
            await main.RestoreRememberedOutputsAsync(c=>{restoreCalls++;if(c.Serial=="A")throw new IOException("missing source");return Task.CompletedTask;});
            Check(restoreCalls==2,"One restore failure does not prevent restoration of another saved device");
            main.RememberOutput(null,"A");await main.RuntimePending;
            Check((await settings.LoadAsync()).D92ResumeContents.Single().Serial=="B","Explicit stop removes only the matching device resume record");

            var registry=new DeviceRegistry();var descriptor=new DeviceDescriptor("temporary-D92","D92",DeviceTransport.UsbHid,new("test","preview-serial",IdentityConfidence.VerifiedStable),new("preview","offline",1),true);registry.Replace(DeviceTransport.UsbHid,[descriptor]);
            await using var sessions=new OutputSessionManager(registry,new InMemoryDeviceLeaseProvider(),(_,_)=>ValueTask.FromResult<IOutputSessionDriver>(new Driver()));
            using var managed=new ManagedDevicesViewModel(registry,sessions,new DevicePreferenceStore(Path.Combine(root,"device.json")),()=>[],()=>[],_=>Task.CompletedTask,logger){Coordinator=main.Output};
            var factory=new WindowsThemeSessionFactory(Dispatcher.CurrentDispatcher,displays,logger);managed.PreviewFactory=factory.CreatePreviewAsync;managed.HomeOutputChanged=main.SetHomeOutputScene;
            var accepted=0;managed.ContentAccepted=(_,_)=>{accepted++;main.ClearDevicePreview();return Task.CompletedTask;};
            await managed.InitializeAsync();var card=managed.Cards.Single();card.Width="1920";card.Height="462";card.Confirmed=true;
            devices.AttachManaged(managed);
            try{managed.SelectConnectedD92("disconnected-selected");throw new Exception("Disconnected selection replaced");}
            catch(InvalidOperationException ex){Check(ex.Message.Contains("所选"),"An explicit disconnected target is never silently replaced by another connected D92");}
            var original=new ThemeProject{Name="original"};original.Landscape.Layers.Add(new(){Kind=ThemeElementKind.DigitalClock,ShowSeconds=true,Width=600,Height=200});
            var originalBytes=JsonSerializer.Serialize(original);
            await managed.ApplySourceAsync(original,[card.DeviceId],LayoutAdaptation.Contain,true);
            var actualHome=main.HomeScene;main.PreviewScene(new ClockScene());
            Check(ReferenceEquals(main.HomeScene,actualHome),"Browsing a local scene does not replace the home preview of the running managed device");
            var draft=original.Clone();draft.Name="temporary";
            var restore=await managed.PreviewTemporaryAsync(draft,card,LayoutAdaptation.Contain,null);main.SetDevicePreview(draft.Name,restore);
            Check(main.HasDevicePreview && main.DevicePreviewLabel.Contains("未保存") && accepted==1 && card.AppliedTheme=="temporary","Temporary device preview is labelled explicitly and does not record formal usage or startup content");
            var window=new MainWindow{DataContext=main,Width=1320,Height=900,ShowInTaskbar=false,ShowActivated=false,Left=-20000,Top=-20000};
            try
            {
                window.Show();await Task.Delay(80);window.UpdateLayout();
                Check(Children<Button>(window).Any(b=>Equals(b.Content,"结束预览并恢复")&&b.IsVisible),"Actual main window exposes an end-preview action beside unified output feedback");
                Capture(window,"output-experience-home.png",96);
                main.SelectedPage=AppPage.Settings;window.Width=1000;window.Height=680;await Task.Delay(80);window.UpdateLayout();
                Check(Children<CheckBox>(window).Any(c=>Equals(c.Content,"启动时恢复上次 D92 内容")&&c.IsVisible),"Compact settings window exposes real opt-in resident and restoration controls");
                Capture(window,"output-experience-settings-125.png",120);
            }
            finally{window.Close();}
            draft.Name="temporary revised";restore=await managed.PreviewTemporaryAsync(draft,card,LayoutAdaptation.Contain,null);main.SetDevicePreview(draft.Name,restore);
            await main.EndDevicePreviewAsync();
            Check(!main.HasDevicePreview && card.AppliedTheme=="original" && JsonSerializer.Serialize(original)==originalBytes,"Ending repeatedly updated preview restores the original managed theme without changing source bytes");
            restore=await managed.PreviewTemporaryAsync(draft,card,LayoutAdaptation.Contain,null);main.SetDevicePreview(draft.Name,restore);
            var formal=original.Clone();formal.Name="new formal";await managed.ApplySourceAsync(formal,[card.DeviceId],LayoutAdaptation.Contain,true);
            try{await restore();throw new Exception("New content overwritten");}catch(InvalidOperationException){}
            Check(card.AppliedTheme=="new formal"&&!main.HasDevicePreview,"Formal content selection clears temporary mode and stale restore cannot overwrite the newer session");
            await sessions.StopAsync(card.DeviceId);managed.Synchronize();
            var legacy=true;var legacyRestored=false;managed.HasLegacyOwnedOutput=_=>legacy;managed.ReleaseLegacyOwnedOutputAsync=_=>{legacy=false;return Task.FromResult(true);};
            restore=await managed.PreviewTemporaryAsync(draft,card,LayoutAdaptation.Contain,()=>{legacyRestored=true;return Task.CompletedTask;});
            await restore();
            Check(legacyRestored && !card.IsActive,"Ending a preview releases its managed session before restoring the previous legacy output");
            try{await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new("different-device","Image","missing.png"));throw new Exception("Wrong identity restored");}
            catch(InvalidOperationException){Check(!card.IsActive,"Startup restore never substitutes a different connected D92 for the recorded identity");}
            try{await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new("preview-serial","Image","missing.png"));throw new Exception("Missing image restored");}
            catch(InvalidOperationException){Check(!card.IsActive,"Missing startup material is rejected before establishing an output session");}
            var imagePath=Path.Combine(root,"restore-image.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(4,1,96,96,PixelFormats.Bgr32,null,new byte[16],16)));using(var file=File.Create(imagePath))png.Save(file);
            var formalBeforeRestore=accepted;
            await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,managed,new("preview-serial","Image",imagePath));
            Check(card.IsActive && main.HomeScene is ThemeScene restoredImage && restoredImage.Children.Count==1 && accepted==formalBeforeRestore,"Startup restoration loads actual image material through the independent device renderer without rewriting formal history");
            await sessions.StopAsync(card.DeviceId);managed.Synchronize();

            var large=new ThemeProject();for(var i=0;i<30;i++)large.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Text,Text="D92 "+i,X=i*35,Y=i%3*120,Width=220,Height=100,FontSize=42});
            using var scene=new ThemeScene(large);using var renderer=new SceneJpegRenderer();var timings=new List<double>();
            for(var i=0;i<24;i++){var watch=System.Diagnostics.Stopwatch.StartNew();scene.Tick(DateTimeOffset.Now);var jpeg=renderer.Render(scene,0);timings.Add(watch.Elapsed.TotalMilliseconds);if(jpeg.Length<1000)throw new InvalidOperationException("Performance frame did not encode");await Dispatcher.Yield(DispatcherPriority.Background);}
            var ordered=timings.Order().ToArray();Console.WriteLine($"METRIC: 30 layers, 1920x480, frames={timings.Count}, JPEG average={timings.Average():F2}ms p95={ordered[(int)(ordered.Length*.95)]:F2}ms max={ordered[^1]:F2}ms; UI max dispatch delay={main.MaxUiDelayMilliseconds:F1}ms; transition={main.Output.LastTransitionMilliseconds:F2}ms.");
            Check(RenderPerformance.Snapshot.Frames>=24 && main.MaxUiDelayMilliseconds>=0,"Bounded render counters and UI responsiveness metrics are available without retaining image frames");
            await managed.PrepareForCloseAsync();
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Driver:IOutputSessionDriver
    {public async Task RunAsync(Action<OutputActualState> report,CancellationToken ct){report(OutputActualState.Streaming);await Task.Delay(Timeout.InfiniteTimeSpan,ct);}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
    private static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T result)yield return result;foreach(var nested in Children<T>(child))yield return nested;}}
    private static void Capture(Window window,string name,double dpi)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi/96),(int)Math.Ceiling(window.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(AppContext.BaseDirectory,name));encoder.Save(file);
    }
}
