namespace MiraBox.Core;

public readonly record struct Size(int Width, int Height);
public sealed record Resource(string Id, string Name, IReadOnlyList<Size> Layouts, bool Local = true);
public sealed class Device(string id, Size nativeSize)
{
    public string Id { get; } = id;
    public string Name { get; private set; } = id;
    public string Model => NativeSize == new Size(1920,462)?"D92":NativeSize == new Size(1280,480)?"D69":"MiraBox";
    public void Rename(string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>30)throw new ArgumentException("设备名称须为1–30个字符。");Name=name.Trim();}
    public Size NativeSize { get; } = nativeSize;
    public int Rotation { get; set; }
    public Size Canvas => Rotation % 180 == 0 ? NativeSize : new(NativeSize.Height, NativeSize.Width);
    public bool Online { get; internal set; } = true;
    bool running;
    public bool Running
    {
        get => running;
        internal set
        {
            if (!Online && running != value) OfflineModified = true;
            running = value;
        }
    }
    internal bool? RunningBeforeDisconnect { get; set; }
    internal bool OfflineModified { get; set; }
    public string? Content { get; internal set; }
    public List<string> History { get; } = [];
    public Dictionary<int,string> DirectionHistory { get; } = [];
    public string Fit { get; internal set; } = "contain";
}
public sealed record PreviewSession(Guid Token, string DeviceId, string ResourceId, TimeSpan EndsAt);
public sealed record ReconnectResult(string DeviceName, string? ContentName, bool Running, bool UsedOfflineChanges)
{
    public string Message => $"「{DeviceName}」已重新连接" +
        (Running ? $"，正在显示「{ContentName}」" : "，保持停止输出") +
        (UsedOfflineChanges ? "（以离线时的修改为准）。" : "。");
}
public sealed record PlaylistItem(string ResourceId, TimeSpan Duration);
public sealed class Playlist(string deviceId, IReadOnlyList<PlaylistItem> items)
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新建轮播";
    public string Mood { get; set; } = "灵感";
    public bool Shuffle { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
    public string DeviceId { get; private set; } = deviceId;
    public void Assign(string id) { DeviceId=id; Playing=false; }
    public void Pause() => Playing=false;
    public void Move(int from,int to) { if(from<0||to<0||from>=entries.Count||to>=entries.Count)return;var current=entries.ElementAtOrDefault(Index)?.ResourceId;var item=entries[from];entries.RemoveAt(from);entries.Insert(to,item);Index=Math.Max(0,entries.FindIndex(x=>x.ResourceId==current)); }
    public void Remove(string id) { var current=entries.ElementAtOrDefault(Index)?.ResourceId;entries.RemoveAll(x=>x.ResourceId==id);if(current==id){Playing=false;Remaining=TimeSpan.Zero;}Index=Math.Max(0,entries.FindIndex(x=>x.ResourceId==current)); }
    public void SetSeconds(int index, double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        var duration = TimeSpan.FromSeconds(Math.Clamp(seconds, 10, 86400));
        var item = entries[index];
        if (Playing && Index == index)
        {
            var elapsed = Math.Max(0, (item.Duration - Remaining).TotalSeconds);
            Remaining = TimeSpan.FromSeconds(Math.Max(0, duration.TotalSeconds - elapsed));
        }
        entries[index] = item with { Duration = duration };
    }
    private readonly List<PlaylistItem> entries = items.ToList();
    public IReadOnlyList<PlaylistItem> Items => entries.AsReadOnly();
    public void Append(IEnumerable<PlaylistItem> additions)
    { foreach(var item in additions.ToArray()) if(!entries.Any(x=>x.ResourceId==item.ResourceId)) entries.Add(item); }
    public void Insert(int position,PlaylistItem item) { if(entries.Any(x=>x.ResourceId==item.ResourceId))return;var current=entries.ElementAtOrDefault(Index)?.ResourceId;entries.Insert(Math.Clamp(position,0,entries.Count),item);Index=Math.Max(0,entries.FindIndex(x=>x.ResourceId==current)); }
    public int Index { get; internal set; }
    public bool Playing { get; internal set; }
    public TimeSpan Remaining { get; internal set; }
}

