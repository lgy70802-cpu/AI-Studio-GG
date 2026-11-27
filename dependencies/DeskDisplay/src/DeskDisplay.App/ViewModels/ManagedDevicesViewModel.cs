using System.Collections.ObjectModel;
using System.Windows.Threading;
using DeskDisplay.Core.Adaptation;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Models;
using DeskDisplay.Core.Output;
using DeskDisplay.Core.Themes;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed record DeviceThemeChoice(ThemeProject Project)
{
    public string Name { get; } = Project.Name;
}

public sealed class ManagedDevicesViewModel : ObservableObject, IDisposable
{
    private readonly DeviceRegistry _registry;
    private readonly OutputSessionManager _sessions;
    private readonly DevicePreferenceStore _store;
    private readonly Func<IReadOnlyList<ThemeProject>> _themes;
    private readonly Func<IReadOnlyList<DisplayInfo>> _displays;
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly ILogger _logger;
    private readonly UsbOutputSettings? _legacyUsb;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, ManagedDevicePreference> _saved = new(StringComparer.Ordinal);
    private bool _closing, _busy, _canSave = true;
    private string _status = "";
    private DeviceThemeChoice? _selectedTheme;
    private Task _pending = Task.CompletedTask, _stops = Task.CompletedTask;
    public Task PendingOperation => _pending;
    public Task PendingStops => _stops;
    public ObservableCollection<ManagedDeviceCard> Cards { get; } = [];
    public ManagedDeviceCard? HomeOutputCard {get;private set;}
    public IReadOnlyList<DeviceThemeChoice> Themes { get; private set; } = [];
    public DeviceThemeChoice? SelectedTheme { get => _selectedTheme; set { if (SetProperty(ref _selectedTheme, value)) NotifyCards(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasDevices => Cards.Count > 0;
    public bool IsBusy => _busy || _closing;
    public Func<ThemeDocument, ResolvedLayout, CancellationToken, Task<DeskDisplay.Display.Services.ThemeDevicePreview>>? PreviewFactory { get; set; }
    public Func<Guid, Task>? ThemeUseAccepted { get; set; }
    public Func<ManagedDeviceCard,Func<Task>?>? CaptureLegacyRestore {get;set;}
    public Action<ManagedDeviceCard>? ExplicitStopRequested {get;set;}
    private readonly Dictionary<string,long> _stopVersions=[];
    public ManagedDeviceCard SelectConnectedD92(string? serial)
    {
        var connected=Cards.Where(c=>c.Connected && !c.Mirrored && c.Device.Transport==DeviceTransport.UsbHid && c.Device.ModelKey=="D92").ToArray();
        var selected=connected.Where(c=>!string.IsNullOrEmpty(serial) && c.Device.Identity.SerialNumber==serial).ToArray();
        if(!string.IsNullOrEmpty(serial) && selected.Length==0)throw new InvalidOperationException("所选 D92 未连接；未自动改用其他设备，请重新选择目标。");
        var target=selected.Length==1?selected[0]:connected.Length==1?connected[0]:null;
        if(target is null)throw new InvalidOperationException(connected.Length==0?"没有已连接的 D92，请先连接设备。":"发现多台 D92，请先在设备设置中选择目标设备。");
        if(!target.Confirmed)throw new InvalidOperationException("请先在设备管理中核对并确认此 D92 的输出参数。");
        return target;
    }
    private DeskDisplay.Display.Services.ThemeDevicePreview? _homePreview;
    public Action<ThemeScene>? HomeOutputChanged { get; set; }
    public OutputCoordinator Coordinator { get; set; } = new();
    public Func<ManagedDeviceCard,ThemeProject?,Task>? ContentAccepted { get; set; }
    private readonly Dictionary<string,(ThemeProject Source,LayoutAdaptation Adaptation,DisplayProfile Profile)> _activeSources=[];
    private sealed record PreviewOrigin((ThemeProject Source,LayoutAdaptation Adaptation,DisplayProfile Profile)? Prior,Func<Task>? Legacy,long Revision);
    private readonly Dictionary<string,PreviewOrigin> _previewOrigins=[];
    private string? _focusedOutput;
    public Task<Func<Task>> PreviewTemporaryAsync(ThemeProject source,ManagedDeviceCard card,LayoutAdaptation adaptation,Func<Task>? restoreLegacy)
        => Coordinator.RunAsync("设备预览",()=>PreviewTemporaryCoreAsync(source,card,adaptation,restoreLegacy));
    private async Task<Func<Task>> PreviewTemporaryCoreAsync(ThemeProject source,ManagedDeviceCard card,LayoutAdaptation adaptation,Func<Task>? restoreLegacy)
    {
        var origin=_previewOrigins.GetValueOrDefault(card.DeviceId);
        var prior=card.IsActive && _activeSources.TryGetValue(card.DeviceId,out var running) ? running : ((ThemeProject,LayoutAdaptation,DisplayProfile)?)null;
        var revision=card.AcceptedOutputRevision;
        var legacy=UsesOwnedLegacyOutput(card)?restoreLegacy:null;
        await ApplySourceAsync(source,[card.DeviceId],adaptation,true,true);
        if(card.AcceptedOutputRevision==revision)throw new InvalidOperationException(card.Message);
        _previewOrigins[card.DeviceId]=origin is null ? new(prior,legacy,card.AcceptedOutputRevision) : origin with {Revision=card.AcceptedOutputRevision};
        return ()=>Coordinator.RunAsync("恢复设备内容",async()=>
        {
            if(!_previewOrigins.TryGetValue(card.DeviceId,out var saved) || saved.Revision!=card.AcceptedOutputRevision || !card.IsActive)
                throw new InvalidOperationException("设备内容已改变，未覆盖新的输出。");
            if(saved.Prior is { } original)
            {
                if(GetProfile(card)!=original.Profile)throw new InvalidOperationException("设备参数已改变，请重新选择原内容。");
                var before=card.AcceptedOutputRevision;
                await ApplySourceAsync(original.Source,[card.DeviceId],original.Adaptation,true);
                if(card.AcceptedOutputRevision==before)throw new InvalidOperationException(card.Message);
            }
            else
            {
                await ReleaseForLegacyUsbAsync(card.Device.Identity.SerialNumber??"");
                if(saved.Legacy is { } restore)await restore();
            }
            _previewOrigins.Remove(card.DeviceId);
        });
    }
    private void TickHomePreview()
    {
        try { _homePreview?.Tick(); }
        catch(Exception ex) { _logger.Warning(ex,"Home output preview tick failed"); }
        NotifyHomeVideoControl();
    }
    public bool CanPauseHomeVideo => !_closing && HomeOutputCard is { } card && _sessions.GetVideoControl(card.DeviceId)?.HasVideo == true;
    public bool HomeBackgroundPaused => HomeOutputCard is { } card && _sessions.GetVideoControl(card.DeviceId)?.BackgroundPaused == true;
    public string HomeBackgroundPauseLabel => HomeBackgroundPaused ? "继续背景" : "暂停背景";
    private AsyncCommand? _pauseHomeVideo;
    public AsyncCommand PauseHomeVideoCommand => _pauseHomeVideo ??= new(async () =>
    {
        var card=HomeOutputCard; if(card is null)return;
        await Coordinator.RunAsync("切换视频背景暂停状态", async () =>
        {
            var control=_sessions.GetVideoControl(card.DeviceId)??throw new InvalidOperationException("视频会话已变化。");
            await control.SetBackgroundPausedAsync(!control.BackgroundPaused);
            Coordinator.Report(control.BackgroundPaused?"背景已暂停，时钟和组件继续更新。":"视频背景已继续播放。");
        });
        NotifyHomeVideoControl();
    },()=>CanPauseHomeVideo&&!Coordinator.IsBusy,_logger);
    private void NotifyHomeVideoControl()
    { OnPropertyChanged(nameof(CanPauseHomeVideo));OnPropertyChanged(nameof(HomeBackgroundPaused));OnPropertyChanged(nameof(HomeBackgroundPauseLabel));_pauseHomeVideo?.NotifyCanExecuteChanged(); }
    public void PublishVideoFrame(string serial, Guid themeId, System.Windows.Media.Imaging.BitmapSource frame)
    {
        if (_closing || _homePreview?.Scene is not { } scene || HomeOutputCard?.Device.Identity.SerialNumber != serial || scene.Project.Id != themeId) return;
        if (scene.OutputFrame is null) foreach (var video in scene.Children.Values.OfType<VideoScene>()) video.Playback.Stop();
        scene.SetOutputFrame(new DeskDisplay.Display.DataProviders.WpfSceneImage(frame, frame.PixelWidth, frame.PixelHeight));
    }
    private async Task SyncHomePreviewAsync(ThemeDocument theme, ResolvedLayout layout)
    {
        if(PreviewFactory is null || HomeOutputChanged is null)return;
        try
        {
            var next=await PreviewFactory(theme,layout,_lifetime.Token);
            var previous=_homePreview;_homePreview=next;
            HomeOutputChanged(next.Scene);
            if(previous is not null)await previous.DisposeAsync();
        }
        catch(Exception ex) { _logger.Warning(ex,"Output accepted but home preview could not refresh"); }
    }
    public DisplayProfile GetProfile(ManagedDeviceCard card)
    {
        if (!int.TryParse(card.Width, out var width) || !int.TryParse(card.Height, out var height) || width < 1 || height < 1
            || width > 8192 || height > 8192 || (long)width * height > 16_777_216) throw new InvalidOperationException("请先在设备管理中填写有效的像素尺寸。");
        if (card.Device.Transport == DeviceTransport.SystemDisplay && card.Rotation != 0) throw new InvalidOperationException("系统副屏使用 Windows 方向，额外旋转应为0。");
        var transport = card.Rotation is 90 or 270 ? new PixelSize(height, width) : new PixelSize(width, height);
        var profile = new DisplayProfile("user:" + card.DeviceId, new(width, height), transport, card.Rotation,
            card.Device.ModelKey == "D69" ? D69Compatibility.Capabilities :
            new(CapabilityEvidence.Declared, CapabilityEvidence.Declared, card.Device.ModelKey == "D92" || card.Device.Transport == DeviceTransport.SystemDisplay ? CapabilityEvidence.Declared : CapabilityEvidence.Unknown));
        if (card.Device.ModelKey == "D69") D69Compatibility.Validate(profile);
        profile.Validate(); return profile;
    }
    public Func<ManagedDeviceCard,bool>? HasLegacyOwnedOutput { get; set; }
    public Func<ManagedDeviceCard,Task<bool>>? ReleaseLegacyOwnedOutputAsync { get; set; }
    public bool UsesOwnedLegacyOutput(ManagedDeviceCard? card) => card is not null && card.Device.Identity.Confidence==IdentityConfidence.VerifiedStable
        && !DeviceIdentityRules.RequiresConfirmation(card.Device,_registry.Snapshot.Where(d=>d.IsConnected)) && HasLegacyOwnedOutput?.Invoke(card)==true;
    public Task ReleaseForLegacyUsbAsync(string serial)=>Coordinator.RunAsync("切换设备输出",()=>ReleaseForLegacyUsbCoreAsync(serial));
    public Func<Task>? CaptureManagedRestore(string serial)
    {
        var matches=Cards.Where(c=>c.Device.Identity.SerialNumber==serial&&c.IsActive&&!UsesOwnedLegacyOutput(c)).ToArray();
        if(matches.Length!=1||!_activeSources.TryGetValue(matches[0].DeviceId,out var current))return null;
        var card=matches[0];var revision=card.AcceptedOutputRevision;var source=current.Source.Clone();var adaptation=current.Adaptation;
        return async()=>
        {
            if(card.AcceptedOutputRevision!=revision||card.IsActive)throw new InvalidOperationException("设备已有新内容，未覆盖恢复。");
            await ApplySourceAsync(source,[card.DeviceId],adaptation,true);
            if(card.AcceptedOutputRevision==revision)throw new InvalidOperationException(card.Message);
        };
    }
    private async Task ReleaseForLegacyUsbCoreAsync(string serial)
    {
        if(IsBusy)throw new InvalidOperationException("设备正在处理主题请求，请稍后再试。");
        var matches=Cards.Where(c=>c.Device.Transport==DeviceTransport.UsbHid && c.Device.Identity.SerialNumber==serial).ToArray();
        if(matches.Length==0)return;
        if(matches.Length!=1 || matches[0].Device.Identity.Confidence!=IdentityConfidence.VerifiedStable || DeviceIdentityRules.RequiresConfirmation(matches[0].Device,_registry.Snapshot.Where(d=>d.IsConnected)))
            throw new InvalidOperationException("设备标识不唯一，请先在设备管理中核对目标。");
        var card=matches[0];
        Synchronize();
        if(!card.IsActive)return; // Snapshot also retains terminal history; it is not an owned live session.
        var owned=_sessions.Snapshot.FirstOrDefault(s=>s.Snapshot.DeviceId==card.DeviceId);
        if(owned is null)return; // External leases are never stopped or bypassed.
        if(!await _sessions.StopOwnedAsync(card.DeviceId,owned.Snapshot.SessionId))
            throw new InvalidOperationException("输出会话已变化，请重新应用壁纸。");
        if(_focusedOutput==card.DeviceId)_focusedOutput=null;
        Synchronize();
    }
    public Task ApplySourceAsync(ThemeProject source, IReadOnlyCollection<string> deviceIds, LayoutAdaptation adaptation, bool replaceOwned = false, bool temporary = false)
    {
        var snapshot = source.Clone(); var ids = deviceIds.ToHashSet(StringComparer.Ordinal);
        return Track(async () => { foreach (var card in Cards.Where(c => ids.Contains(c.DeviceId)).ToArray()) await ApplyAsync(card, snapshot, adaptation, replaceOwned,temporary); });
    }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand ApplySelectedCommand { get; }
    public AsyncCommand StopSelectedCommand { get; }

    public ManagedDevicesViewModel(DeviceRegistry registry, OutputSessionManager sessions, DevicePreferenceStore store,
        Func<IReadOnlyList<ThemeProject>> themes, Func<IReadOnlyList<DisplayInfo>> displays,
        Func<CancellationToken, Task> refresh, ILogger logger, UsbOutputSettings? legacyUsb = null)
    {
        _registry = registry; _sessions = sessions; _store = store; _themes = themes; _displays = displays; _refresh = refresh; _logger = logger; _legacyUsb = legacyUsb;
        RefreshCommand = new(() => Track(RefreshAsync), () => !_closing && !_busy, logger);
        SaveCommand = new(() => Track(SaveAsync), () => !_closing && !_busy && _canSave, logger);
        ApplySelectedCommand = new(() => Track(async () =>
        {
            // Snapshot the user's selection and editable theme before the first asynchronous operation.
            var theme = SelectedTheme?.Project.Clone();
            foreach (var card in Cards.Where(c => c.Selected).ToArray()) await ApplyAsync(card, theme);
        }), () => !_closing && !_busy && SelectedTheme is not null && Cards.Any(c => c.Selected), logger);
        StopSelectedCommand = new(() => Track(async () =>
        {
            foreach (var card in Cards.Where(c => c.Selected).ToArray()) await StopAsync(card);
        }), () => !_closing && !_busy && Cards.Any(c => c.Selected && c.IsActive), logger);
        _timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => { Synchronize(); TickHomePreview(); }, Dispatcher.CurrentDispatcher);
        _timer.Stop();
    }

    public async Task InitializeAsync()
    {
        try { foreach (var item in await _store.LoadAsync(_lifetime.Token)) _saved[item.DeviceId] = item; }
        catch (Exception ex) { _canSave = false; Status = "设备设置读取失败，原文件已保留。"; _logger.Warning(ex, "Cannot load managed device preferences"); }
        Synchronize(); _timer.Start();
    }
    public async Task RefreshAsync() { await _refresh(_lifetime.Token); Synchronize(); }
    public void Synchronize()
    {
        if (_closing) return;
        var displays = _displays(); var statuses = _sessions.Snapshot.ToDictionary(s => s.Snapshot.DeviceId);
        foreach (var device in _registry.Snapshot)
        {
            var card = Cards.FirstOrDefault(c => c.DeviceId == device.DeviceId);
            if (card is null)
            {
                _saved.TryGetValue(device.DeviceId, out var preference);
                var monitor = displays.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.DeviceId) && OutputDeviceKeys.WindowsDisplay(d.DeviceId) == device.DeviceId);
                var old = device.ModelKey == "D92" && device.Transport == DeviceTransport.UsbHid && device.Identity.Confidence == IdentityConfidence.VerifiedStable
                    && _legacyUsb?.SerialNumber == device.Identity.SerialNumber ? _legacyUsb : null;
                card = new(this, device, preference, monitor?.Width ?? old?.Width ?? (device.ModelKey == "D69" ? 1280 : 0),
                    monitor?.Height ?? old?.Height ?? (device.ModelKey == "D69" ? 480 : 0),
                    old?.RotationDegrees ?? (device.ModelKey == "D69" ? 90 : 0), _logger);
                Cards.Add(card);
            }
            var mirrored = device.Transport == DeviceTransport.SystemDisplay && (displays.Count(d => d.DeviceName == device.Endpoint.EndpointId) > 1
                || device.DeviceId.StartsWith("ambiguous-display:", StringComparison.Ordinal));
            card.Update(device, statuses.GetValueOrDefault(device.DeviceId), mirrored);
            card.ObserveDisplay(displays.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.DeviceId)
                && OutputDeviceKeys.WindowsDisplay(d.DeviceId) == device.DeviceId));
        }
        foreach(var card in Cards) if(CanReuseConfirmation(card)) card.Confirmed=true;
        var available = _themes().GroupBy(t => t.Id).Select(g => g.First()).ToArray();
        if (!available.Select(p => (p, p.Name)).SequenceEqual(Themes.Select(c => (c.Project, c.Name))))
        {
            var selected = SelectedTheme?.Project.Id;
            Themes = available.Select(p => new DeviceThemeChoice(p)).ToArray();
            OnPropertyChanged(nameof(Themes)); SelectedTheme = Themes.FirstOrDefault(c => c.Project.Id == selected);
        }
        OnPropertyChanged(nameof(HasDevices)); NotifyCards();
        if(_focusedOutput is { } focused && Cards.FirstOrDefault(c=>c.DeviceId==focused) is { } output)
            Coordinator.Report(output.Alias+" · "+output.AppliedTheme+" · "+output.OutputStatus+(string.IsNullOrEmpty(output.Message)?"":" · "+output.Message));
    }

    private bool CanReuseConfirmation(ManagedDeviceCard card) => _saved.TryGetValue(card.DeviceId,out var saved) && saved.ProfileConfirmed
        && card.Connected && !card.Mirrored && !card.HasOutputFault && !card.ConfirmationRevoked && card.Device.Identity.Confidence==IdentityConfidence.VerifiedStable
        && !DeviceIdentityRules.RequiresConfirmation(card.Device,_registry.Snapshot.Where(d=>d.IsConnected))
        && card.Width==saved.Width.ToString() && card.Height==saved.Height.ToString() && card.Rotation==saved.Rotation && card.PhysicalGroup.Trim()==saved.PhysicalGroup;
    private async Task RememberConfirmedProfileAsync(ManagedDeviceCard card, DisplayProfile profile)
    {
        if(!_canSave || card.Device.Identity.Confidence!=IdentityConfidence.VerifiedStable) return;
        var saved=new ManagedDevicePreference(card.DeviceId,card.Alias,profile.SurfaceSize.Width,profile.SurfaceSize.Height,card.Rotation,card.PhysicalGroup.Trim(),true);
        if(_saved.GetValueOrDefault(card.DeviceId)==saved)return;
        try { await _store.SaveAsync([saved],_lifetime.Token); _saved[card.DeviceId]=saved; }
        catch(Exception ex) { _logger.Warning(ex,"Output accepted but confirmed profile could not be remembered");card.Message+=" 设备确认未记住，下次仍需核对。"; }
    }
    internal bool CanApply(ManagedDeviceCard card) => !_closing && !_busy && !card.Busy && card.Connected && !card.IsActive && card.Confirmed && !card.Mirrored && SelectedTheme is not null;
    internal Task ApplyCardAsync(ManagedDeviceCard card) => Track(() => ApplyAsync(card, SelectedTheme?.Project.Clone()));
    internal bool CanStop(ManagedDeviceCard card) => !_closing && card.IsActive;
    internal Task StopCardAsync(ManagedDeviceCard card)
    {
        if (_closing) return Task.CompletedTask;
        var stop = StopAsync(card); _stops = Task.WhenAll(_stops, stop); return stop;
    }
    private Task ApplyAsync(ManagedDeviceCard card, ThemeProject? source, LayoutAdaptation adaptation = LayoutAdaptation.Contain, bool replaceOwned = false,bool temporary=false)
        => Coordinator.RunAsync("应用到 "+card.Alias,()=>ApplyCoreAsync(card,source,adaptation,replaceOwned,temporary));
    private async Task ApplyCoreAsync(ManagedDeviceCard card, ThemeProject? source, LayoutAdaptation adaptation, bool replaceOwned,bool temporary)
    {
        if (source is null) { card.Message = "请先选择已保存的创作主题。";Coordinator.Report(card.Message,true); return; }
        if (!card.Confirmed || !card.Connected || card.Mirrored) { card.Message = "请核对已连接的独立设备并确认参数。";Coordinator.Report(card.Message,true);if(!temporary)Coordinator.SetRetry(()=>ApplySourceAsync(source,[card.DeviceId],adaptation,replaceOwned)); return; }
        card.Busy = true;
        var prior=card.IsActive && !UsesOwnedLegacyOutput(card)?_activeSources.GetValueOrDefault(card.DeviceId):default;
        var legacyRestore=replaceOwned && UsesOwnedLegacyOutput(card)?CaptureLegacyRestore?.Invoke(card):null;
        var released=false;
        var epochBefore=card.Device.Endpoint.ConnectionEpoch;
        var stopVersion=_stopVersions.GetValueOrDefault(card.DeviceId);
        try
        {
            var profile = GetProfile(card);
            if (card.Device.Identity.Confidence == IdentityConfidence.Ambiguous && !_registry.ConfirmEndpoint(card.DeviceId, card.Device.Endpoint.ConnectionEpoch))
                throw new InvalidOperationException("连接已变化，请刷新并重新确认。");
            var theme = LegacyThemeAdapter.Read(source);
            var selection = LayoutSelector.Select(theme, profile.SurfaceSize);
            card.LayoutStatus = selection.Match switch { LayoutMatch.Exact => "比例精确匹配", LayoutMatch.WithinTolerance => "比例容差匹配", _ => "等比完整显示，可能留边" };
            if (card.PhysicalGroup.Trim() != (_saved.GetValueOrDefault(card.DeviceId)?.PhysicalGroup ?? "")) throw new InvalidOperationException("关联组已修改，请先保存设备设置再应用。");
            var legacyOwned=replaceOwned && UsesOwnedLegacyOutput(card);
            if (replaceOwned && (card.IsActive || legacyOwned))
            {
                var owned = _sessions.Snapshot.FirstOrDefault(s => s.Snapshot.DeviceId == card.DeviceId);
                if (owned is null && !legacyOwned) throw new InvalidOperationException("未找到本软件持有的会话，未执行切换。");
                var epoch = card.Device.Endpoint.ConnectionEpoch;
                var resolved = LayoutResolver.Resolve(selection.Layout, profile.SurfaceSize, adaptation);
                if (resolved.Diagnostics.Any(d => d.Code is "LegacyStretchRequiresReview" or "OrientationMismatch" or "UnsatisfiedMinimumMargin")) throw new InvalidOperationException("新布局需要调整，原输出保持运行。");
                var previewFactory = PreviewFactory ?? throw new InvalidOperationException("无法检查新主题，原输出保持运行。");
                // Resolve fonts/assets/plugins before relinquishing the existing output.
                await using (var check = await previewFactory(theme, resolved, _lifetime.Token)) { }
                if (!card.Confirmed || !card.Connected || card.Mirrored || card.Device.Endpoint.ConnectionEpoch != epoch || GetProfile(card) != profile || card.PhysicalGroup.Trim() != (_saved.GetValueOrDefault(card.DeviceId)?.PhysicalGroup ?? ""))
                    throw new InvalidOperationException("设备或参数已变化，未执行切换。");
                if(legacyOwned)
                {
                    if(ReleaseLegacyOwnedOutputAsync is not { } release || !await release(card))throw new InvalidOperationException("原有 USB 输出已变化，未强制停止其他输出。");
                }
                else if (owned is not null && !await _sessions.StopOwnedAsync(card.DeviceId, owned.Snapshot.SessionId))
                    throw new InvalidOperationException("输出会话已变化，未替换新会话，请重试。");
                released=true;
            }
            var result = await _sessions.StartAsync(card.DeviceId, profile, theme, adaptation: adaptation, cancellationToken: _lifetime.Token, conflictGroupId: _saved.GetValueOrDefault(card.DeviceId)?.PhysicalGroup);
            if(result.Started && released)result=await ConfirmReplacementFrameAsync(card,result,stopVersion);
            card.Message = result.Started ? "已启动，等待首帧。" : result.Code switch
            {
                "Busy" => "该设备已有输出，请先停止原会话。", "IdentityNeedsConfirmation" => "设备标识不唯一，请重新确认。",
                "PhysicalDeviceBusy" => "同一物理屏的另一连接正在输出，请先停止该连接。",
                "LayoutNeedsAdjustment" => "布局需要调整，请检查旧拉伸设置或约束。", "DeviceUnavailable" or "ConnectionChanged" => "设备连接已变化，请刷新。",
                "Cancelled" => "已取消启动。",
                _ => result.Code.StartsWith("新内容首帧失败",StringComparison.Ordinal)?result.Code:"未启动，请查看下方设备状态。"
            };
            card.AppliedTheme = result.Started ? source.Name : card.AppliedTheme;
            if(result.Started)card.AcceptedOutputRevision++;
            if(result.Started)
            {
                released=false;
                Coordinator.SetRetry(null);
                _focusedOutput=card.DeviceId;
                _activeSources[card.DeviceId]=(source.Clone(),adaptation,profile);
                if(!temporary)_previewOrigins.Remove(card.DeviceId);
                if(!temporary && ContentAccepted is { } accepted)await accepted(card,source);
            }
            Coordinator.Report(card.Alias+" · "+source.Name+" · "+card.Message,!result.Started);
            if(!result.Started && !temporary && _stopVersions.GetValueOrDefault(card.DeviceId)==stopVersion)Coordinator.SetRetry(()=>ApplySourceAsync(source,[card.DeviceId],adaptation,replaceOwned));
            if(!result.Started && released && _stopVersions.GetValueOrDefault(card.DeviceId)==stopVersion)await RecoverPreviousAsync(card,prior.Source,prior.Adaptation,prior.Profile,legacyRestore,epochBefore);
            if(result.Started){HomeOutputCard=card;await SyncHomePreviewAsync(theme,LayoutResolver.Resolve(selection.Layout,profile.SurfaceSize,adaptation));}
            if (result.Started && replaceOwned) await RememberConfirmedProfileAsync(card,profile);
            if (result.Started && !temporary && ThemeUseAccepted is { } record)
            {
                try { await record(source.Id); }
                catch (Exception ex) { _logger.Warning(ex, "Output started but theme history could not be saved"); card.Message += " 使用记录保存失败。"; }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { card.Message = ex.Message;Coordinator.Report(card.Alias+" · "+ex.Message,true);if(!temporary)Coordinator.SetRetry(()=>ApplySourceAsync(source,[card.DeviceId],adaptation,replaceOwned)); _logger.Warning(ex, "Cannot apply theme to {DeviceId}", card.DeviceId);if(released && _stopVersions.GetValueOrDefault(card.DeviceId)==stopVersion)await RecoverPreviousAsync(card,prior.Source,prior.Adaptation,prior.Profile,legacyRestore,epochBefore); }
        finally { card.Busy = false; Synchronize(); }
    }
    private async Task<OutputStartResult> ConfirmReplacementFrameAsync(ManagedDeviceCard card,OutputStartResult started,long stopVersion)
    {
        var deadline=Environment.TickCount64+5000;string error="等待首帧超时";
        while(Environment.TickCount64<deadline)
        {
            if(_stopVersions.GetValueOrDefault(card.DeviceId)!=stopVersion)return new(false,"Cancelled",started.SessionId);
            var status=_sessions.Snapshot.FirstOrDefault(s=>s.Snapshot.DeviceId==card.DeviceId&&s.Snapshot.SessionId==started.SessionId);
            if(status?.Snapshot.ActualState is OutputActualState.Streaming or OutputActualState.WaitingForDevice)return started;
            if(status?.Snapshot.ActualState is OutputActualState.Failed or OutputActualState.Blocked or OutputActualState.Stopped){error=status.Error??"输出已终止";break;}
            await Task.Delay(20,_lifetime.Token);
        }
        if(started.SessionId is { } id)await _sessions.StopOwnedAsync(card.DeviceId,id);
        return new(false,"新内容首帧失败："+error,started.SessionId);
    }
    private async Task RecoverPreviousAsync(ManagedDeviceCard card,ThemeProject? source,LayoutAdaptation adaptation,DisplayProfile? profile,Func<Task>? legacyRestore,long epoch)
    {
        var failure=card.Message;
        try
        {
            if(!card.Connected || !card.Confirmed || card.Device.Endpoint.ConnectionEpoch!=epoch || source is not null && GetProfile(card)!=profile)throw new InvalidOperationException("设备或参数已变化。");
            if(source is not null && profile is not null)
            {
                var restored=await _sessions.StartAsync(card.DeviceId,profile,LegacyThemeAdapter.Read(source),adaptation:adaptation,cancellationToken:_lifetime.Token,conflictGroupId:_saved.GetValueOrDefault(card.DeviceId)?.PhysicalGroup);
                if(!restored.Started)throw new InvalidOperationException(restored.Code);
                card.AppliedTheme=source.Name;
            }
            else if(legacyRestore is not null)await legacyRestore();
            else throw new InvalidOperationException("没有可恢复的原始来源。");
            card.Message=failure+"；已重新启动原内容。";
        }
        catch(Exception ex){card.Message=failure+"；原内容恢复未完成："+ex.Message;_logger.Warning(ex,"Previous output recovery failed for {DeviceId}",card.DeviceId);}
        Coordinator.Report(card.Alias+" · "+card.Message,true);
    }
    // Stop is an interrupt, not another queued transition: it must cancel asynchronous driver preparation.
    public async Task StopAsync(ManagedDeviceCard card)
    {
        _stopVersions[card.DeviceId]=_stopVersions.GetValueOrDefault(card.DeviceId)+1;
        ExplicitStopRequested?.Invoke(card);
        card.Busy = true;
        try { await _sessions.StopAsync(card.DeviceId); card.Message = "该设备输出已停止。"; if(!_closing && ContentAccepted is { } accepted)await accepted(card,null);Coordinator.Report(card.Alias+" · 输出已停止"); }
        catch (Exception ex) { card.Message = OutputTroubleshooting.DescribeFailure("停止输出",ex.Message);Coordinator.Report(card.Alias+" · "+card.Message,true); _logger.Warning(ex, "Cannot stop {DeviceId}", card.DeviceId); }
        finally { card.Busy = false; Synchronize(); }
    }
    public async Task SaveAsync()
    {
        if (Cards.Any(c => c.IsActive) && Cards.Any(c => c.PhysicalGroup.Trim() != (_saved.GetValueOrDefault(c.DeviceId)?.PhysicalGroup ?? ""))) { Status = "请先停止全部输出后修改设备关联组。"; return; }
        if (!_canSave) { Status = "原设备设置不可读取，未覆盖。"; return; }
        try
        {
            var merged = new Dictionary<string, ManagedDevicePreference>(_saved, StringComparer.Ordinal);
            foreach (var card in Cards)
            {
                if (!int.TryParse(card.Width, out var width) || !int.TryParse(card.Height, out var height)) throw new InvalidOperationException("请填写整数像素尺寸后保存。");
                merged[card.DeviceId] = new(card.DeviceId, card.Alias.Trim(), width, height, card.Rotation, card.PhysicalGroup.Trim(), CanReuseConfirmation(card));
            }
            await _store.SaveAsync(merged.Values.ToArray(), _lifetime.Token);
            _saved.Clear(); foreach (var item in merged) _saved[item.Key] = item.Value;
            Status = "设备名称和输出参数已保存；不会自动启动输出。";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Status = "保存失败：" + ex.Message; _logger.Warning(ex, "Cannot save managed devices"); }
    }
    private Task Track(Func<Task> action)
    {
        if (_busy || _closing) return Task.CompletedTask;
        _busy = true; NotifyCards(); return _pending = ExecuteAsync(action);
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Status = "操作失败，请刷新后重试。"; _logger.Warning(ex, "Managed device operation failed"); }
        finally { _busy = false; NotifyCards(); }
    }
    private void NotifyCards()
    {
        OnPropertyChanged(nameof(IsBusy));
        foreach (var card in Cards) card.Notify();
        RefreshCommand.NotifyCanExecuteChanged(); SaveCommand.NotifyCanExecuteChanged(); ApplySelectedCommand.NotifyCanExecuteChanged(); StopSelectedCommand.NotifyCanExecuteChanged();
    }
    internal void SelectionChanged() { ApplySelectedCommand.NotifyCanExecuteChanged(); StopSelectedCommand.NotifyCanExecuteChanged(); }
    public async Task PrepareForCloseAsync() { _closing = true; _timer.Stop(); _lifetime.Cancel(); await Task.WhenAll(_pending, _stops); await ReleaseHomePreviewAsync(); NotifyCards(); }
    public void Dispose() { _closing = true; _timer.Stop(); _lifetime.Cancel(); _ = ReleaseHomePreviewAsync(); }
    private async Task ReleaseHomePreviewAsync()
    {
        var previous=_homePreview;_homePreview=null;
        try { if(previous is not null)await previous.DisposeAsync(); }
        catch(Exception ex) { _logger.Warning(ex,"Cannot release home output preview"); }
    }
}

