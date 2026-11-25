using System.IO;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public sealed partial class MixedPlaylistViewModel
{
    private bool _batchSeconds;
    private string _candidateSearch = "";
    public string CandidateSearch
    {
        get => _candidateSearch;
        set
        {
            if (SetProperty(ref _candidateSearch, value))
                OnPropertyChanged(nameof(FilteredCandidates));
        }
    }
    public IEnumerable<MixedCandidate> FilteredCandidates => Candidates.Where(item =>
        item.Name.Contains(CandidateSearch, StringComparison.CurrentCultureIgnoreCase));
    public string ListSummary => $"{Items.Count} 项内容，停留时间合计 {TimeSpan.FromSeconds(Items.Sum(item => (long)item.Seconds)):g}（播完切换的视频以实际时长为准）";

    public Task RenameListAsync(string name) => ChangeListAsync(async () =>
    {
        if (SelectedList is not { } selected || selected.Id == Guid.Empty)
            throw new InvalidOperationException("默认列表保留原名称，可复制为新的命名列表。");
        name = name.Trim();
        if (name.Length is < 1 or > 60) throw new InvalidDataException("名称应为1至60个字符。");
        var replacement = selected with { Name = name };
        var index = NamedLists.Where(item => item.Id != Guid.Empty)
            .Select(item => item.Id == selected.Id ? replacement : item).ToList();
        await _store.SaveAsync("mixed-list-index.json", index);
        NamedLists[NamedLists.IndexOf(selected)] = replacement;
        SelectedList = replacement;
    });

    public Task CopyListAsync(string name) => ChangeListAsync(async () =>
    {
        name = name.Trim();
        if (name.Length is < 1 or > 60) throw new InvalidDataException("名称应为1至60个字符。");
        if (NamedLists.Count >= 101) throw new InvalidDataException("最多创建100个命名列表。");
        await FlushAsync();
        var list = new NamedPlaylist(Guid.NewGuid(), name);
        var snapshot = new MixedList(1, Items.Select(item => item.Entry with { EntryId = Guid.NewGuid() }).ToList(), Shuffle);
        await _store.SaveAsync($"mixed-list-{list.Id:N}.json", snapshot);
        await _store.SaveAsync("mixed-list-index.json", NamedLists.Where(item => item.Id != Guid.Empty).Append(list).ToList());
        NamedLists.Add(list);
        await SelectListCoreAsync(list);
    });

    public Task DeleteSelectedListAsync() => ChangeListAsync(async () =>
    {
        if (SelectedList is not { } selected || selected.Id == Guid.Empty)
            throw new InvalidOperationException("默认列表不能删除。");
        await SelectListCoreAsync(NamedLists[0]);
        await _store.SaveAsync("mixed-list-index.json", NamedLists.Where(item => item.Id != Guid.Empty && item.Id != selected.Id).ToList());
        NamedLists.Remove(selected);
    });

    public void SetAllSeconds(int seconds)
    {
        if (!CanEdit || seconds is < 10 or > 86400) return;
        _batchSeconds = true;
        try { foreach (var item in Items) item.Seconds = seconds; }
        finally { _batchSeconds = false; }
        QueueSave();
        NotifyPlayback();
    }
}
