using System.IO;
using System.Collections.ObjectModel;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public sealed record NamedPlaylist(Guid Id, string Name);

public sealed partial class MixedPlaylistViewModel
{
    private bool _listChanging;
    private string _activeListFile = "mixed-playlist.json";
    private NamedPlaylist? _selectedList;
    public ObservableCollection<NamedPlaylist> NamedLists { get; } = [];
    public NamedPlaylist? SelectedList
    {
        get => _selectedList;
        private set => SetProperty(ref _selectedList, value);
    }

    public Task CreateListAsync(string name) => ChangeListAsync(() => CreateListCoreAsync(name));
    public Task SelectListAsync(NamedPlaylist list) => ChangeListAsync(() => SelectListCoreAsync(list));

    private async Task ChangeListAsync(Func<Task> operation)
    {
        if (!CanEdit) return;
        _listChanging = true;
        OnPropertyChanged(nameof(CanEdit));
        try { await operation(); }
        finally
        {
            _listChanging = false;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(SelectedList));
        }
    }
    private async Task LoadNamedListsAsync()
    {
        var lists = await _store.ReadAsync("mixed-list-index.json", new List<NamedPlaylist>());
        if (lists.Count > 100 || lists.Any(list => list.Id == Guid.Empty || string.IsNullOrWhiteSpace(list.Name))
            || lists.Select(list => list.Id).Distinct().Count() != lists.Count)
            throw new InvalidDataException("轮播列表索引无效。");
        NamedLists.Add(new NamedPlaylist(Guid.Empty, "默认列表"));
        foreach (var list in lists) NamedLists.Add(list);
        SelectedList = NamedLists[0];
    }

    private async Task CreateListCoreAsync(string name)
    {
        if (!_initialized || IsRunning || _applying) return;
        name = name.Trim();
        if (name.Length is < 1 or > 60) throw new InvalidDataException("列表名称应为1至60个字符。");
        if (NamedLists.Count >= 101) throw new InvalidDataException("最多创建100个命名列表。");
        var list = new NamedPlaylist(Guid.NewGuid(), name);
        await _store.SaveAsync($"mixed-list-{list.Id:N}.json", new MixedList(1, []));
        var index = NamedLists.Where(item => item.Id != Guid.Empty).Append(list).ToList();
        await _store.SaveAsync("mixed-list-index.json", index);
        NamedLists.Add(list);
        await SelectListCoreAsync(list);
    }

    private async Task SelectListCoreAsync(NamedPlaylist list)
    {
        if (IsRunning || _applying || !NamedLists.Contains(list) || SelectedList == list) return;
        await FlushAsync();
        var file = list.Id == Guid.Empty ? "mixed-playlist.json" : $"mixed-list-{list.Id:N}.json";
        var data = await _store.ReadAsync(file, new MixedList(1, []));
        if (data.Version != 1 || data.Items is null || data.Items.Count > 100
            || data.Items.Select(item => item.EntryId).Distinct().Count() != data.Items.Count)
            throw new InvalidDataException("轮播列表无效。");
        foreach (var item in data.Items)
        {
            ContentListStore.Validate(item.Content);
            if (item.EntryId == Guid.Empty || item.Seconds is < 10 or > 86400)
                throw new InvalidDataException("轮播条目无效。");
        }
        _activeListFile = file;
        Items.Clear();
        foreach (var item in data.Items) Items.Add(new MixedPlaylistRow(this, item));
        _shuffle = data.Shuffle;
        OnPropertyChanged(nameof(Shuffle));
        SelectedList = list;
        Selected = null;
        NotifyPlayback();
    }
}