/// <summary>Pure logical state; never claims a frame was transmitted to hardware.</summary>
public sealed partial class Workspace
{
    public Dictionary<string, Device> Devices { get; } = [];
    public Dictionary<string, Resource> Resources { get; } = [];
    public List<Playlist> Playlists { get; } = [];
    public PreviewSession? Preview { get; private set; }
    public TimeSpan Clock { get; private set; }
    public static double? Difference(Resource resource, Size canvas) => BestLayout(resource, canvas)?.Loss;

    public static (int Index, double Loss)? BestLayout(Resource resource, Size canvas)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0) return null;
        var target = (double)canvas.Width / canvas.Height;
        (int Index, double Loss)? best = null;
        for (var index = 0; index < resource.Layouts.Count; index++)
        {
            var size = resource.Layouts[index];
            if (size.Width <= 0 || size.Height <= 0) continue;
            if (size == canvas) return (index, 0);
            var ratio = (double)size.Width / size.Height;
            var loss = 1 - Math.Min(ratio, target) / Math.Max(ratio, target);
            if (best == null || loss < best.Value.Loss) best = (index, loss);
        }
        return best;
    }
    private bool Eligible(string resourceId, Device device) => Resources.TryGetValue(resourceId, out var r)
        && r.Local && Difference(r, device.Canvas) is double loss && loss <= .05;
    readonly LibraryStore? usageLibrary;

    private void SetContent(Device device, string resourceId)
    {
        if (device.Content != resourceId) usageLibrary?.RecordUse(resourceId);
        if (device.Content != resourceId && device.Content is string old)
        {
            device.History.Remove(old); device.History.Insert(0, old);
            if (device.History.Count > 100) device.History.RemoveRange(100, device.History.Count - 100);
        }
        if (!device.Online) device.OfflineModified = true;
        device.Content = resourceId; device.Running = true;
        if(Eligible(resourceId,device))device.DirectionHistory[device.Rotation%180==0?0:90]=resourceId;
    }
    public void ApplyDirect(string deviceId, string resourceId, bool preserveOtherPreview = false)
    {
        var device = Devices[deviceId];
        if (!Eligible(resourceId, device)) throw new InvalidOperationException("Requires adaptation flow.");
        if (!preserveOtherPreview || Preview?.DeviceId == deviceId) Preview = null;
        foreach (var p in Playlists.Where(p => p.DeviceId == deviceId)) p.Playing = false;
        SetContent(device, resourceId);
        device.Fit="contain";
    }
    public void ApplyConfirmed(string deviceId, string resourceId, string fit="contain")
    {
        var d = Devices[deviceId]; var r = Resources[resourceId];
        if (!d.Online || !r.Local || Difference(r, d.Canvas) is null)
            throw new InvalidOperationException("Cannot apply to selected device.");
        Preview = null;
        foreach (var p in Playlists.Where(p => p.DeviceId == deviceId)) p.Playing = false;
        SetContent(d, resourceId);
        d.Fit=fit=="cover"?"cover":"contain";
    }
    public void ConfigureDevice(string deviceId,string name,int rotation)
    {
        if(rotation is not (0 or 90 or 180 or 270))throw new ArgumentOutOfRangeException(nameof(rotation));
        var d=Devices[deviceId];d.Rename(name);
        if(d.Rotation==rotation)return;
        if (Preview?.DeviceId == deviceId) Preview = null;
        d.Rotation=rotation;d.Running=false;
        foreach(var p in Playlists.Where(p=>p.DeviceId==deviceId))p.Pause();
    }
    public void ToggleOutput(string deviceId)
    {
        var d = Devices[deviceId];
        if (d.Content is null) return;
        if (Preview?.DeviceId == deviceId) Preview = null;
        d.Running = !d.Running;
        if (!d.Running) foreach (var p in Playlists.Where(p => p.DeviceId == deviceId)) p.Playing = false;
    }
    public Guid BeginPreview(string deviceId, string resourceId, bool mismatchConfirmed = false)
    {
        var device = Devices[deviceId]; var resource = Resources[resourceId];
        if (!device.Online || !resource.Local || Difference(resource, device.Canvas) is not double loss)
            throw new InvalidOperationException("Preview unavailable.");
        if (loss > .05 && !mismatchConfirmed) throw new InvalidOperationException("Confirm mismatch.");
        Preview = new(Guid.NewGuid(), deviceId, resourceId, Clock + TimeSpan.FromSeconds(15));
        return Preview.Token;
    }
    public void EndPreview(Guid token) { if (Preview?.Token == token) Preview = null; }
    public string? VisibleContent(string deviceId) => !Devices[deviceId].Online ? null
        : Preview?.DeviceId == deviceId ? Preview.ResourceId : Devices[deviceId].Content;
    public string VisibleFit(string deviceId) => Preview?.DeviceId==deviceId?"contain":Devices[deviceId].Fit;
    public ReconnectResult? SetOnline(string deviceId, bool online)
    {
        var device = Devices[deviceId];
        if (device.Online == online) return null;
        device.Online = online;
        if (!online)
        {
            device.RunningBeforeDisconnect = device.Running;
            device.OfflineModified = false;
            if (Preview?.DeviceId == deviceId) Preview = null;
            return null;
        }
        var modified = device.OfflineModified;
        if (!modified && device.RunningBeforeDisconnect is { } wasRunning) device.Running = wasRunning;
        device.RunningBeforeDisconnect = null;
        device.OfflineModified = false;
        return new(device.Name, device.Content != null && Resources.TryGetValue(device.Content, out var content)
            ? content.Name : null, device.Running && device.Content != null, modified);
    }
    public void Start(Playlist playlist)
    {
        var device = Devices[playlist.DeviceId];
        if (!device.Online) throw new InvalidOperationException("Device offline.");
        var valid = Enumerable.Range(0, playlist.Items.Count).Where(i => Eligible(playlist.Items[i].ResourceId, device)).ToArray();
        if (valid.Length == 0) throw new InvalidOperationException("No compatible items.");
        if (!Playlists.Contains(playlist)) Playlists.Add(playlist);
        foreach (var p in Playlists.Where(p => p.DeviceId == device.Id)) p.Playing = false;
        if (!valid.Contains(playlist.Index)) { playlist.Index = valid[0];playlist.Remaining=TimeSpan.Zero; }
        if (Preview?.DeviceId == device.Id) Preview = null;
        if (playlist.Remaining <= TimeSpan.Zero) playlist.Remaining = Duration(playlist.Items[playlist.Index]);
        playlist.LastUsed = scheduleClock.GetLocalNow();
        playlist.Playing = true; SetContent(device, playlist.Items[playlist.Index].ResourceId);device.Fit="contain";
    }
    private static TimeSpan Duration(PlaylistItem item) => TimeSpan.FromSeconds(Math.Clamp(item.Duration.TotalSeconds, 10, 86400));
    public void Step(Playlist p,int direction=1)
    {
        if(!Devices.TryGetValue(p.DeviceId,out var d)||!d.Online)return;
        var valid=Enumerable.Range(0,p.Items.Count).Where(i=>Eligible(p.Items[i].ResourceId,d)).ToArray();
        if(valid.Length==0){p.Pause();return;}
        int next;
        if(p.Shuffle){var options=valid.Where(i=>i!=p.Index).ToArray();next=options.Length==0?valid[0]:options[Random.Shared.Next(options.Length)];}
        else next=Enumerable.Range(1,p.Items.Count).Select(n=>(p.Index+Math.Sign(direction)*n+p.Items.Count*2)%p.Items.Count).First(valid.Contains);
        p.Index=next;p.Remaining=Duration(p.Items[next]);SetContent(d,p.Items[next].ResourceId);d.Fit="contain";
    }
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        Clock += elapsed;
        foreach (var p in Playlists.Where(p => p.Playing))
        {
            if(!Devices.TryGetValue(p.DeviceId,out var d)||p.Items.Count==0)continue;
            if (!d.Online || !d.Running || d.Content != p.Items[p.Index].ResourceId) continue;
            p.Remaining -= elapsed;
            if (p.Remaining > TimeSpan.Zero) continue;
            Step(p);
        }
        if (Preview is not null && Clock >= Preview.EndsAt) Preview = null;
    }
}
