using System.Text.Json;
using DeskDisplay.Core.Models;

namespace DeskDisplay.Library;

public sealed record SavedClockStyle(Guid Id, string Name, ThemeLayer Layer);
public sealed partial class ThemeProjectStore
{
    private string ClockStyleDirectory => Path.Combine(_directory, "ClockStyles");
    private static ThemeLayer ClockStyleLayer(ThemeLayer source)
    {
        if (source.Kind != ThemeElementKind.DigitalClock) throw new InvalidDataException("仅数字时钟可保存为时钟样式。");
        var layer = source.Clone(); layer.Id = Guid.NewGuid(); layer.X = layer.Y = layer.Rotation = 0; layer.ZIndex = 0;
        layer.GroupId = null; layer.GroupName = "组合"; layer.Locked = false; layer.Visible = true;
        layer.Path = ""; layer.AssetId = null; layer.CloudResourceId = ""; layer.IsVideo = false;
        var project = new ThemeProject(); project.Landscape.Layers.Add(layer); project.Validate(); return layer;
    }
    public async Task<IReadOnlyList<SavedClockStyle>> ListClockStylesAsync()
    {
        if (!Directory.Exists(ClockStyleDirectory)) return [];
        var result = new List<SavedClockStyle>();
        foreach (var path in Directory.EnumerateFiles(ClockStyleDirectory, "*.clock.json").Take(100))
        {
            try
            {
                if (new FileInfo(path).Length > 100_000) continue;
                var item = JsonSerializer.Deserialize<SavedClockStyle>(await File.ReadAllTextAsync(path));
                if (item is null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 80 || item.Layer is null) continue;
                ClockStyleLayer(item.Layer); result.Add(item);
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException) { Serilog.Log.Warning(ex, "Cannot read clock style {Path}; file preserved", path); }
        }
        return result.OrderBy(i => i.Name).ToArray();
    }
    public async Task SaveClockStyleAsync(string name, ThemeLayer source)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80) throw new InvalidDataException("时钟样式名称需要1至80字。");
        var layer = ClockStyleLayer(source); var item = new SavedClockStyle(Guid.NewGuid(), name.Trim(), layer);
        await _gate.WaitAsync(); string? temp = null;
        try
        {
            Directory.CreateDirectory(ClockStyleDirectory);
            if (Directory.EnumerateFiles(ClockStyleDirectory,"*.clock.json").Count() >= 100) throw new InvalidDataException("最多保存100款个人时钟样式，请先移除不再使用的样式。");
            var path = Path.Combine(ClockStyleDirectory,item.Id.ToString("N")+".clock.json"); temp = path + ".tmp";
            await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(item)); File.Move(temp,path); temp = null;
        }
        finally { if (temp is not null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
    }
    public async Task DeleteClockStyleAsync(Guid id)
    {
        await _gate.WaitAsync();
        try { File.Delete(Path.Combine(ClockStyleDirectory,id.ToString("N")+".clock.json")); }
        finally { _gate.Release(); }
    }
}
