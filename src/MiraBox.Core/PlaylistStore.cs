using System.Text.Json;
namespace MiraBox.Core;
public sealed record PlaylistSnapshot(string Id,string Name,string DeviceId,string Mood,bool Shuffle,List<PlaylistItem> Items,int Index,double Remaining,DateTimeOffset? LastUsed=null);
public static class PlaylistStore
{
    public static void Save(string file,IEnumerable<Playlist> lists)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var data=lists.Select(p=>new PlaylistSnapshot(p.Id,p.Name,p.DeviceId,p.Mood,p.Shuffle,p.Items.ToList(),p.Index,p.Remaining.TotalSeconds,p.LastUsed));
        var temp=file+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,data);stream.Flush(true);}File.Move(temp,file,true);
    }
    public static List<Playlist> Load(string file)
    {
        if(!File.Exists(file))return [];
        var data=JsonSerializer.Deserialize<List<PlaylistSnapshot>>(File.ReadAllText(file))??throw new InvalidDataException("轮播列表数据无效。");
        return data.Select(s=>new Playlist(s.DeviceId,s.Items){Id=s.Id,Name=s.Name,Mood=s.Mood,Shuffle=s.Shuffle,LastUsed=s.LastUsed,Index=Math.Clamp(s.Index,0,Math.Max(0,s.Items.Count-1)),Remaining=TimeSpan.FromSeconds(Math.Clamp(s.Remaining,0,86400))}).ToList();
    }
}
