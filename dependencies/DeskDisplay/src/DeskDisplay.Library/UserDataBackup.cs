using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeskDisplay.Library;

public sealed record BackupFile(string Path,long Bytes,string Hash);
public sealed record BackupManifest(int Version,DateTimeOffset CreatedAt,List<BackupFile> Files,List<string> Missing);
public sealed record BackupInspection(int Files,int Conflicts,IReadOnlyList<string> Missing);

/// <summary>Portable user content, never credentials, executables, logs or device leases.</summary>
public static class UserDataBackup
{
    private const long MaxTotal=4L*1024*1024*1024;
    private static readonly HashSet<string> Media=new(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".mp4",".gif",".webp"};
    private static readonly HashSet<string> PathKeys=new(StringComparer.OrdinalIgnoreCase){"Path","LastImagePath","LastVideoPath","DashboardImagePath"};
    private static bool Metadata(string path)=>path is "settings.json" or "Library/library.json" or "home-pins.json" or "mixed-playlist.json" || path.StartsWith("Themes/",StringComparison.Ordinal) && !path.Contains("/History/") && !path.Contains("/Drafts/") && (path.EndsWith(".json",StringComparison.Ordinal) || path.EndsWith(".png",StringComparison.Ordinal));
    private static bool Allowed(string path)=>Metadata(path) || path.StartsWith("Assets/",StringComparison.Ordinal) && Media.Contains(System.IO.Path.GetExtension(path));
    private static string Child(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||relative.Contains('\\')||relative.Contains(':')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("备份路径不安全。");
        var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
        if(!full.StartsWith(System.IO.Path.GetFullPath(root)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("备份路径越界。");
        for(var p=System.IO.Path.GetDirectoryName(full);p is not null && p.Length>=System.IO.Path.GetFullPath(root).Length;p=System.IO.Path.GetDirectoryName(p))
            if(Directory.Exists(p)&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("不支持链接目录。");
        if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("不支持链接文件。");
        return full;
    }
    private static void Rewrite(JsonNode? node,Func<string,string> map)
    {
        if(node is JsonObject obj)foreach(var pair in obj.ToArray())
        {
            if(PathKeys.Contains(pair.Key)&&pair.Value is JsonValue value&&value.TryGetValue<string>(out var path)&&!string.IsNullOrWhiteSpace(path))obj[pair.Key]=map(path);
            else Rewrite(pair.Value,map);
        }
        else if(node is JsonArray array)foreach(var child in array)Rewrite(child,map);
    }
    public static async Task<BackupManifest> ExportAsync(string root,string destination)
    {
        root=System.IO.Path.GetFullPath(root);var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);var mediaSources=new Dictionary<string,(string Source,BackupFile File)>();var missing=new List<string>();var sourceMap=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);long total=0;
        void Add(string relative,byte[] bytes){total+=bytes.Length;if(total>MaxTotal||files.Count>=20000)throw new InvalidDataException("备份超过 4 GiB 或文件数限制，请分批导出大型主题素材。");files.Add(relative,bytes);}
        string Map(string source)
        {
            if(sourceMap.TryGetValue(source,out var mapped))return mapped;
            if(!System.IO.Path.IsPathFullyQualified(source)||!File.Exists(source)){missing.Add(source);return source;}
            if(!Media.Contains(System.IO.Path.GetExtension(source)))throw new InvalidDataException("素材格式不受支持："+source);
            var info=new FileInfo(source);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length>MaxTotal-total)throw new InvalidDataException("素材过大或为文件链接。");
            string hash;using(var input=File.OpenRead(source))hash=Convert.ToHexString(SHA256.HashData(input));
            var name="Assets/"+hash.ToLowerInvariant()+System.IO.Path.GetExtension(source).ToLowerInvariant();
            if(!files.ContainsKey(name)){Add(name,[]);total+=info.Length;mediaSources.Add(name,(source,new(name,info.Length,hash)));}
            sourceMap[source]="backup://"+name;return sourceMap[source];
        }
        foreach(var relative in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}).Select(p=>System.IO.Path.GetRelativePath(root,p).Replace('\\','/')).Where(Metadata))
        {
            var path=Child(root,relative);if(new FileInfo(path).Length>16_000_000)throw new InvalidDataException("元数据过大。");var bytes=await File.ReadAllBytesAsync(path);
            if(relative.EndsWith(".json",StringComparison.Ordinal))
            {
                var node=JsonNode.Parse(bytes)??throw new InvalidDataException("空元数据。");
                if(relative=="settings.json")node=JsonSerializer.SerializeToNode(node.Deserialize<DeskDisplay.Core.Models.AppSettings>()??new());
                Rewrite(node,Map);bytes=JsonSerializer.SerializeToUtf8Bytes(node);
            }
            Add(relative,bytes);
        }
        var manifest=new BackupManifest(1,DateTimeOffset.Now,files.Select(f=>mediaSources.TryGetValue(f.Key,out var media)?media.File:new BackupFile(f.Key,f.Value.LongLength,Convert.ToHexString(SHA256.HashData(f.Value)))).ToList(),missing.Distinct().ToList());
        var temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var archive=ZipFile.Open(temp,ZipArchiveMode.Create))
            {
                foreach(var file in files)
                {
                    await using var stream=archive.CreateEntry(file.Key,mediaSources.ContainsKey(file.Key)?CompressionLevel.NoCompression:CompressionLevel.Fastest).Open();
                    if(mediaSources.TryGetValue(file.Key,out var media)){await using var input=File.OpenRead(media.Source);await input.CopyToAsync(stream);}
                    else await stream.WriteAsync(file.Value);
                }
                await using var meta=archive.CreateEntry("backup.json").Open();await JsonSerializer.SerializeAsync(meta,manifest);
            }
            _=await ReadAsync(temp);File.Move(temp,destination,false);return manifest;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static async Task<(BackupManifest Manifest,Dictionary<string,byte[]> Files)> ReadAsync(string archivePath)
    {
        using var zip=ZipFile.OpenRead(archivePath);var entries=zip.Entries.ToDictionary(e=>e.FullName,StringComparer.OrdinalIgnoreCase);
        if(entries.Count>20001||!entries.TryGetValue("backup.json",out var meta)||meta.Length>8_000_000)throw new InvalidDataException("备份清单无效。");
        BackupManifest manifest;await using(var stream=meta.Open())manifest=await JsonSerializer.DeserializeAsync<BackupManifest>(stream)??throw new InvalidDataException("备份为空。");
        if(manifest.Version!=1||manifest.Files is null||manifest.Missing is null||manifest.Files.Count+1!=entries.Count)throw new InvalidDataException("备份版本或文件数量无效。");
        var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long total=0;long metadataBytes=0;
        foreach(var item in manifest.Files)
        {
            _=Child(System.IO.Path.GetTempPath().TrimEnd('\\','/'),item.Path);
            if(!Allowed(item.Path)||item.Bytes<0||(total+=item.Bytes)>MaxTotal||!entries.TryGetValue(item.Path,out var entry)||entry.Length!=item.Bytes||files.ContainsKey(item.Path))throw new InvalidDataException("备份包含无效文件。");
            await using var stream=entry.Open();
            if(item.Path.StartsWith("Assets/",StringComparison.Ordinal))
            {if(!string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(stream)),item.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("备份素材校验失败。");files.Add(item.Path,[]);continue;}
            if(item.Bytes>16_000_000 || (metadataBytes+=item.Bytes)>128_000_000)throw new InvalidDataException("备份元数据过大。");
            using var memory=new MemoryStream();await stream.CopyToAsync(memory);var bytes=memory.ToArray();
            if(!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),item.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("备份文件校验失败。");
            if(Metadata(item.Path)&&item.Path.EndsWith(".json",StringComparison.Ordinal))
            {
                _=JsonNode.Parse(bytes)??throw new InvalidDataException("元数据为空。");
                if(item.Path.EndsWith(".theme.json",StringComparison.Ordinal))(JsonSerializer.Deserialize<DeskDisplay.Core.Models.ThemeProject>(bytes)??throw new InvalidDataException("主题为空。")).Validate();
                if(item.Path=="Library/library.json" && JsonSerializer.Deserialize<DeskDisplay.Core.Models.WallpaperLibrary>(bytes) is not {Version:1,Assets:not null,Playlist:not null,CarouselPresets:not null,History:not null})throw new InvalidDataException("资源库版本无效。");
                if(item.Path.EndsWith("/theme-playlist.json",StringComparison.Ordinal))(JsonSerializer.Deserialize<ThemePlaylistSettings>(bytes)??throw new InvalidDataException("轮播为空。")).Validate();
            }
            files.Add(item.Path,bytes);
        }
        return(manifest,files);
    }
    public static async Task<BackupInspection> InspectAsync(string archive,string root)
    {var data=await ReadAsync(archive);return new(data.Files.Count,data.Files.Keys.Count(p=>File.Exists(Child(root,p))),data.Manifest.Missing);}
    public static async Task<BackupInspection> RestoreAsync(string archive,string root)
    {
        await using var archiveLock=new FileStream(archive,FileMode.Open,FileAccess.Read,FileShare.Read);
        var data=await ReadAsync(archive);using var zip=new ZipArchive(archiveLock,ZipArchiveMode.Read,true);Directory.CreateDirectory(root);var conflicts=0;var added=new List<string>();var originals=new Dictionary<string,byte[]>();
        try
        {
            foreach(var pair in data.Files.OrderBy(f=>Metadata(f.Key)))
            {
                var destination=Child(root,pair.Key);var exists=File.Exists(destination);
                if(exists){conflicts++;if(pair.Key is not ("Library/library.json" or "Themes/usage.json"))continue;}
                if(pair.Key.StartsWith("Assets/",StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                    await using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);added.Add(destination);
                    await using var input=zip.GetEntry(pair.Key)!.Open();await input.CopyToAsync(output);continue;
                }
                var bytes=pair.Value;
                if(pair.Key.EndsWith(".json",StringComparison.Ordinal))
                {
                    var node=JsonNode.Parse(bytes)!;
                    Rewrite(node,p=>
                    {
                        if(p.StartsWith("backup://",StringComparison.Ordinal) && data.Files.ContainsKey(p[9..]) && p[9..].StartsWith("Assets/",StringComparison.Ordinal))return Child(root,p[9..]);
                        if(data.Manifest.Missing.Contains(p))return Child(root,"Missing/"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(p)))+".missing");
                        throw new InvalidDataException("素材引用不在备份清单内。");
                    });
                    if(pair.Key=="settings.json" && node is JsonObject settings){settings["AutoDisplayOnStartup"]=false;settings["RestoreD92OnStartup"]=false;settings["LaunchAtLogin"]=false;settings["UsbOutput"]=null;settings["TargetDisplay"]=null;}
                    bytes=JsonSerializer.SerializeToUtf8Bytes(node);
                }
                if(exists)
                {
                    var original=await File.ReadAllBytesAsync(destination);originals[destination]=original;
                    if(pair.Key=="Library/library.json")
                    {
                        var current=JsonSerializer.Deserialize<DeskDisplay.Core.Models.WallpaperLibrary>(original)??throw new InvalidDataException("现有资源库无效。");
                        var incoming=JsonSerializer.Deserialize<DeskDisplay.Core.Models.WallpaperLibrary>(bytes)??throw new InvalidDataException("导入资源库无效。");
                        current.Assets.AddRange(incoming.Assets.Where(a=>!current.Assets.Any(c=>c.Id==a.Id)));
                        current.CarouselPresets.AddRange(incoming.CarouselPresets.Where(a=>!current.CarouselPresets.Any(c=>c.Id==a.Id)));
                        current.History.AddRange(incoming.History.Where(a=>!current.History.Any(c=>c.WallpaperId==a.WallpaperId)));bytes=JsonSerializer.SerializeToUtf8Bytes(current);
                    }
                    else
                    {
                        var current=JsonSerializer.Deserialize<List<ThemeUsage>>(original)??[];var incoming=JsonSerializer.Deserialize<List<ThemeUsage>>(bytes)??[];
                        current.AddRange(incoming.Where(a=>!current.Any(c=>c.ProjectId==a.ProjectId)));bytes=JsonSerializer.SerializeToUtf8Bytes(current);
                    }
                    var temporary=destination+".restore-"+Guid.NewGuid().ToString("N");
                    try{await File.WriteAllBytesAsync(temporary,bytes);File.Move(temporary,destination,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
                    continue;
                }
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                await using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){added.Add(destination);await output.WriteAsync(bytes);}
            }
            return new(added.Count,conflicts,data.Manifest.Missing);
        }
        catch{foreach(var path in added)File.Delete(path);foreach(var original in originals)await File.WriteAllBytesAsync(original.Key,original.Value);throw;}
    }
}
