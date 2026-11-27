using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class UserWorkflowTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var root=Path.Combine(Path.GetTempPath(),"D92-workflow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source");Directory.CreateDirectory(Path.Combine(source,"Library"));Directory.CreateDirectory(Path.Combine(source,"Themes"));
            var image=Path.Combine(root,"original.png");await File.WriteAllBytesAsync(image,[1,2,3,4]);
            var id=Guid.NewGuid();var asset=new WallpaperAsset{Id=id,Name="portable",Path=image,Category="自然",Subcategory="山",Favorite=true};
            var library=new WallpaperLibrary{Assets=[asset],Playlist=[id],CarouselPresets=[new(){Name="portable preset",Playlist=[id]}]};
            await File.WriteAllTextAsync(Path.Combine(source,"Library","library.json"),JsonSerializer.Serialize(library));
            var project=new ThemeProject{Name="portable source"};project.Landscape.Layers.Add(new(){Kind=ThemeElementKind.Image,Path=image});project.Portrait.Layers.Add(new(){Kind=ThemeElementKind.Image,Path=Path.Combine(root,"missing.png")});
            await new ThemeProjectStore(Path.Combine(source,"Themes")).SaveAsync(project);
            await File.WriteAllTextAsync(Path.Combine(source,"settings.json"),"{\"SchemaVersion\":1,\"LaunchAtLogin\":true,\"RestoreD92OnStartup\":true,\"Password\":\"MUST-NOT-EXPORT\"}");
            await File.WriteAllTextAsync(Path.Combine(source,"credentials.json"),"secret");await File.WriteAllTextAsync(Path.Combine(source,"Themes","usage.json"),JsonSerializer.Serialize(new[]{new ThemeUsage(project.Id,null,true)}));
            var archive=Path.Combine(root,"content.d92backup");var exported=await UserDataBackup.ExportAsync(source,archive);
            Check(exported.Missing.Count==1 && exported.Files.Count(f=>f.Path.StartsWith("Assets/"))==1,"Backup deduplicates referenced media and explicitly reports missing source assets");
            using(var zip=ZipFile.OpenRead(archive)){Check(zip.GetEntry("credentials.json") is null,"Backup excludes credentials and unrelated user files");using var reader=new StreamReader(zip.GetEntry("settings.json")!.Open());Check(!(await reader.ReadToEndAsync()).Contains("MUST-NOT-EXPORT"),"Unknown secret settings are not serialized into a portable backup");}
            var target=Path.Combine(root,"target");var result=await UserDataBackup.RestoreAsync(archive,target);
            var restored=JsonSerializer.Deserialize<WallpaperLibrary>(await File.ReadAllTextAsync(Path.Combine(target,"Library","library.json")))!;
            Check(result.Missing.Count==1 && restored.Assets.Single().Category=="自然" && restored.Assets.Single().Favorite && File.Exists(restored.Assets.Single().Path),"Backup restores actual portable media, identity, categories and favorites");
            var restoredProject=await new ThemeProjectStore(Path.Combine(target,"Themes")).ReadSavedAsync(project.Id);
            Check(restoredProject.Landscape.Layers[0].Path==restored.Assets[0].Path && !File.Exists(restoredProject.Portrait.Layers[0].Path) && !restoredProject.Portrait.Layers[0].Path.Contains(root+"\\missing"),"Theme references are rewritten to restored assets and missing files never bind to a source-machine path");
            var restoredSettings=JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(Path.Combine(target,"settings.json")))!;
            Check(!restoredSettings.LaunchAtLogin&&!restoredSettings.RestoreD92OnStartup&&!restoredSettings.AutoDisplayOnStartup,"Restoring onto a fresh machine does not enable device output or login registration");
            var settingsFile=Path.Combine(target,"settings.json");await File.WriteAllTextAsync(settingsFile,"{\"HomePortrait\":true}");var settingsBefore=await File.ReadAllBytesAsync(settingsFile);
            restored.Assets.Add(new(){Name="existing",Path="existing.png"});await File.WriteAllTextAsync(Path.Combine(target,"Library","library.json"),JsonSerializer.Serialize(restored));
            var inspection=await UserDataBackup.InspectAsync(archive,target);Check(inspection.Conflicts>0,"Restore inspection reports conflicts before changing user data");
            await UserDataBackup.RestoreAsync(archive,target);
            Check((await File.ReadAllBytesAsync(settingsFile)).SequenceEqual(settingsBefore) && JsonSerializer.Deserialize<WallpaperLibrary>(await File.ReadAllTextAsync(Path.Combine(target,"Library","library.json")))!.Assets.Count==2,"Repeated restore preserves existing settings and merges catalog assets without duplication");
            var unsafeZip=Path.Combine(root,"unsafe.zip");using(var zip=ZipFile.Open(unsafeZip,ZipArchiveMode.Create)){var entry=zip.CreateEntry("../escape.txt");using(var writer=new StreamWriter(entry.Open()))writer.Write("escape");using var meta=new StreamWriter(zip.CreateEntry("backup.json").Open());meta.Write(JsonSerializer.Serialize(new BackupManifest(1,DateTimeOffset.Now,[new("../escape.txt",6,Convert.ToHexString(SHA256.HashData("escape"u8)))],[])));}
            await Reject(()=>UserDataBackup.RestoreAsync(unsafeZip,target),"Backup rejects traversal before writing any restored file");
            var damaged=Path.Combine(root,"damaged.d92backup");File.Copy(archive,damaged);using(var zip=ZipFile.Open(damaged,ZipArchiveMode.Update)){var entry=zip.Entries.First(e=>e.FullName.StartsWith("Assets/"));var name=entry.FullName;entry.Delete();using var stream=zip.CreateEntry(name).Open();stream.Write([9,9,9,9]);}
            await Reject(()=>UserDataBackup.RestoreAsync(damaged,target),"Modified backup media fails its hash check before merge");
            var brokenTarget=Path.Combine(root,"broken-target");Directory.CreateDirectory(Path.Combine(brokenTarget,"Library"));await File.WriteAllTextAsync(Path.Combine(brokenTarget,"Library","library.json"),"[]");
            await Reject(()=>UserDataBackup.RestoreAsync(archive,brokenTarget),"Invalid existing catalog aborts the merge instead of replacing user metadata");
            Check(Directory.GetFiles(brokenTarget,"*",SearchOption.AllDirectories).Length==1 && await File.ReadAllTextAsync(Path.Combine(brokenTarget,"Library","library.json"))=="[]","Failed restore rolls back newly added files and preserves the original catalog");
            Check(LocalUpdatePackage.CompareVersions("0.1.0-preview.20261027.10","0.1.0-preview.20261027.2")>0 && LocalUpdatePackage.CompareVersions("0.1.0","0.1.0-preview.9")>0,"Update comparison handles numeric preview components and stable releases");
            var update=Path.Combine(root,"update.zip");var payload=new byte[]{4,5,6};using(var zip=ZipFile.Open(update,ZipArchiveMode.Create))
            {using(var stream=zip.CreateEntry("app/DeskDisplay.App.exe").Open())stream.Write(payload);using(var writer=new StreamWriter(zip.CreateEntry("release.json").Open()))writer.Write(JsonSerializer.Serialize(new LocalRelease("D92 Studio","0.1.0-preview.20261027.1","win-x64",true)));using(var writer=new StreamWriter(zip.CreateEntry("files.json").Open()))writer.Write(JsonSerializer.Serialize(new[]{new ReleasePayload("DeskDisplay.App.exe",Convert.ToHexString(SHA256.HashData(payload)),payload.Length)}));}
            var release=await LocalUpdatePackage.InspectAsync(update,"0.1.0-preview.20261026.2");Check(release.Version.EndsWith("20261027.1"),"Local update accepts a newer correctly hashed compatible package without executing it");
            await Reject(()=>LocalUpdatePackage.InspectAsync(update,"0.1.0-preview.20261027.2"),"Local update prevents installing an older preview");
            using(var zip=ZipFile.Open(update,ZipArchiveMode.Update)){using var writer=new StreamWriter(zip.CreateEntry("app/unlisted.exe").Open());writer.Write("bad");}
            await Reject(()=>LocalUpdatePackage.InspectAsync(update,"0.1.0-preview.20261026.2"),"Local update rejects unlisted payload files");
        }
        finally{Directory.Delete(root,true);}
    }
    private static async Task Reject(Func<Task> action,string label){try{await action();}catch(Exception e)when(e is InvalidDataException or ArgumentException or JsonException){Check(true,label);return;}throw new InvalidOperationException(label);}
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
}
