using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using DeskDisplay.Display.Views;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;
internal static class ClockComponentTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root = Path.Combine(Path.GetTempPath(),"D92-clock-components-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var displays = new Displays(); using var devices = new DevicesViewModel(displays,new TargetDisplayService(new(),new Settings(),logger),logger);
            using var main = new MainViewModel(devices,new ClockScene()); var store = new ThemeProjectStore(root);
            var editor = new CreatorViewModel(main,logger,store); main.SetCreator(editor); main.SelectedPage=AppPage.Creator;
            await editor.InitializeAsync(); await editor.OpenProjectAsync(new ThemeProject());
            editor.Add(ThemeElementKind.Text); await editor.PendingOperation; editor.SelectedLayer!.X=80;editor.SelectedLayer.Y=30;editor.SelectedLayer.Width=600;editor.SelectedLayer.Height=90;editor.SelectedLayer.FontSize=40;editor.SelectedLayer.Text="当前主题内容保留"; var existing = editor.SelectedLayer!.Id; var background=editor.CanvasBackground;
            Check(editor.ClockPresetCards.Count == 10 && editor.ClockPresetCards.All(c=>c.Cover.IsFrozen),"Clock panel exposes ten thumbnails rendered by the real scene renderer");
            Console.WriteLine("FONT: "+string.Join(", ",Fonts.SystemFontFamilies.Where(f=>f.Source.Contains("Segoe")||f.Source.Contains("Bahnschrift")).Select(f=>f.Source)));
            using var renderer = new SceneJpegRenderer(); var images = new HashSet<string>();
            foreach(var card in editor.ClockPresetCards)
            {
                editor.AddClockPresetCommand.Execute(card); await editor.PendingOperation; var layer=editor.SelectedLayer!;
                Check(editor.Layers.Count==2 && editor.Layers[0].Id==layer.Id && editor.Project.ActiveLayout.Layers.Any(l=>l.Id==existing) && editor.CanvasBackground==background,$"{card.Name} adds one topmost clock without replacing the existing theme");
                var size=ThemeTextMetrics.MeasureClock(layer);
                Check(layer.Width>=size.Width && layer.Height>=size.Height && layer.Width<editor.CanvasWidth*.75,$"{card.Name} has compact measured editable bounds");
                var jpeg=renderer.Render(editor.Scene,0); images.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jpeg)));
                Check(jpeg.Length>1000 && layer.GetText(DateTimeOffset.Now)!=layer.GetText(DateTimeOffset.Now.AddMinutes(1)),$"{card.Name} renders JPEG output and uses changing clock data");
                var bytes=JsonSerializer.Serialize(editor.Project); await editor.SaveClockStyleAsync();
                Check(JsonSerializer.Serialize(editor.Project)==bytes,$"{card.Name} saves a reusable component without changing source project bytes");
                var project=JsonSerializer.Deserialize<ThemeProject>(bytes)!; project.Validate();
                Check(project.ActiveLayout.Layers.Single(l=>l.Id==layer.Id).ClockAppearance==layer.ClockAppearance,$"{card.Name} face configuration survives source serialization");
                editor.UndoCommand.Execute(null); await editor.PendingOperation;
                Check(editor.Project.ActiveLayout.Layers.Single().Id==existing,$"{card.Name} insertion is one undo transaction");
            }
            Check(images.Count==10,"All ten clock variants produce different real output frames");
            editor.AddClockPresetCommand.Execute(editor.ClockPresetCards[0]);await editor.PendingOperation;
            editor.ShowSeconds=false;editor.FitClockContentCommand.Execute(null);
            var compact=editor.SelectedLayer!;var compactId=compact.Id;
            var oldWidth=compact.Width;var oldHeight=compact.Height;var oldX=compact.X;var oldY=compact.Y;
            editor.ShowSeconds=true;
            var expanded=ThemeTextMetrics.MeasureClock(editor.SelectedLayer!);
            Check(editor.SelectedLayer!.Width>=expanded.Width && editor.SelectedLayer.Height>=expanded.Height && Math.Abs(editor.SelectedLayer.X+editor.SelectedLayer.Width/2-oldX-oldWidth/2)<.01,"Enabling clock seconds grows measured bounds without moving the visual centre");
            editor.UndoCommand.Execute(null);await editor.PendingOperation;
            compact=editor.Project.ActiveLayout.Layers.Single(l=>l.Id==compactId);
            Check(!compact.ShowSeconds && compact.Width==oldWidth && compact.Height==oldHeight && compact.X==oldX && compact.Y==oldY,"Seconds toggle and bounds growth undo together");
            editor.UndoCommand.Execute(null);await editor.PendingOperation;
            // Return to the pre-fixture project without changing the saved personal style count.
            await editor.OpenProjectAsync(new ThemeProject());editor.Add(ThemeElementKind.Text);await editor.PendingOperation;
            var saved=await new ThemeProjectStore(root).ListClockStylesAsync();
            Check(saved.Count==10 && saved.Any(s=>s.Layer.ClockAppearance==DigitalClockAppearance.SevenSegment),"Personal clock styles persist across store instances including vector faces");
            var style=saved.First(s=>s.Layer.ClockAppearance==DigitalClockAppearance.Stacked);
            editor.AddSavedClockStyleCommand.Execute(style); await editor.PendingOperation;
            Check(editor.SelectedLayer!.Id!=style.Layer.Id && editor.SelectedLayer.ClockAppearance==DigitalClockAppearance.Stacked && editor.Layers.Count==2,"Personal style creates a new independent editable clock");
            editor.Locked=true; Check(!editor.SaveClockStyleCommand.CanExecute(null),"Locked clock cannot be modified through the save-style command"); editor.Locked=false;
            await store.DeleteClockStyleAsync(style.Id); await editor.RefreshClockStylesAsync();
            Check(editor.SavedClockStyles.Count==9 && editor.Layers.Count==2,"Removing a saved style preserves existing canvas layers");
            await File.WriteAllTextAsync(Path.Combine(root,"ClockStyles","corrupt.clock.json"),"broken");
            Check((await store.ListClockStylesAsync()).Count==9 && File.Exists(Path.Combine(root,"ClockStyles","corrupt.clock.json")),"Corrupt clock presets are preserved and do not prevent loading valid styles");
            editor.SelectedMaterial=editor.ResolveMaterialChoice("时钟");
            var shell=new MainWindow{DataContext=main,Width=1320,Height=900,ShowInTaskbar=false,ShowActivated=false,Left=-20000,Top=-20000}; shell.Show(); await Task.Delay(80); shell.UpdateLayout();
            Check(Children<ItemsControl>(shell).Any(i=>ReferenceEquals(i.ItemsSource,editor.ClockPresetCards)),"Actual editor clock panel binds to component presets rather than theme templates");
            Check(Children<Button>(shell).Any(b=>b.Name=="QuickDevicePreview" && Equals(b.Content,"设备预览")),"Actual editor exposes a direct device preview action beside save and finish");
            var bitmap=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(shell); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(AppContext.BaseDirectory,"clock-component-editor.png")))encoder.Save(stream);
            var prompt = editor.Project.Clone(); prompt.ActiveLayout.Layers[0].ClockAppearance=(DigitalClockAppearance)999;
            try { prompt.Validate(); throw new Exception("invalid face accepted"); } catch(InvalidDataException){}
            Check(new ThemeLayer().ClockAppearance==DigitalClockAppearance.Standard,"Legacy layers retain the original clock face by default");
            editor.ConfirmLeave=_=>CreatorLeaveChoice.Discard; shell.Close();
        }
        finally { if(Directory.Exists(root))Directory.Delete(root,true); }
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static IEnumerable<T> Children<T>(DependencyObject p) where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T t)yield return t;foreach(var n in Children<T>(c))yield return n;}}
    private sealed class Settings:ISettingsService{public Task<AppSettings> LoadAsync()=>Task.FromResult(new AppSettings());public Task<bool> SaveAsync(AppSettings s)=>Task.FromResult(true);}
    private sealed class Displays:IDisplayManager{public event EventHandler? DisplaysChanged{add{}remove{}}public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<DisplayInfo>>([]);public void Dispose(){}}
}


