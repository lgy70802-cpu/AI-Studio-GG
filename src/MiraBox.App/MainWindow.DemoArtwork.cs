using System.Windows.Media;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record DemoArtwork(LibraryEntry Entry, Brush Gradient, bool Recent);

    static readonly DemoArtwork[] DemoGallery = CreateDemoGallery();
    static readonly Dictionary<string, Brush> DemoGradients =
        DemoGallery.ToDictionary(art => art.Entry.Resource.Id, art => art.Gradient);

    static IEnumerable<LibraryEntry> DemoSeedResources() =>
        IsPlaylistDemo ? DemoResources.Concat(DemoGallery.Select(art => art.Entry)) : DemoResources;

    static Brush? FindDemoGradient(string id) => DemoGradients.GetValueOrDefault(id);

    void SeedDemoRecent()
    {
        if (state.Recent.Count != 0) return;
        foreach (var art in DemoGallery.Where(art => art.Recent && state.Resources.ContainsKey(art.Entry.Resource.Id)))
            state.NoteUse(art.Entry.Resource.Id);
    }

    static DemoArtwork[] CreateDemoGallery()
    {
        string[] titles =
        [
            "青空", "晚霞", "潮汐", "雾岛", "夜航", "晨曦", "远山", "流光", "茶烟",
            "海盐", "初雪", "落日", "霓虹", "松林", "微光", "空谷", "浮云", "星桥"
        ];
        (string Start, string End)[] palette =
        [
            ("#17356F", "#42C7DB"), ("#5F315E", "#F2A377"), ("#184B6A", "#6ADBC7"),
            ("#2E3A62", "#93B8CC"), ("#181E47", "#7661C7"), ("#536C8A", "#F4BF9A"),
            ("#345A50", "#9DC69C"), ("#3B317B", "#E687AC"), ("#694940", "#D8AF84")
        ];
        string[] types = ["壁纸", "视频", "主题", "时钟"];
        var gallery = new List<DemoArtwork>();

        AddSet("wide", "横屏", [new(1920, 462), new(1280, 480)], titles.Length);
        AddSet("tall", "竖屏", [new(462, 1920), new(480, 1280)], titles.Length);
        AddSet("near-square", "近方形", [new(1000, 960)], 3);
        return gallery.ToArray();

        void AddSet(string key, string orientation, IReadOnlyList<Size> layouts, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var type = types[index % types.Length];
                var (start, end) = palette[index % palette.Length];
                var gradient = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString(start),
                    (Color)ColorConverter.ConvertFromString(end), key == "tall" ? 80 + index * 3 : 14 + index * 4);
                gradient.Freeze();
                var title = titles[index] + " · " + orientation;
                var resource = new Resource($"v7-art-{key}-{index + 1:00}", title, layouts);
                var entry = new LibraryEntry(resource, type, "渐变", Added: DateTimeOffset.UnixEpoch.AddDays(80 + index),
                    Hot: 110 + index, Duration: type == "视频" ? TimeSpan.FromSeconds(16 + index * 3) : null);
                gallery.Add(new(entry, gradient, key != "near-square" && index < 6));
            }
        }
    }
}
