using System.Text.Json;

namespace DeskDisplay.Library;

public sealed record ContentRef(string Kind,Guid Id);
public sealed record MixedEntry(Guid EntryId,ContentRef Content,int Seconds=30,bool PlayToEnd=false);
public sealed record MixedList(int Version,List<MixedEntry> Items,bool Shuffle=false);
public sealed class ContentListStore(string root)
{
    private readonly SemaphoreSlim _gate=new(1,1);
    public async Task<T> ReadAsync<T>(string name,T fallback)
    {
        var path=Path.Combine(root,name);if(!File.Exists(path))return fallback;
        if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("内容列表过大。");
        return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path))??throw new InvalidDataException("内容列表为空。");
    }
    public async Task SaveAsync<T>(string name,T value)
    {
        var json=JsonSerializer.Serialize(value);await _gate.WaitAsync();var temp=Path.Combine(root,name+"."+Guid.NewGuid().ToString("N")+".tmp");
        try{Directory.CreateDirectory(root);await File.WriteAllTextAsync(temp,json);File.Move(temp,Path.Combine(root,name),true);}
        finally{if(File.Exists(temp))File.Delete(temp);_gate.Release();}
    }
    public static void Validate(ContentRef value){if(value.Id==Guid.Empty||value.Kind is not ("Wallpaper" or "Theme"))throw new InvalidDataException("资源引用无效。");}
}
