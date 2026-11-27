using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiraBox.Core;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    readonly Grid pluginHost = new() { Width = 1, Height = 1, IsHitTestVisible = false };
    readonly Dictionary<string, WoodlandRuntime> pluginOutputs = new();
    Window? pluginPreviewWindow;
    CraftPluginStore CraftStore => new(Path.Combine(dataRoot, "Plugins"));
    const string PluginPrefix = "craft-plugin:";

    CraftPluginEntry FindPlugin(string id) => CraftStore.Entries.FirstOrDefault(p => p.Id == id)
        ?? throw new InvalidOperationException("此插件已缺失，请重新导入。");

    string PluginSettingsPath(CraftPluginEntry entry) =>
        Path.Combine(dataRoot, "Plugins", Path.GetFileName(CraftStore.PackagePath(entry)) + "-settings.json");

    static JsonObject PluginDefaults(CraftPluginEntry entry)
    {
        var settings = JsonNode.Parse(entry.DefaultSettings ?? "{}")!.AsObject();
        if (entry.Id == CraftPluginStore.WoodlandId) settings["hour12"] ??= entry.Hour12;
        return settings;
    }

    static bool EditablePluginSetting(KeyValuePair<string, JsonNode?> field) =>
        field.Key != "ActionGeometry" && field.Value?.GetValueKind() is
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.String or JsonValueKind.Number;

    JsonObject PluginSettings(CraftPluginEntry entry)
    {
        var settings = PluginDefaults(entry);
        var path = PluginSettingsPath(entry);
        if (!File.Exists(path)) return settings;
        var stored = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        foreach (var field in settings.Where(EditablePluginSetting).ToArray())
            if (stored[field.Key] is JsonValue value) settings[field.Key] = value.DeepClone();
        return settings;
    }

    WoodlandRuntime OpenPlugin(Panel panel, CraftPluginEntry entry, SavedDevice? device)
    {
        var width = device?.Model == "D69" ? 1280 : 1920;
        var height = device?.Model == "D69" ? 480 : 462;
        if (device?.Portrait == true) (width, height) = (height, width);
        // Woodland's artwork is designed for a fixed ratio. Fit the result to the target separately.
        if (entry.Id == CraftPluginStore.WoodlandId) (width, height) = (entry.Width, entry.Height);
        return new WoodlandRuntime(panel, entry, CraftStore.PackagePath(entry),
            Path.Combine(dataRoot, "PluginFrames", entry.PackageDirectory, "unused.image"), width, height,
            entry.Hour12, cacheFrames: false, configuredSettings: PluginSettings(entry));
    }

    static BitmapSource FitPlugin(BitmapSource frame, SavedDevice? device)
    {
        int width = device?.Model == "D69" ? 1280 : 1920;
        int height = device?.Model == "D69" ? 480 : 462;
        if (device?.Portrait == true) (width, height) = (height, width);
        var scale = Math.Min((double)width / frame.PixelWidth, (double)height / frame.PixelHeight);
        var imageWidth = frame.PixelWidth * scale;
        var imageHeight = frame.PixelHeight * scale;
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            context.DrawImage(frame, new Rect((width - imageWidth) / 2, (height - imageHeight) / 2, imageWidth, imageHeight));
        }
        var output = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        return output;
    }

    async Task<object> PreviewCraft(CraftPluginEntry entry, SavedDevice? device)
    {
        pluginPreviewWindow?.Close();
        var panel = new Grid { Background = Brushes.Black };
        var image = new Image { Stretch = Stretch.Uniform };
        var runtime = OpenPlugin(panel, entry, device);
        panel.Children.Add(image);
        var dialog = new Window { Owner = this, Title = entry.Name + " · 实时预览",
            Width = device?.Portrait == true ? 350 : 900, Height = device?.Portrait == true ? 650 : 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        runtime.FrameChanged += () =>
        {
            dialog.Title = entry.Name + " · " + (runtime.Error ?? "实时预览");
            if (runtime.Frame is { } frame) image.Source = FitPlugin(frame, device);
        };
        dialog.Closed += (_, _) => { pluginPreviewWindow = null; runtime.Dispose(); };
        pluginPreviewWindow = dialog;
        dialog.Show();
        try { await runtime.Ready.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (TaskCanceledException) { return false; }
        catch { dialog.Close(); throw; }
        return true;
    }

    async Task<object> CraftCommand(string command, JsonElement message, bool online)
    {
        var entry = FindPlugin(message.GetProperty("pluginId").GetString()!);
        var device = message.TryGetProperty("serial", out var serial) ? saved.GetValueOrDefault(serial.GetString() ?? "") : null;
        if (command == "craft-settings") return PluginSettings(entry).Where(EditablePluginSetting).ToDictionary(field => field.Key, field => field.Value);
        if (command == "craft-configure")
        {
            var path = PluginSettingsPath(entry);
            var values = message.GetProperty("settings");
            var settings = new JsonObject();
            foreach (var field in PluginDefaults(entry).Where(EditablePluginSetting))
            {
                if (!values.TryGetProperty(field.Key, out var value)) throw new InvalidDataException("配置字段缺失。");
                var type = field.Value!.GetValueKind();
                bool valid = type is JsonValueKind.True or JsonValueKind.False
                    ? value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    : type == value.ValueKind;
                if (!valid || value.GetRawText().Length > 4096) throw new InvalidDataException("配置字段值无效。");
                settings[field.Key] = JsonNode.Parse(value.GetRawText());
            }
            File.WriteAllText(path + ".tmp", settings.ToJsonString());
            File.Move(path + ".tmp", path, true);
            foreach (var active in saved.Values.Where(d => d.Playing && d.ThemeId == PluginPrefix + entry.Id && outputs.ContainsKey(d.Serial)).ToArray())
                await Start(active);
            return true;
        }
        if (command == "craft-preview") return await PreviewCraft(entry, device);
        if (device == null || !online) throw new InvalidOperationException("请选择已连接的设备。");
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Id + "|" + device.Serial + "|" + device.Portrait)));
        var next = device with { ThemeId = PluginPrefix + entry.Id, ThemeName = entry.Name,
            ImageFile = "plugin-" + key + ".image", VideoFile = null, ThemeFile = null, Playing = true, ManualStandby = false };
        try { await Start(next); }
        catch
        {
            if (device.Playing && device.ImageFile != null && !outputs.ContainsKey(device.Serial))
                try { await Start(device); } catch (Exception ex) { errors[device.Serial] = ex.Message; }
            throw;
        }
        InvalidatePlaylistWork(device.Serial);
        playlistProgress.Remove(device.Serial);
        saved[device.Serial] = next;
        Save();
        return true;
    }
}
