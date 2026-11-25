using System.Text.Json;
namespace MiraBox.Core;
public sealed record FlipClockSettings(bool TwelveHour = false)
{
    public string Display(DateTimeOffset localTime) =>
        localTime.ToString(TwelveHour ? "hh:mm" : "HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string Period(DateTimeOffset localTime) => TwelveHour ? localTime.Hour < 12 ? "上午" : "下午" : "";
}

public sealed record ThemeLayout(Size Size, string Text = "10:24", string Color = "#FFFFFF", string Style = "ink",
    FlipClockSettings? FlipClock = null);
public sealed record ThemeProject(string ResourceId,List<ThemeLayout> Layouts,string? BackgroundFile=null,string? BackgroundName=null,
    JsonElement? EditorData=null, CraftPluginReference? CraftPlugin=null);
public sealed record CraftPluginReference(string PluginId, bool Hour12 = false);
public sealed record RecentUse(string ResourceId,DateTimeOffset At);
public sealed record TrashReference(string PlaylistId,int Position,PlaylistItem Item);
public sealed record TrashEntry(LibraryEntry Entry,DateTimeOffset DeletedAt,List<TrashReference> References,ThemeProject? Theme);
public sealed record DeviceSnapshot(string Id,Size NativeSize,int Rotation,bool Online,bool Running,string? Content,List<string> History,Dictionary<int,string> DirectionHistory,string Fit,string? Name=null,
    bool? RunningBeforeDisconnect = null, bool OfflineModified = false);
public sealed record HardwareOutputSnapshot(string Serial, string? BaseResourceId, string? VideoResourceId = null);
public sealed record SessionSnapshot(int Version,int Scenario,List<LibraryEntry> Library,List<DeviceSnapshot> Devices,
    List<PlaylistSnapshot> Playlists,List<string> Playing,List<TrashEntry> Trash,List<ThemeProject> Themes,List<RecentUse> Recent,
    List<ScheduleRule>? Schedules = null, List<ScheduleExecution>? ScheduleHistory = null, HardwareOutputSnapshot? HardwareOutput = null,
    HardwareOutputSnapshot? StoppedHardwareOutput = null, List<HardwareOutputSnapshot>? HardwareOutputs = null,
    List<HardwareOutputSnapshot>? StoppedHardwareOutputs = null);
public sealed partial class Workspace
{
    public void PrepareStartupOutput(bool resume, Func<Resource, bool> available)
    {
        foreach (var device in Devices.Values)
        {
            var canResume = resume && device.Content is { } id && Resources.TryGetValue(id, out var resource) &&
                resource.Local && BestLayout(resource, device.Canvas) != null && available(resource);
            if (!canResume)
            {
                device.Running = false;
                device.RunningBeforeDisconnect = false;
            }
            foreach (var list in Playlists.Where(list => list.DeviceId == device.Id))
                if (!canResume || !device.Running || list.Items.ElementAtOrDefault(list.Index)?.ResourceId != device.Content)
                    list.Pause();
        }
        foreach (var list in Playlists.Where(list => !Devices.ContainsKey(list.DeviceId))) list.Pause();
    }

    public List<TrashEntry> Trash { get; }=[];
    public Dictionary<string,ThemeProject> Themes { get; }=[];
    public List<RecentUse> Recent { get; }=[];
    public void NoteUse(string id){Recent.RemoveAll(x=>x.ResourceId==id);Recent.Insert(0,new(id,DateTimeOffset.UtcNow));if(Recent.Count>60)Recent.RemoveRange(60,Recent.Count-60);}
    public bool RecordHardwareUse(string resourceId)
    {
        if (!Resources.TryGetValue(resourceId, out var resource) || !resource.Local) return false;
        usageLibrary?.RecordUse(resourceId);
        NoteUse(resourceId);
        return true;
    }
    public string? DirectionCandidate(string deviceId,int rotation)
    {
        var device = Devices[deviceId];
        var probe = new Device(deviceId, device.NativeSize) { Rotation = rotation };
        if (device.Content == null || Eligible(device.Content, probe)) return device.Content;
        if (!device.Running) return null;
        return device.DirectionHistory.TryGetValue(rotation % 180 == 0 ? 0 : 90, out var remembered) &&
            Eligible(remembered, probe) ? remembered : null;
    }
    public void Rotate(string deviceId,int rotation,string? replacement=null)
    {
        if(rotation is not (0 or 90 or 180 or 270))throw new ArgumentOutOfRangeException(nameof(rotation));
        var device = Devices[deviceId];
        if (device.Rotation == rotation) return;
        var candidate = replacement ?? DirectionCandidate(deviceId, rotation);
        if (Preview?.DeviceId == deviceId) Preview = null;
        device.Rotation = rotation;
        foreach (var playlist in Playlists.Where(list => list.DeviceId == deviceId)) playlist.Pause();

        if (candidate == null || !Eligible(candidate, device))
        {
            device.Running = false;
            return;
        }
        if (candidate == device.Content) return;

        var running = device.Running;
        SetContent(device, candidate);
        device.Running = running;
        device.Fit = "contain";
    }
    public void RotateAndUse(string deviceId, int rotation, string resourceId, Playlist? resume = null)
    {
        if (rotation is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotation));
        var device = Devices[deviceId];
        if (device.Rotation == rotation) return;
        var probe = new Device(deviceId, device.NativeSize) { Rotation = rotation };
        if (!Eligible(resourceId, probe))
            throw new InvalidOperationException("替代内容已不可用，请重新选择。");

