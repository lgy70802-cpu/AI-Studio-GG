using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiraBox.Core;
namespace MiraBox.App;
public partial class MainWindow
{
    string? selectedList;
    string? lastResourcePlaylist;
    static readonly string[] PlaylistMoods = ["专注", "清晨", "夜晚", "放松", "灵感", "喜爱"];
    static string MoodIcon(string mood) => mood switch
    {
        "专注" => "◎", "清晨" => "☀", "夜晚" => "☾", "放松" => "♪", "喜爱" => "♥", _ => "✦"
    };

    WrapPanel MoodChoices(string selectedMood, Action<string> select)
    {
        var choices = new WrapPanel();
        choices.Children.Add(new Border { Height = 40, Margin = new Thickness(0, 0, 8, 0), Child = HomeText("心情") });
        foreach (var mood in PlaylistMoods)
        {
            var button = new RadioButton
            {
                Content = PlaylistIconLabel(mood, PlaylistMoodGeometry(mood)), GroupName = "PlaylistMood",
                IsChecked = mood == selectedMood, Margin = new Thickness(0, 4, 8, 4)
            };
            button.SetResourceReference(StyleProperty, "PlaylistMoodChoice");
            System.Windows.Automation.AutomationProperties.SetName(button, mood);
            button.Click += (_, _) =>
            {
                try { select(mood); }
                catch (Exception ex) { Notice.Text = ex.Message; }
            };
            choices.Children.Add(button);
        }
        return choices;
    }
    (string List,string[] Ids,DateTimeOffset Until)? undo;
    void SaveLists()=>SaveWorkspace();
    void CreateList(Action<Playlist>? after = null, string? deviceId = null)
    {
        var panel=new StackPanel{Margin=new Thickness(20)};var dialog=Dialog("新建轮播列表",panel,460,320);var name=new TextBox{Margin=new Thickness(4),MaxLength=30};panel.Children.Add(Text("列表名称"));panel.Children.Add(name);
        var mood = "灵感";
        panel.Children.Add(MoodChoices(mood, value => mood = value));
        panel.Children.Add(Action("创建", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return;
            var list = new Playlist(HardwarePlaybackSelected ? HardwareSchedulePrefix + hardwareApplySerial :
                deviceId ?? target ?? "", []) { Name = name.Text.Trim(), Mood = mood };
            state.Playlists.Add(list);
            selectedList = list.Id;
            SaveLists();
            dialog.Close();
            after?.Invoke(list);
            Render();
        }));
        dialog.ShowDialog();
    }
    void Added(Playlist p,IEnumerable<string> ids)
    {
        var requested = ids.Distinct().Where(id => state.Resources.GetValueOrDefault(id) is { Local: true }).ToArray();
        var added = requested.Where(id => !p.Items.Any(item => item.ResourceId == id)).ToArray();
        var repeated = requested.Length - added.Length;
        if (added.Length == 0)
        {
            Notice.Text = repeated == 0 ? "没有可加入的本地内容。" : $"所选 {repeated} 项已在「{p.Name}」中。";
            return;
        }
        p.Append(added.Select(id => new PlaylistItem(id, TimeSpan.FromSeconds(30))));
        undo = (p.Id, added, DateTimeOffset.UtcNow.AddSeconds(5));
        SaveLists();
        if (page != "轮播列表")
        {
            lastResourcePlaylist = p.Id;
            var mismatches = added.Count(id => !PlaylistFits(p, state.Resources[id]));
            Notice.Text = $"已加入「{p.Name}」{added.Length} 项" +
                (repeated > 0 ? $" · 已在列表 {repeated} 项" : "") +
                (mismatches > 0 ? $" · {mismatches} 项需适配，播放时跳过" : "");
        }
    }
    void RenderUndo(Panel panel){if(undo is not {} u||DateTimeOffset.UtcNow>u.Until)return;panel.Children.Add(Action("撤销本次加入",()=>{if(DateTimeOffset.UtcNow>u.Until){undo=null;Render();return;}var p=state.Playlists.FirstOrDefault(x=>x.Id==u.List);if(p!=null)foreach(var id in u.Ids)p.Remove(id);undo=null;SaveLists();Render();}));}
    void PickPlaylist() => PickPlaylist(selected.ToArray());

    void PickPlaylist(string[] requested)
    {
        var ids = requested.Distinct().Where(id => state.Resources.GetValueOrDefault(id) is { Local: true }).ToArray();
        if (ids.Length == 0) return;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("加入轮播列表", panel, 600, 480);
        panel.Children.Add(Text($"把 {ids.Length} 项内容加到哪个列表？"));
        void Add(Playlist p)
        {
            Added(p, ids);
            if (selecting)
            {
                selected.ExceptWith(ids);
                selecting = selected.Count > 0;
            }
            dialog.Close();
            Render();
        }
        foreach (var p in state.Playlists.Where(list => !IsHardwareSchedule(list.DeviceId) ||
            HardwarePlaybackSelected && list.DeviceId == HardwareSchedulePrefix + hardwareApplySerial))
        {
            var row = new WrapPanel();
            foreach (var item in p.Items.Take(3))
                if (state.Resources.TryGetValue(item.ResourceId, out var r)) row.Children.Add(Thumbnail(r, BrowseCanvas(), 80, 40));
            var candidates = ids.Where(id => !p.Items.Any(item => item.ResourceId == id)).ToArray();
            var incompatible = candidates.Count(id => !PlaylistFits(p, state.Resources[id]));
            var add = HomeButton($"{p.Name} · 加入 {candidates.Length} 项", () => Add(p), "PillButton");
            add.IsEnabled = candidates.Length > 0;
            row.Children.Add(add);
            var card = new StackPanel();
            card.Children.Add(row);
            card.Children.Add(HomeText($"{(HardwarePlaybackSelected ? HardwareTargetLabel : DeviceName(p.DeviceId))} · " +
                $"已在列表 {ids.Length - candidates.Length} 项" +
                (incompatible > 0 ? $" · {incompatible} 项需适配，播放时跳过" : ""), 13, muted: true));
            panel.Children.Add(HomeCard(card));
        }
        panel.Children.Add(Action("新建并加入", () => CreateList(Add)));
        dialog.ShowDialog();
    }
}
