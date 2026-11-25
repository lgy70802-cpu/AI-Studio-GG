using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    static bool IsPlaylistDemo => Environment.GetEnvironmentVariable("MIRABOX_PLAYLIST_DEMO") == "1";
    SessionSnapshot? hiddenSimulation;

    void OpenDemoMode(object sender, System.Windows.RoutedEventArgs e)
    {
        if (IsPlaylistDemo) return;
        DemoToggle.IsChecked = false;
        OpenPlaylistDemo();
    }

    void HideSimulationInDailyMode()
    {
        if (IsPlaylistDemo) return;
        hiddenSimulation = SessionStore.Capture(state, library, activeScenario);
        state.Devices.Clear();
        foreach (var list in state.Playlists.Where(list => !IsHardwareSchedule(list.DeviceId)).ToArray())
            state.Playlists.Remove(list);
        state.Schedules.RemoveAll(rule => !IsHardwareSchedule(rule.DeviceId));
    }

    SessionSnapshot PreserveHiddenSimulation(SessionSnapshot snapshot) => hiddenSimulation == null ? snapshot : snapshot with
    {
        Devices = hiddenSimulation.Devices,
        Playlists = snapshot.Playlists.Concat(hiddenSimulation.Playlists
            .Where(list => !IsHardwareSchedule(list.DeviceId) && snapshot.Playlists.All(current => current.Id != list.Id))).ToList(),
        Schedules = (snapshot.Schedules ?? []).Concat((hiddenSimulation.Schedules ?? [])
            .Where(rule => !IsHardwareSchedule(rule.DeviceId))).ToList()
    };

    void OpenPlaylistDemo()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法找到程序路径。");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.Environment["MIRABOX_DATA_DIR"] = Path.Combine(DataRoot, "PlaylistDemo-V7");
        start.Environment["MIRABOX_PLAYLIST_DEMO"] = "1";
        Process.Start(start)?.Dispose();
    }

    static readonly LibraryEntry[] DemoResources =
    [
        new(new("clock", "留白时钟", [new(1920, 462), new(1280, 480), new(462, 1920)]), "主题", "氛围"),
        new(new("sun", "日落航线", [new(1920, 462)]), "壁纸", "风景", "海岸", Online: true, Hot: 90),
        new(new("vertical", "纵向日历", [new(462, 1920)]), "主题", "效率", "日历"),
        new(new("sea", "海岸晨光", [new(1280, 480)], false), "壁纸", "风景", "海岸", Online: true, Hot: 80),
        Example("starfield", "星河长夜", "壁纸", "风景", "星空", [new(1920, 462)], 93, 44, online: true),
        Example("mint", "薄荷渐变", "壁纸", "抽象", "渐变", [new(1280, 480)], 57, 46, online: true),
        Example("portrait-cloud", "竖版云海", "壁纸", "风景", "山水", [new(462, 1920)], 79, 52, online: true),
        Example("portrait-hills", "青山入梦", "壁纸", "风景", "山水", [new(480, 1280)], 72, 45, local: false, online: true),
        Example("paper-calendar", "纸页日历", "主题", "效率", "日历", [new(1920, 462), new(462, 1920)], 83, 54, online: true),
        Example("performance", "性能仪表", "主题", "监控", "性能", [new(1920, 462), new(1280, 480)], 91, 53),
        Example("minimal-clock", "极简翻页钟", "时钟", "极简", "", [new(1920, 462), new(462, 1920)], 76, 51, online: true),
        Example("moon-clock", "月相时钟", "时钟", "数字", "翻页", [new(1920, 462)], 90, 58, local: false, online: true),
        Example("aurora-film", "极光流动", "视频", "自然", "极光", [new(1920, 462)], 94, 56, online: true),
        Example("cyber-rain", "赛博雨夜", "视频", "科幻", "", [new(1920, 462), new(1280, 480)], 89, 57),
        Example("star-film", "星尘穿梭", "视频", "科幻", "", [new(1920, 462)], 86, 60, local: false, online: true)
    ];

    static LibraryEntry Example(string id, string name, string type, string category, string subcategory,
        IReadOnlyList<Size> layouts, int hot, int added, bool local = true, bool online = false) =>
        new(new(id, name, layouts, local), type, category, subcategory,
            Added: DateTimeOffset.UnixEpoch.AddDays(added), Online: online, Hot: hot);

    static ThemeLayout DemoAppearance(string id, Size size) => id switch
    {
        "clock" => new(size, Color: "#363947", Style: "paper"),
        "sun" or "sea" => new(size, Style: "sun"),
        "vertical" or "aurora-film" => new(size, Style: "aurora"),
        "starfield" or "star-film" => new(size, Style: "night"),
        "mint" => new(size, Style: "mint"),
        "portrait-cloud" => new(size, Style: "ice"),
        "portrait-hills" => new(size, Style: "mountain"),
        "paper-calendar" => new(size, "OCT 30", "#363947", "paper"),
        "performance" => new(size, "GPU 41%", Style: "neon"),
        "minimal-clock" => new(size, Style: "mono"),
        "moon-clock" => new(size, "23:08", Style: "night"),
        "cyber-rain" => new(size, Style: "neon"),
        _ => new(size)
    };

    static Brush DemoStyle(string style) => ColorBrush(style switch
    {
        "night" => "#18254D", "mint" => "#518B76", "ice" => "#64899F",
        "neon" => "#533D75", "mono" => "#30343A", _ => "#253D5C"
    });

    void AddDemoPlaylists()
    {
        foreach (var device in state.Devices.Values)
        {
            Add("日常桌面", "清晨", "clock", "sun");
            Add("竖屏精选", "灵感", "vertical");
            Add("夜晚", "夜晚", "clock", "vertical");

            void Add(string name, string mood, params string[] resources)
            {
                var id = $"demo:{device.Id}:{name}";
                if (state.Playlists.Any(list => list.Id == id)) return;
                state.Playlists.Add(new Playlist(device.Id,
                    resources.Where(resource => state.Resources.GetValueOrDefault(resource)?.Local == true)
                        .Select(resource => new PlaylistItem(resource, TimeSpan.FromSeconds(12))).ToArray())
                    { Id = id, Name = name, Mood = mood });
            }
        }
    }
}
