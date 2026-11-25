using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiraBox.Core;
using Size = MiraBox.Core.Size;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly Dictionary<(bool Hour12, int Width, int Height), WoodlandRuntime> woodlandRuntimes = [];
    bool woodlandLifetimeHooked, preparingWoodland;
    bool woodlandHour12;

    WoodlandRuntime Woodland(CraftPluginReference reference, Size canvas)
    {
        if (reference.PluginId != CraftPluginStore.WoodlandId) throw new NotSupportedException("尚未支持此 Craft 插件运行。");
        var key = (reference.Hour12, canvas.Width, canvas.Height);
        if (!woodlandRuntimes.TryGetValue(key, out var runtime))
        {
            var entry = craftPlugins.Entries.SingleOrDefault(item => item.Id == reference.PluginId)
                ?? throw new FileNotFoundException("Woodland Clock 插件尚未导入。");
            var package = craftPlugins.PackagePath(entry);
            if (!File.Exists(Path.Combine(package, entry.CodePath))) throw new FileNotFoundException("Woodland Clock 插件文件已丢失。");
            var cache = Path.Combine(DataRoot, "Plugins", "RuntimeCache", $"{entry.ContentHash}-{canvas.Width}x{canvas.Height}-{reference.Hour12}.jpg");
            runtime = new(CraftRuntimeHost, entry, package, cache, canvas.Width, canvas.Height, reference.Hour12);
            woodlandRuntimes.Add(key, runtime);
            var observed = runtime;
            runtime.FrameChanged += () => {
                if (observed.Error != null) Notice.Text = "Woodland Clock 运行异常，已保留上一帧：" + observed.Error;
            };
        }
        runtime.LastUsed = DateTimeOffset.UtcNow;
        if (!woodlandLifetimeHooked)
        {
            woodlandLifetimeHooked = true;
            Closed += (_, _) => DisposeWoodlandRuntimes();
        }
        return runtime;
    }

    void DisposeWoodlandRuntimes()
    {
        foreach (var runtime in woodlandRuntimes.Values) runtime.Dispose();
        woodlandRuntimes.Clear();
    }

    void TrimWoodlandRuntimes()
    {
        foreach (var pair in woodlandRuntimes.ToArray())
            if (pair.Value.Viewers == 0 && DateTimeOffset.UtcNow - pair.Value.LastUsed > TimeSpan.FromMinutes(1))
            { pair.Value.Dispose(); woodlandRuntimes.Remove(pair.Key); }
    }

    FrameworkElement WoodlandArtwork(CraftPluginReference reference, Size size)
    {
        WoodlandRuntime runtime;
        try { runtime = Woodland(reference, size); }
        catch (Exception ex) { return HomeText("Woodland Clock 无法加载：" + ex.Message, 13, muted: true); }
        var image = new Image { Source = runtime.Frame, Stretch = Stretch.Uniform };
        var message = HomeText("正在启动 Woodland Clock…", 20, muted: true);
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.VerticalAlignment = VerticalAlignment.Center;
        var panel = new Grid { Background = ColorBrush("#E3D4C4") };
        panel.Children.Add(image); panel.Children.Add(message);
        void Refresh()
        {
            image.Source = runtime.Frame;
            message.Text = runtime.Error ?? "正在启动 Woodland Clock…";
            message.Visibility = runtime.Frame == null ? Visibility.Visible : Visibility.Collapsed;
        }
        Refresh();
        var subscribed = false;
        panel.Loaded += (_, _) => {
            if (subscribed) return;
            subscribed = true; runtime.Viewers++; runtime.FrameChanged += Refresh; Refresh();
        };
        panel.Unloaded += (_, _) => {
            if (!subscribed) return;
            subscribed = false; runtime.Viewers--; runtime.FrameChanged -= Refresh;
        };
        return panel;
    }

    byte[] EncodeWoodland(ThemeProject theme, int rotation)
    {
        var geometry = D92StaticImage.Geometry(rotation, HardwareModel);
        var runtime = Woodland(theme.CraftPlugin!, new(geometry.Width, geometry.Height));
        if (runtime.Error != null) throw new InvalidOperationException("Woodland Clock 运行异常：" + runtime.Error);
        if (runtime.Frame == null) throw new InvalidOperationException("Woodland Clock 正在加载，请稍后再次应用。");
        var key = (runtime.Revision, rotation, HardwareModel);
        if (runtime.Encoded == null || runtime.EncodedKey != key)
        {
            runtime.Encoded = D92StaticImage.Encode(rotation, runtime.Frame, HardwareModel);
            runtime.EncodedKey = key;
        }
        return runtime.Encoded;
    }

    bool WoodlandFilesAvailable(CraftPluginReference reference)
    {
        try
        {
            var entry = craftPlugins.Entries.SingleOrDefault(item => item.Id == reference.PluginId);
            return entry != null && new[] { entry.CodePath, "interval.js", "images/woodland.png", "fonts/fredoka.ttf" }
                .All(file => File.Exists(Path.Combine(craftPlugins.PackagePath(entry), file)));
        }
        catch { return false; }
    }

    async Task<Resource> PrepareWoodlandTheme(bool hour12)
    {
        var reference = new CraftPluginReference(CraftPluginStore.WoodlandId, hour12);
        var layouts = new[] { new Size(1920, 462), new Size(1280, 480) };
        foreach (var size in layouts)
        {
            var runtime = Woodland(reference, size);
            await runtime.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            if (runtime.Error != null) throw new InvalidOperationException(runtime.Error);
        }
        var id = hour12 ? "craft-woodland-12" : "craft-woodland-24";
        var theme = new ThemeProject(id, layouts.Select(size => new ThemeLayout(size)).ToList(), CraftPlugin: reference);
        var before = SessionStore.Capture(state, library, activeScenario);
        try
        {
            state.SaveTheme(theme, hour12 ? "Woodland Clock · 12 小时制" : "Woodland Clock · 24 小时制", "主题", library);
            SaveWorkspace(); SyncLibrary();
        }
        catch { state = SessionStore.Restore(before, library); throw; }
        return state.Resources[id];
    }

    async Task UseWoodland(bool hour12)
    {
        if (preparingWoodland) return;
        preparingWoodland = true;
        var serial = hardwareApplySerial;
        var simulated = target;
        try
        {
            Notice.Text = "正在启动 Woodland Clock…";
            foreach (var pair in woodlandRuntimes.Where(pair => pair.Value.Error != null).ToArray())
            { pair.Value.Dispose(); woodlandRuntimes.Remove(pair.Key); }
            var resource = await PrepareWoodlandTheme(hour12);
            if (serial != hardwareApplySerial || simulated != target)
            { Notice.Text = "Woodland Clock 已保存到资源库；当前设备已切换，请重新点击应用。"; return; }
            // Plugin-center actions target the explicitly selected device, independent of
            // the legacy page-specific routing used by resource cards.
            if (serial != null) ApplyHardwareResource(resource);
            else Apply(resource);
        }
        catch (Exception ex) { Notice.Text = "Woodland Clock 启动失败：" + ex.Message; }
        finally { preparingWoodland = false; }
    }

    void PreviewWoodland(bool hour12)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var artwork = WoodlandArtwork(new(CraftPluginStore.WoodlandId, hour12), new(1920, 462));
        artwork.Height = 170;
        panel.Children.Add(artwork);
        panel.Children.Add(HomeText("原插件实时显示小时、分钟、日期与星期，不含秒钟；此预览不会改变设备输出。", 13, muted: true));
        var dialog = Dialog("Woodland Clock · 实时预览", panel, 780, 340);
        panel.Children.Add(HomeButton("关闭", dialog.Close, "PillButton"));
        dialog.ShowDialog();
    }
}
