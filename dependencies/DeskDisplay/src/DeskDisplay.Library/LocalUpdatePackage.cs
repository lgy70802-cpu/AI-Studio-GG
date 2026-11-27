using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskDisplay.Library;

public sealed record LocalRelease(string Product,string Version,string Architecture,bool SelfContained);
public sealed record ReleasePayload(string Path,string SHA256,long Bytes);
public static class LocalUpdatePackage
{
    public static int CompareVersions(string left,string right)
    {
        static (Version Core,string? Pre) Parse(string v){var p=v.Split('-',2);if(!Version.TryParse(p[0],out var core))throw new InvalidDataException("版本号无效。");return(core,p.Length>1?p[1]:null);}
        var a=Parse(left);var b=Parse(right);var compare=a.Core.CompareTo(b.Core);if(compare!=0)return compare;
        if(a.Pre is null||b.Pre is null)return a.Pre is null?(b.Pre is null?0:1):-1;
        var x=a.Pre.Split('.');var y=b.Pre.Split('.');for(var i=0;i<Math.Min(x.Length,y.Length);i++){compare=long.TryParse(x[i],out var n)&&long.TryParse(y[i],out var m)?n.CompareTo(m):string.CompareOrdinal(x[i],y[i]);if(compare!=0)return compare;}return x.Length.CompareTo(y.Length);
    }
    public static async Task<LocalRelease> InspectAsync(string archive,string currentVersion)
    {
        using var zip=ZipFile.OpenRead(archive);if(zip.Entries.Count>10000||zip.Entries.Sum(e=>e.Length)>2L*1024*1024*1024)throw new InvalidDataException("更新包过大。");
        foreach(var entry in zip.Entries){var path=entry.FullName.Replace('\\','/').TrimEnd('/');if(path.StartsWith('/')||path.Contains(':')||path.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("更新包目录无效。");}
        var files=zip.Entries.Where(e=>!e.FullName.EndsWith('/')).ToDictionary(e=>e.FullName.Replace('\\','/'),StringComparer.OrdinalIgnoreCase);
        foreach(var path in files.Keys)if(path.StartsWith('/')||path.Contains(':')||path.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("更新包路径无效。");
        if(!files.TryGetValue("release.json",out var releaseEntry)||releaseEntry.Length>10000||!files.TryGetValue("files.json",out var manifestEntry)||manifestEntry.Length>8_000_000)throw new InvalidDataException("缺少更新清单。");
        LocalRelease release;await using(var stream=releaseEntry.Open())release=await JsonSerializer.DeserializeAsync<LocalRelease>(stream)??throw new InvalidDataException("更新信息为空。");
        if(release.Product!="D92 Studio"||release.Architecture!="win-x64"||!release.SelfContained||!Regex.IsMatch(release.Version,@"^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$")||CompareVersions(release.Version,currentVersion)<=0)throw new InvalidDataException("不是适用的更新版本，或版本不高于当前版本。");
        List<ReleasePayload> manifest;await using(var stream=manifestEntry.Open())manifest=await JsonSerializer.DeserializeAsync<List<ReleasePayload>>(stream)??throw new InvalidDataException("文件清单为空。");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in manifest)
        {
            var path="app/"+item.Path.Replace('\\','/');if(!seen.Add(path)||!files.TryGetValue(path,out var entry)||entry.Length!=item.Bytes)throw new InvalidDataException("更新文件数量或长度不符。");
            await using var stream=entry.Open();if(!string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(stream)),item.SHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包校验失败。");
        }
        if(!seen.Contains("app/DeskDisplay.App.exe")||files.Keys.Any(p=>p.StartsWith("app/",StringComparison.OrdinalIgnoreCase)&&!seen.Contains(p)))throw new InvalidDataException("更新包包含未声明的程序文件。");
        if(files.Keys.Any(p=>!p.StartsWith("app/",StringComparison.OrdinalIgnoreCase)&&p is not ("release.json" or "files.json" or "Install.ps1" or "Install-Staged.ps1" or "Uninstall.ps1" or "README.txt")))throw new InvalidDataException("更新包包含额外文件。");
        return release;
    }
    public static async Task<string> StageAsync(string archive,string currentVersion,string directory)
    {
        await InspectAsync(archive,currentVersion);
        var destination=Path.Combine(directory,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        // Fresh private directory; extraction is performed only after validating every entry.
        ZipFile.ExtractToDirectory(archive,destination,false);return destination;
    }
}