        var index = -1;
        if (resume != null)
        {
            index = resume.Items.ToList().FindIndex(item => item.ResourceId == resourceId);
            if (!Playlists.Contains(resume) || !resume.Playing ||
                resume.DeviceId != deviceId || index < 0)
                throw new InvalidOperationException("轮播状态已变化，请重新选择。");
        }

        Rotate(deviceId, rotation, resourceId);
        if (resume != null)
        {
            resume.Index = index;
            resume.Remaining = Duration(resume.Items[index]);
            resume.Playing = true;
            device.Running = true;
        }
        else
        {
            device.Running = true;
        }
        NoteUse(resourceId);
    }
    public void DeleteToTrash(string id,LibraryStore library,DateTimeOffset now)
    {
        var entry=library.Entries.FirstOrDefault(x=>x.Resource.Id==id);if(entry is null||!entry.Resource.Local)return;
        Preview=null;
        var refs=Playlists.Select(p=>(p,index:p.Items.ToList().FindIndex(x=>x.ResourceId==id)))
            .Where(x=>x.index>=0).Select(x=>new TrashReference(x.p.Id,x.index,x.p.Items[x.index])).ToList();
        PurgeTrash(now);
        var earlierCopies = Trash.Where(copy => copy.Entry.Resource.Id == id)
            .OrderByDescending(copy => copy.DeletedAt).ToArray();
        foreach (var reference in earlierCopies.SelectMany(copy => copy.References))
            if (refs.All(existing => existing.PlaylistId != reference.PlaylistId)) refs.Add(reference);
        Trash.RemoveAll(copy => copy.Entry.Resource.Id == id);
        Trash.Add(new(entry,now,refs,Themes.GetValueOrDefault(id)));
        foreach(var p in Playlists)p.Remove(id);
        if(entry.Online||entry.Downloaded) {var online=entry with{Resource=entry.Resource with{Local=false},Favorite=false,Downloaded=false};library.Entries[library.Entries.IndexOf(entry)]=online;Resources[id]=online.Resource;}
        else {library.Entries.Remove(entry);Resources.Remove(id);}
        Themes.Remove(id);Recent.RemoveAll(x=>x.ResourceId==id);
        foreach(var d in Devices.Values)
        {
            if(d.Content==id){var run=d.Running;var fallback=d.History.FirstOrDefault(h=>h!=id&&Eligible(h,d));if(fallback!=null){SetContent(d,fallback);d.Running=run&&d.Online;d.Fit="contain";}else{d.Content=null;d.Running=false;}foreach(var p in Playlists.Where(x=>x.DeviceId==d.Id))p.Pause();}
            d.History.RemoveAll(x=>x==id);foreach(var key in d.DirectionHistory.Where(x=>x.Value==id).Select(x=>x.Key).ToArray())d.DirectionHistory.Remove(key);
        }
    }
    public void RestoreTrash(string id,LibraryStore library,DateTimeOffset now)
    {
        PurgeTrash(now);var entry=Trash.FirstOrDefault(x=>x.Entry.Resource.Id==id);if(entry is null)return;
        var local = library.Entries.FirstOrDefault(current => current.Resource.Id == id && current.Resource.Local);
        var restored = local == null ? entry.Entry : local with
        {
            Favorite = local.Favorite || entry.Entry.Favorite,
            Downloaded = local.Downloaded || entry.Entry.Downloaded
        };
        library.Entries.RemoveAll(x=>x.Resource.Id==id);library.Entries.Add(restored);Resources[id]=restored.Resource;
        if(entry.Theme!=null && !Themes.ContainsKey(id))Themes[id]=entry.Theme;
        foreach(var reference in entry.References)Playlists.FirstOrDefault(p=>p.Id==reference.PlaylistId)?.Insert(reference.Position,reference.Item);
        Trash.Remove(entry);
    }
    public void PurgeTrash(DateTimeOffset now)=>Trash.RemoveAll(x=>now-x.DeletedAt>TimeSpan.FromDays(30));
    public void SaveTheme(ThemeProject project,string name,string type,LibraryStore library)
    {
        if(string.IsNullOrWhiteSpace(name)||project.Layouts.Count==0)throw new InvalidOperationException("请输入名称并保留至少一个布局。");
        if(project.Layouts.Select(x=>x.Size).Distinct().Count()!=project.Layouts.Count)throw new InvalidOperationException("布局尺寸重复。");
        var resource=new Resource(project.ResourceId,name.Trim(),project.Layouts.Select(x=>x.Size).ToArray());
        var old=library.Entries.FirstOrDefault(x=>x.Resource.Id==resource.Id);
        library.Entries.RemoveAll(x=>x.Resource.Id==resource.Id);library.Entries.Add(old is null?new(resource,type,Created:true,Added:DateTimeOffset.UtcNow):old with{Resource=resource,Type=type,Created=true});
        Resources[resource.Id]=resource;Themes[resource.Id]=project with{Layouts=project.Layouts.ToList()};
    }
}
public static class SessionStore
{
    public static SessionSnapshot Capture(Workspace w,LibraryStore library,int scenario)=>new(2,scenario,library.Entries.ToList(),
        w.Devices.Values.Select(d=>new DeviceSnapshot(d.Id,d.NativeSize,d.Rotation,d.Online,d.Running,d.Content,d.History.ToList(),new(d.DirectionHistory),d.Fit,d.Name,d.RunningBeforeDisconnect,d.OfflineModified)).ToList(),
        w.Playlists.Select(p=>new PlaylistSnapshot(p.Id,p.Name,p.DeviceId,p.Mood,p.Shuffle,p.Items.ToList(),p.Index,p.Remaining.TotalSeconds,p.LastUsed)).ToList(),
        w.Playlists.Where(p=>p.Playing).Select(p=>p.Id).ToList(),w.Trash.ToList(),w.Themes.Values.ToList(),w.Recent.ToList(),
        w.Schedules.ToList(),w.ScheduleHistory.Take(20).ToList());
    public static void Save(string file,SessionSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);var temp=file+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,snapshot);stream.Flush(true);}File.Move(temp,file,true);
    }
    public static SessionSnapshot? Read(string file){if(!File.Exists(file))return null;var s=JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(file))??throw new InvalidDataException("工作空间无效。");if(s.Version is not (1 or 2))throw new InvalidDataException("工作空间版本不支持。");return s;}
    public static Workspace Restore(SessionSnapshot s,LibraryStore library,IClock? clock=null)
    {
        library.Replace(s.Library);var w=new Workspace(clock,library);foreach(var r in s.Library)w.Resources[r.Resource.Id]=r.Resource;
        foreach(var x in s.Devices){var d=new Device(x.Id,x.NativeSize){Rotation=x.Rotation,Online=x.Online,Running=x.Running,Content=x.Content,Fit=x.Fit,RunningBeforeDisconnect=x.RunningBeforeDisconnect,OfflineModified=x.OfflineModified};d.Rename(x.Name??x.Id);d.History.AddRange(x.History);foreach(var h in x.DirectionHistory)d.DirectionHistory[h.Key]=h.Value;w.Devices.Add(d.Id,d);}
        foreach(var p in s.Playlists)w.Playlists.Add(new(p.DeviceId,p.Items){Id=p.Id,Name=p.Name,Mood=p.Mood,Shuffle=p.Shuffle,LastUsed=p.LastUsed,Index=Math.Clamp(p.Index,0,Math.Max(0,p.Items.Count-1)),Remaining=TimeSpan.FromSeconds(Math.Clamp(p.Remaining,0,86400)),Playing=s.Playing.Contains(p.Id)&&p.Items.Count>0});
        w.Trash.AddRange(s.Trash);foreach(var t in s.Themes)w.Themes[t.ResourceId]=t;w.Recent.AddRange(s.Recent);
        w.Schedules.AddRange(s.Schedules ?? []);w.ScheduleHistory.AddRange((s.ScheduleHistory ?? []).Take(20));return w;
    }
}
