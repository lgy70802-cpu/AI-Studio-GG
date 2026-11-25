using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public sealed class ThemePlaylistRow(CreatorViewModel owner, ThemePlaylistEntry entry) : ObservableObject
{
    public Guid ProjectId => entry.ProjectId;
    public void RefreshSource(){OnPropertyChanged(nameof(Name));OnPropertyChanged(nameof(Availability));}
    public string Name => owner.Projects.FirstOrDefault(p => p.Project.Id == ProjectId)?.Name ?? "缺失项目：" + ProjectId.ToString("N")[..8];
    public string Availability
    {
        get
        {
            var project=owner.Projects.FirstOrDefault(p=>p.Project.Id==ProjectId)?.Project;
            if(project is null)return "项目缺失，将跳过";
            return project.Landscape.Layers.Concat(project.Portrait.Layers).Any(l=>l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper && !System.IO.File.Exists(l.Path))?"存在缺失素材，请检查":"播放前检查目标能力";
        }
    }
    private int _seconds = entry.Seconds;
    public int Seconds { get => _seconds; set { var valid = Math.Clamp(value, 10, 86400); if (SetProperty(ref _seconds, valid)) owner.QueueThemePlaylistSave(); } }
}
public sealed partial class CreatorViewModel
{
    private DispatcherTimer _themeTimer = null!;
    private DispatcherTimer _themeStatusTimer = null!;
    private bool _themePaused;
    private long _themeDue;
    private TimeSpan _themeRemaining;
    public bool IsThemePaused => _themePaused;
    public string ThemePauseLabel => _themePaused ? "继续切换" : "暂停切换";
    public string ThemeCurrentName => _themeCurrent is null ? "尚未播放" : _themePlayback?.Project.Name ?? "当前主题";
    public string ThemeNextName { get { var items = ThemePlaylistItems.ToArray(); if (items.Length == 0) return "无"; var index = Array.FindIndex(items, i => i.ProjectId == _themeCurrent); return items[(index + 1) % items.Length].Name; } }
    public string ThemeCountdown => !IsThemeRotating ? "未启动" : _themePaused ? "已暂停自动切换，当前画面继续更新" : $"约 {Math.Max(0, (int)Math.Ceiling((_themeDue - Environment.TickCount64) / 1000d))} 秒后切换";
    public void ToggleThemePause()
    {
        if (!IsThemeRotating || _themeTransition) return;
        if (!_themePaused) { _themeRemaining = TimeSpan.FromMilliseconds(Math.Max(1, _themeDue - Environment.TickCount64)); _themeTimer.Stop(); _themePaused = true; }
        else { _themePaused = false; ScheduleThemeSwitch(_themeRemaining); }
        NotifyThemePlayback();
        if(ThemePlaylistTarget=="D92 USB" && _main.Usb is {IsRunning:true,SelectedSerial:{Length:>0} serial} usb)_main.RememberLegacyOutput(serial,usb.Scene);
    }
    private void ScheduleThemeSwitch(TimeSpan interval) { _themeTimer.Interval = interval; _themeDue = Environment.TickCount64 + (long)interval.TotalMilliseconds; if (IsThemeRotating && !_themePaused) _themeTimer.Start(); NotifyThemePlayback(); }
    private void NotifyThemePlayback() { foreach (var property in new[] { nameof(IsThemePaused), nameof(ThemePauseLabel), nameof(ThemeCurrentName), nameof(ThemeNextName), nameof(ThemeCountdown) }) OnPropertyChanged(property); }
    private void ThemeStatusTick(object? sender, EventArgs e) => OnPropertyChanged(nameof(ThemeCountdown));
    private CreatorViewModel? _themePlayback;
    private Task _themeAdvanceTask = Task.CompletedTask, _themePlaylistSaveTask = Task.CompletedTask;
    private bool _themeTransition, _themePlaylistLoaded;
    private Guid? _themeCurrent;
    private long _themeGeneration;
    private string? _themeDeviceIdentity;
    private string _themeTarget = "D92 USB", _themeStatus = "主题轮播未启动";
    public bool IsThemeRotating { get; private set; }
    public string ThemePlaylistStatus { get => _themeStatus; private set => SetProperty(ref _themeStatus, value); }
    public string ThemePlaylistTarget { get => _themeTarget; set { if (value is not ("D92 USB" or "Windows 副屏") || value == _themeTarget) return; StopThemePlaylist(); _themeTarget = value; OnPropertyChanged(); QueueThemePlaylistSave(); } }
    public ObservableCollection<ThemePlaylistRow> ThemePlaylistItems { get; } = [];
    public AsyncCommand ThemePlaylistCommand { get; private set; } = null!;
    private void InitializeThemePlaylist()
    {
        _themeTimer = new DispatcherTimer(DispatcherPriority.Background); _themeTimer.Tick += ThemePlaylistTick;
        _themeStatusTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) }; _themeStatusTimer.Tick += ThemeStatusTick;
        ThemePlaylistCommand = new(async () => { await RefreshProjectsAsync(); await LoadThemePlaylistAsync(); _main.CarouselSection=1;_main.SelectedPage=AppPage.Carousel; }, () => CanEdit, _logger);
    }
    public async Task LoadThemePlaylistAsync()
    {
        if (_themePlaylistLoaded) return; var settings = await _store.ReadThemePlaylistAsync(); _themeTarget = settings.Target; ThemePlaylistItems.Clear(); foreach (var entry in settings.Entries) ThemePlaylistItems.Add(new(this, entry)); _themePlaylistLoaded = true; OnPropertyChanged(nameof(ThemePlaylistTarget));
    }
    public void AddThemePlaylist(Guid id)
    {
        if (!Projects.Any(p => p.Project.Id == id) || ThemePlaylistItems.Any(e => e.ProjectId == id) || ThemePlaylistItems.Count >= 100) return;
        ThemePlaylistItems.Add(new(this, new(id, 30))); QueueThemePlaylistSave(); NotifyThemePlayback();
    }
    public void RemoveThemePlaylist(Guid id) { var item = ThemePlaylistItems.FirstOrDefault(e => e.ProjectId == id); if (item is null) return; ThemePlaylistItems.Remove(item); QueueThemePlaylistSave(); NotifyThemePlayback(); if (ThemePlaylistItems.Count == 0) StopThemePlaylist(); }
    public void MoveThemePlaylist(Guid id, int delta) { var item = ThemePlaylistItems.FirstOrDefault(e => e.ProjectId == id); if (item is null) return; var index = ThemePlaylistItems.IndexOf(item); var target = Math.Clamp(index + delta, 0, ThemePlaylistItems.Count - 1); if (target != index) { ThemePlaylistItems.Move(index, target); QueueThemePlaylistSave(); NotifyThemePlayback(); } }
    public void QueueThemePlaylistSave()
    {
        if (!_themePlaylistLoaded || _disposed) return;
        var snapshot = new ThemePlaylistSettings(ThemePlaylistTarget, ThemePlaylistItems.Select(i => new ThemePlaylistEntry(i.ProjectId, i.Seconds)).ToList());
        _themePlaylistSaveTask = SavePlaylistAfterAsync(_themePlaylistSaveTask, snapshot);
    }
    private async Task SavePlaylistAfterAsync(Task previous, ThemePlaylistSettings snapshot) { try { await previous; await _store.SaveThemePlaylistAsync(snapshot); } catch (Exception e) { _logger.Warning(e, "Cannot save theme playlist"); ThemePlaylistStatus = "轮播保存失败：" + e.Message; } }
    public Task FlushThemePlaylistAsync() => _themePlaylistSaveTask;
    public async Task ConfirmThemePlaylistStartAsync(System.Windows.Window owner)
    {
        await LoadThemePlaylistAsync(); var sources = await _store.ListAsync(); var lastProblem = "";
        foreach (var item in ThemePlaylistItems)
        {
            var source = sources.FirstOrDefault(p => p.Id == item.ProjectId); if (source is null) continue;
            using var preview = new CreatorViewModel(_main, _logger, _store) { OutputTarget = ThemePlaylistTarget };
            await preview.OpenProjectAsync(source.Clone());
            var check = preview.CheckThemeApplication(); if (!check.CanApply) { lastProblem = string.Join("；", check.Problems); continue; }
            // The final start routine checks every source again; this dialog does not perform output.
            var dialog = new DeskDisplay.App.Views.CreatorApplyConfirmation(preview) { Owner = owner };
            if (dialog.ShowDialog() == true) await StartThemePlaylistAsync();
            return;
        }
        ThemePlaylistStatus = "没有可用于该目标的主题。" + lastProblem;
    }
    public async Task StartThemePlaylistAsync(Guid? startAt=null,bool paused=false)
    {
        _main.ActiveMixedPlaylist?.Stop();
        StopThemePlaylist(); await _themeAdvanceTask; await LoadThemePlaylistAsync(); await FlushThemePlaylistAsync(); if (ThemePlaylistItems.Count == 0) { ThemePlaylistStatus = "请先添加已保存主题。"; return; }
        _themeDeviceIdentity = ThemePlaylistTarget == "D92 USB" ? _main.Usb?.SelectedSerial : _main.Devices.TargetDisplay?.DeviceName;
        var entries=ThemePlaylistItems.ToArray();var startIndex=Array.FindIndex(entries,e=>e.ProjectId==startAt);
        _themeCurrent=startIndex>=0?entries[(startIndex-1+entries.Length)%entries.Length].ProjectId:null;
        IsThemeRotating = true; _themeStatusTimer.Start(); _main.Library?.StopRotation(); OnPropertyChanged(nameof(IsThemeRotating)); await AdvanceThemePlaylistAsync();
        if(paused && IsThemeRotating)ToggleThemePause();
    }
    public async Task AdvanceThemePlaylistAsync()
    {
        if (!IsThemeRotating || _themeTransition) return;
        _themeAdvanceTask = AdvanceThemeCoreAsync(); await _themeAdvanceTask;
    }
    private async Task AdvanceThemeCoreAsync()
    {
        _themeTimer.Stop(); _themeTransition = true; var generation = _themeGeneration;
        try
        {
            if (ThemePlaylistTarget == "D92 USB" && _main.Usb?.IsWaitingForDevice == true) { ThemePlaylistStatus = "等待原设备重连，暂停切换主题。"; ScheduleThemeSwitch(TimeSpan.FromSeconds(2)); return; }
            var identity = ThemePlaylistTarget == "D92 USB" ? _main.Usb?.SelectedSerial : _main.Devices.TargetDisplay?.DeviceName;
            if (identity != _themeDeviceIdentity || identity is null) { StopThemePlaylist(); ThemePlaylistStatus = "输出目标已改变或不可用，轮播已停止。"; return; }
            var projects = await _store.ListAsync(); var items = ThemePlaylistItems.ToArray(); var index = Array.FindIndex(items, i => i.ProjectId == _themeCurrent);
            for (var offset = 1; offset <= items.Length; offset++)
            {
                if (_disposed || generation != _themeGeneration) return;
                var item = items[(index + offset) % items.Length]; var source = projects.FirstOrDefault(p => p.Id == item.ProjectId); if (source is null) continue;
                var candidate = new CreatorViewModel(_main, _logger, _store) { OutputTarget = ThemePlaylistTarget };
                try
                {
                    await candidate.OpenProjectAsync(source.Clone());
                    if (!candidate.CheckThemeApplication().CanApply) { candidate.Dispose(); continue; }
                    if (_disposed || generation != _themeGeneration) { candidate.Dispose(); return; }
                    var previous = _themePlayback; _themePlayback = candidate; _main.PreviewScene(candidate.Scene);
                    if (ThemePlaylistTarget == "D92 USB") await _main.Usb!.ApplyCurrentAsync();
                    else await _main.Devices.ApplySceneAsync(candidate.Scene);
                    previous?.Dispose();
                    if (_disposed || generation != _themeGeneration) return;
                    if (ThemePlaylistTarget == "D92 USB" ? _main.Usb?.HasSelectedOutput != true : !_main.Devices.IsRenderOpen) { StopThemePlaylist(); ThemePlaylistStatus = "输出启动失败，主题轮播已停止。"; return; }
                    await TryRecordThemeUseAsync(source.Id);
                    _themeCurrent = item.ProjectId; ThemePlaylistStatus = $"正在轮播：{source.Name} · {item.Seconds}秒";
                    _themeRemaining = TimeSpan.FromSeconds(item.Seconds); ScheduleThemeSwitch(_themeRemaining); return;
                }
                catch (Exception e) { if (!ReferenceEquals(_themePlayback, candidate)) candidate.Dispose(); _logger.Warning(e, "Skipping unusable theme {Id}", item.ProjectId); }
            }
            StopThemePlaylist(); ThemePlaylistStatus = "没有可用于当前目标的主题；缺失素材、未启用插件或不兼容的条目已跳过。";
        }
        finally { _themeTransition = false; }
    }
    private async void ThemePlaylistTick(object? sender, EventArgs e)
    {
        try
        {
            if (ThemePlaylistTarget == "D92 USB" && _main.Usb?.HasSelectedOutput != true || ThemePlaylistTarget == "Windows 副屏" && !_main.Devices.IsRenderOpen) { StopThemePlaylist(); return; }
            await AdvanceThemePlaylistAsync();
        }
        catch (Exception exception) { StopThemePlaylist(); _logger.Error(exception, "Theme playlist stopped"); }
    }
    public void StopThemePlaylist() { var was=IsThemeRotating;_themeGeneration++; IsThemeRotating = false; _themePaused = false; _themeTimer?.Stop(); _themeStatusTimer?.Stop(); NotifyThemePlayback(); ThemePlaylistStatus = "主题轮播已停止切换，当前输出由设备控制管理。"; OnPropertyChanged(nameof(IsThemeRotating));if(was && ThemePlaylistTarget=="D92 USB" && _main.Usb is {IsRunning:true,SelectedSerial:{Length:>0} serial} usb)_main.RememberLegacyOutput(serial,usb.Scene); }
    private void ObserveThemePlaylist(PropertyChangedEventArgs e) { if (e.PropertyName == nameof(MainViewModel.Scene) && IsThemeRotating && !_themeTransition && !ReferenceEquals(_main.Scene, _themePlayback?.Scene)) StopThemePlaylist(); }
    private void DisposeThemePlaylist() { StopThemePlaylist(); if (_themeTimer is not null) _themeTimer.Tick -= ThemePlaylistTick; if (_themeStatusTimer is not null) _themeStatusTimer.Tick -= ThemeStatusTick; _themePlayback?.Dispose(); }
}