public sealed class ManagedDeviceCard : ObservableObject
{
    internal long AcceptedOutputRevision { get; set; }
    private readonly ManagedDevicesViewModel _owner;
    private string _alias, _width, _height, _message = "", _appliedTheme = "未应用主题", _layoutStatus = "";
    private bool _selected, _confirmed, _busy, _confirmationRevoked;
    internal bool ConfirmationRevoked => _confirmationRevoked;
    private void ClearConnectionConfirmation() { if (SetProperty(ref _confirmed,false,nameof(Confirmed))) Notify(); }
    private string _physicalGroup = "";
    public string PhysicalGroup { get => _physicalGroup; set { if (SetProperty(ref _physicalGroup, value)) Confirmed = false; } }
    private int _rotation;
    private OutputActualState? _state;
    private Guid? _lastSession;
    public DeviceDescriptor Device { get; private set; }
    public string DeviceId => Device.DeviceId;
    public string Model => Device.ModelKey;
    public string CapabilityNotice => Device.ModelKey == "D69"
        ? "D69 实验支持：横屏静态壁纸与数字时钟，每秒更新。停止后可能返回设备 Logo；视频、其他方向与重连尚待实机验收。"
        : "";
    public string PreviewLabel => $"{Alias} · {Width}×{Height} · {IdentityLabel}";
    public string IdentityLabel => Device.Identity.SerialNumber is { Length: > 0 } serial ? serial.Length > 8 ? "…" + serial[^8..] : serial : DeviceId;
    public string Connection => Device.Transport == DeviceTransport.UsbHid ? "USB HID" : "Windows 副屏";
    public bool Connected => Device.IsConnected;
    public string ConnectionStatus => Connected ? "已连接" : "未连接";
    public string Orientation => int.TryParse(Width, out var width) && int.TryParse(Height, out var height) && width > 0 && height > 0 ? width >= height ? "横屏" : "竖屏" : "方向待设置";
    public string IdentityStatus => Device.Identity.Confidence switch { IdentityConfidence.VerifiedStable => "唯一标识", IdentityConfidence.UserConfirmed => "当前连接已人工确认", _ => "标识不唯一，确认仅适用于当前连接" };
    public bool Mirrored { get; private set; }
    public bool Selected { get => _selected; set { if (SetProperty(ref _selected, value)) _owner.SelectionChanged(); } }
    public bool Confirmed { get => _confirmed; set { if (SetProperty(ref _confirmed, value)) { _confirmationRevoked=!value; Notify(); } } }
    public string Alias { get => _alias; set { if (SetProperty(ref _alias, value)) OnPropertyChanged(nameof(PreviewLabel)); } }
    public string Width { get => _width; set { if (SetProperty(ref _width, value)) { Confirmed = false; OnPropertyChanged(nameof(Orientation)); OnPropertyChanged(nameof(PreviewLabel)); } } }
    public string Height { get => _height; set { if (SetProperty(ref _height, value)) { Confirmed = false; OnPropertyChanged(nameof(Orientation)); OnPropertyChanged(nameof(PreviewLabel)); } } }
    public int Rotation { get => _rotation; set { if (SetProperty(ref _rotation, value)) Confirmed = false; } }
    public IReadOnlyList<int> Rotations { get; } = [0, 90, 180, 270];
    public bool CanEdit => !Busy && !IsActive;
    public bool CanEditGeometry => CanEdit && Device.Transport == DeviceTransport.UsbHid;
    public bool CanConfirm => CanEdit && Connected && !Mirrored;
    public bool Busy { get => _busy; internal set { if (SetProperty(ref _busy, value)) Notify(); } }
    internal bool HasOutputFault => _state is OutputActualState.Failed or OutputActualState.Blocked;
    public bool IsActive => _state is OutputActualState.Preparing or OutputActualState.Starting or OutputActualState.Streaming or OutputActualState.WaitingForDevice or OutputActualState.Stopping;
    public string AppliedTheme { get => _appliedTheme; internal set => SetProperty(ref _appliedTheme, value); }
    public string LayoutStatus { get => _layoutStatus; internal set => SetProperty(ref _layoutStatus, value); }
    public string Message { get => _message; internal set => SetProperty(ref _message, value); }
    public string OutputStatus => _state switch
    {
        OutputActualState.Preparing => "正在准备", OutputActualState.Starting => "等待首帧", OutputActualState.Streaming => "正在输出",
        OutputActualState.WaitingForDevice => "等待原设备恢复", OutputActualState.Stopping => "正在停止", OutputActualState.Failed => "输出失败",
        OutputActualState.Blocked => "设备被占用或阻止", _ => "未输出"
    };
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand StopCommand { get; }
    public ManagedDeviceCard(ManagedDevicesViewModel owner, DeviceDescriptor device, ManagedDevicePreference? preference,
        int width, int height, int rotation, ILogger logger)
    {
        _owner = owner; _physicalGroup = preference?.PhysicalGroup ?? ""; Device = device; _alias = preference?.Alias ?? device.ModelKey;
        _width = (preference?.Width ?? width).ToString(); _height = (preference?.Height ?? height).ToString(); _rotation = preference?.Rotation ?? rotation;
        ApplyCommand = new(() => owner.ApplyCardAsync(this), () => owner.CanApply(this), logger);
        StopCommand = new(() => owner.StopCardAsync(this), () => owner.CanStop(this), logger);
    }
    internal void Update(DeviceDescriptor device, ManagedOutputStatus? status, bool mirrored)
    {
        if (Device.Endpoint.ConnectionEpoch != device.Endpoint.ConnectionEpoch || !device.IsConnected) ClearConnectionConfirmation();
        var newError = _state != status?.Snapshot.ActualState || _lastSession != status?.Snapshot.SessionId;
        Device = device; Mirrored = mirrored; _state = status?.Snapshot.ActualState; _lastSession = status?.Snapshot.SessionId;
        OnPropertyChanged(nameof(IdentityLabel)); OnPropertyChanged(nameof(PreviewLabel));
        if (newError && (_state is OutputActualState.Failed or OutputActualState.Blocked)) { Confirmed = false; if (status?.Error is { } error) Message = error; }
        if (_state == OutputActualState.Streaming && Message == "已启动，等待首帧。") Message = "";
        if (mirrored) Message = "系统显示源有歧义，无法独立输出。请在 Windows 中确认扩展模式后刷新。";
        foreach (var name in new[] { nameof(Connected), nameof(ConnectionStatus), nameof(IdentityStatus), nameof(OutputStatus), nameof(IsActive), nameof(CanEdit), nameof(Mirrored) }) OnPropertyChanged(name);
    }
    internal void ObserveDisplay(DisplayInfo? display)
    {
        if (Device.Transport != DeviceTransport.SystemDisplay || IsActive || display is null) return;
        Width = display.Width.ToString(); Height = display.Height.ToString(); Rotation = 0;
    }
    internal void Notify()
    {
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditGeometry)); OnPropertyChanged(nameof(CanConfirm));
        ApplyCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged();
    }
}




