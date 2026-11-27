using System.Security.Cryptography;
using DeskDisplay.Core.Models;
namespace DeskDisplay.Library;
public sealed record ResourceIssue(Guid AssetId,string Name,string Path,string Issue,string Group="");
public static class ResourceAudit
{
    public static async Task<IReadOnlyList<ResourceIssue>> ScanAsync(IReadOnlyList<WallpaperAsset> assets,CancellationToken cancellationToken=default)
    {
        var issues=new List<ResourceIssue>();var available=new List<(WallpaperAsset Asset,long Size)>();
        foreach(var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{if(!File.Exists(asset.Path))issues.Add(new(asset.Id,asset.Name,asset.Path,"文件缺失"));else available.Add((asset,new FileInfo(asset.Path).Length));}
            catch(IOException){issues.Add(new(asset.Id,asset.Name,asset.Path,"文件无法读取"));}
        }
        foreach(var group in available.GroupBy(a=>a.Size).Where(g=>g.Count()>1))
        {
            var hashes=new List<(WallpaperAsset Asset,string Hash)>();
            foreach(var item in group)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try{await using var input=File.OpenRead(item.Asset.Path);hashes.Add((item.Asset,Convert.ToHexString(await SHA256.HashDataAsync(input,cancellationToken))));}
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){issues.Add(new(item.Asset.Id,item.Asset.Name,item.Asset.Path,"文件无法读取"));}
            }
            foreach(var duplicate in hashes.GroupBy(h=>h.Hash).Where(g=>g.Count()>1))foreach(var item in duplicate)issues.Add(new(item.Asset.Id,item.Asset.Name,item.Asset.Path,"内容重复",duplicate.Key[..12]));
        }
        return issues;
    }
    public static IReadOnlyDictionary<Guid,string> FindFolderMatches(IReadOnlyList<WallpaperAsset> assets,string directory)
    {
        var files=Directory.EnumerateFiles(directory,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}).Take(20001).ToArray();if(files.Length>20000)throw new InvalidDataException("文件夹超过20000个文件，请选择更具体的目录。");
        var names=files.GroupBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key!,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
        return assets.Where(a=>!File.Exists(a.Path)).Where(a=>names.TryGetValue(Path.GetFileName(a.Path),out var matches)&&matches.Length==1).ToDictionary(a=>a.Id,a=>names[Path.GetFileName(a.Path)][0]);
    }
}
