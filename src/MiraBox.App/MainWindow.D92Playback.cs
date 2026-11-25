using System.IO;
using System.Windows;
using DeskDisplay.Core.Models;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record HardwareStill(byte[] Bytes, string Name, string? ResourceId = null, DateTimeOffset? ClockTime = null)
    {
        public string Mode => ClockTime.HasValue ? "clock" : "wallpaper";
    }
    HardwareStill? hardwareStill;
    HardwareOutputSnapshot? stoppedHardwareOutput;
    HardwareStill? stoppedHardwareStill;
    bool CanResumeHardware => hardwareOutput == null &&
        (stoppedHardwareOutput?.Serial == hardwareApplySerial && stoppedHardwareOutput != null ||
         !preferences.Restore && pendingHardwareRestore?.Serial == hardwareApplySerial && pendingHardwareRestore != null);

    void ResumeStoppedHardware()
    {
        if (!CanResumeHardware || (stoppedHardwareOutput ?? pendingHardwareRestore) is not { } saved) return;
        if (saved.BaseResourceId is { } id && state.Resources.TryGetValue(id, out var resource))
        {
            if (HardwareApplyIssue(resource) is { } issue) { Notice.Text = issue; return; }
            UseHardwareStill(resource);
        }
        if (saved.VideoResourceId is { } videoId && state.Resources.TryGetValue(videoId, out var video))
        {
            if (HardwareApplyIssue(video) is { } issue) { Notice.Text = issue; return; }
            PlayHardwareVideo(video);
        }
        SaveWorkspace();
        Render();
    }
    sealed record HardwareVideoRequest(string Id, string Path, string Name, UsbVideoPreferences Profile);
    string? hardwareVideoResourceId;
    TimeSpan? hardwarePreviewEnds;
    string? hardwarePreviewResourceId;
    string? hardwarePlaylistId;
    string? hardwarePlaylistItem;
    TimeSpan hardwarePlaylistRemaining;
    int hardwarePlaylistIndex;
    bool hardwarePlaylistPaused;
    readonly HashSet<string> hardwareFailedPlaylistItems = [];
    internal Func<TimeSpan>? HardwareClock { get; set; }
    TimeSpan HardwareNow => HardwareClock?.Invoke() ?? watch.Elapsed;

    bool HardwarePlaybackSelected => hardwareApplySerial != null;
    bool IsHardwarePlaylist(Playlist playlist) => hardwarePlaylistId == playlist.Id && !hardwarePlaylistPaused;
    bool HasHardwarePlaylist => hardwarePlaylistId != null;
    bool HardwareSending => hardwareOutput is { Identification: false };
    bool HardwareVideo => hardwareOutput?.VideoPath != null;
    bool HardwareVideoPaused => HardwareVideo && hardwareOutput!.VideoPaused;
    bool CanToggleHardwareVideo => HardwareVideo && hardwareOutput is
        { VideoHasWritten: true, IsStopping: false } output && output.Serial == hardwareApplySerial;
    UsbVideoPreferences D92VideoProfile => HardwareModel == "D69" ?
        new(ValidatedD69VideoFps, true, 80) : (HardwareModel == "D92" && hardwareApplySerial is { } serial &&
        Enum.TryParse<VideoPlaybackMode>(hardwareOrientations.GetOptions(serial).VideoMode, out var mode)
            ? mode : preferences.D92VideoMode) == VideoPlaybackMode.Smooth
        ? D92StaticOutput.SmoothVideoProfile : D92StaticOutput.VideoProfile;
    int ValidatedD69VideoFps => hardwareApplySerial is { } serial &&
        hardwareOrientations.GetOptions(serial).D69VideoFps is 5 or 10 or 20 or 24 ?
        hardwareOrientations.GetOptions(serial).D69VideoFps : 20;

    bool IsHardwareWallpaper(Resource resource) => resource.Local && Entry(resource.Id).Type == "壁纸";

    bool IsHardwareTheme(Resource resource) => Entry(resource.Id).Type is "主题" or "时钟";

    MiraBox.Core.Size HardwareCanvas
    {
        get
        {
            var geometry = D92StaticImage.Geometry(hardwareApplyRotation, HardwareModel);
            return new(geometry.Width, geometry.Height);
        }
    }

    int HardwareBrowseRotation => HardwareCanvas.Width < HardwareCanvas.Height ? 90 : 0;
    string HardwareMismatch => $"内容与 {HardwareModel} 当前方向不适配";

    bool IsHardwareClock(Resource resource) => resource.Local &&
        Workspace.BestLayout(resource, HardwareCanvas) is { Loss: <= .05 } match &&
        (state.Themes.GetValueOrDefault(resource.Id)?.Layouts.ElementAtOrDefault(match.Index)?.FlipClock != null ||
         state.Themes.GetValueOrDefault(resource.Id)?.EditorData != null ||
         state.Themes.GetValueOrDefault(resource.Id)?.CraftPlugin != null);

    bool IsHardwareStill(Resource resource) => IsHardwareWallpaper(resource) ||
        resource.Local && IsHardwareTheme(resource) && Workspace.Difference(resource, HardwareCanvas) is <= .05;

    bool IsHardwareVideo(Resource resource) => resource.Local &&
        (Entry(resource.Id).Type == "视频" && library.MediaPath(Entry(resource.Id)) != null || ThemeVideoPath(resource) != null);

    string? HardwareApplyIssue(Resource resource)
    {
        if (!resource.Local) return "目标已删除";
        if (IsHardwareTheme(resource) && Workspace.Difference(resource, HardwareCanvas) is not <= .05)
            return HardwareMismatch;
        var video = IsHardwareVideo(resource);
        if (!IsHardwareStill(resource) && !video) return "D92 无法播放此内容";
        if (!StartupResourceAvailable(resource)) return video ? "视频文件已丢失" : "素材文件已丢失";
        return null;
    }

    string? HardwarePlaylistIssue(Resource resource) => HardwareApplyIssue(resource) ??
        (Workspace.Difference(resource, HardwareCanvas) is <= .05 ? null : HardwareMismatch);

    byte[] EncodeHardwareStill(Resource resource, int rotation, DateTimeOffset? clockTime = null)
    {
        if (state.Themes.GetValueOrDefault(resource.Id) is { CraftPlugin: not null } craft)
            return EncodeWoodland(craft, rotation);
        if (IsHardwareWallpaper(resource) && images.TryGetValue(resource.Id, out var file))
            return D92StaticImage.Encode(rotation, (context, dimensions) => {
                var original = resource.Layouts.FirstOrDefault();
                var decodeWidth = Math.Max((int)dimensions.Width, (int)dimensions.Height);
                if (original.Width > 0 && original.Height > 0)
                {
                    var decodeScale = HardwareMediaFit == "cover" ? Math.Max(dimensions.Width/original.Width, dimensions.Height/original.Height)
                        : Math.Min(dimensions.Width/original.Width, dimensions.Height/original.Height);
                    decodeWidth = Math.Max(1, (int)Math.Ceiling(original.Width*Math.Min(1,decodeScale)));
                }
                var bitmap = Load(file, decodeWidth);
                var scale = HardwareMediaFit == "cover" ? Math.Max(dimensions.Width / bitmap.PixelWidth, dimensions.Height / bitmap.PixelHeight)
                    : Math.Min(dimensions.Width / bitmap.PixelWidth, dimensions.Height / bitmap.PixelHeight);
                var width = bitmap.PixelWidth * scale; var height = bitmap.PixelHeight * scale;
                context.PushClip(new System.Windows.Media.RectangleGeometry(new Rect(dimensions)));
                context.DrawImage(bitmap, new Rect((dimensions.Width-width)/2, (dimensions.Height-height)/2, width, height));
                context.Pop();
            }, HardwareModel);

        return D92StaticImage.Encode(rotation, (context, size) =>
        {
            var canvas = new MiraBox.Core.Size((int)size.Width, (int)size.Height);
            var view = Thumbnail(resource, canvas, size.Width, size.Height, clockTime: clockTime);
            view.Measure(size);
            view.Arrange(new Rect(size));
            context.DrawRectangle(new System.Windows.Media.VisualBrush(view), null, new Rect(size));
        }, HardwareModel);
    }

    void UseHardwareStill(Resource resource, bool fromPlaylist = false, bool recordUse = true)
    {
        var serial = hardwareApplySerial ?? throw new InvalidOperationException("请先选择 D92 实机。");
        var rotation = hardwareOrientations.Get(serial);
        var still = PrepareHardwareStill(resource, rotation);
        StartHardwareOutput(serial, rotation, still.Bytes, still.Name, resourceId: still.ResourceId, clockTime: still.ClockTime,
            recordUse: recordUse);
        if (!fromPlaylist) PauseHardwarePlaylist();
        Notice.Text = page == "首页" ? "" : $"正在向 {HardwareTargetLabel} 发送「{resource.Name}」。";
    }

    HardwareStill PrepareHardwareStill(Resource resource, int rotation)
    {
        var clockTime = IsHardwareClock(resource) ? state.LocalTime : (DateTimeOffset?)null;
        var jpeg = EncodeHardwareStill(resource, rotation, clockTime);
        return new(jpeg, resource.Name, resource.Id, clockTime);
    }

    void PreviewHardwareStill(Resource resource)
    {
        if (!CanPreviewHardware(resource))
        {
            Notice.Text = HardwareApplyIssue(resource) ?? "请先在实机应用壁纸或主题。视频播放期间可先恢复之前的内容。";
            return;
        }

        var output = hardwareOutput!;
        var jpeg = EncodeHardwareStill(resource, output.Rotation, IsHardwareClock(resource) ? state.LocalTime : null);
        output.SubmitStill(jpeg, resource.Name, "preview");
        hardwarePreviewEnds = HardwareNow + TimeSpan.FromSeconds(15);
        hardwarePreviewResourceId = resource.Id;
        hardwareContent = $"预览「{resource.Name}」";
        RenderHardwareStatus();
    }

    bool CanPreviewHardware(Resource resource) => IsHardwareStill(resource) &&
        hardwareOutput is { Identification: false, IsStopping: false } output && output.Serial == hardwareApplySerial &&
        hardwareStill != null && !HardwareVideo && StartupResourceAvailable(resource);

    void RestoreHardwareStill()
    {
        if (RestoreEditorPreview()) return;
        hardwarePreviewEnds = null;
        hardwarePreviewResourceId = null;
        if (hardwareStill == null || hardwareOutput is not { Identification: false } output) return;
        hardwareVideoResourceId = null;
        var still = RefreshClockFrame(hardwareStill, output.Rotation);
        hardwareStill = still;
        output.SubmitStill(still.Bytes, still.Name, still.Mode);
        hardwareContent = still.Name;
        RenderHardwareStatus();
    }

    HardwareStill RefreshClockFrame(HardwareStill still, int rotation)
    {
        if (still.ClockTime is not { } last || still.ResourceId is not { } id) return still;
        if (state.Themes.GetValueOrDefault(id) is { CraftPlugin: not null } craft)
        {
            try
            {
                var bytes = EncodeWoodland(craft, rotation);
                return ReferenceEquals(bytes, still.Bytes) ? still : still with { Bytes = bytes, ClockTime = state.LocalTime };
            }
            catch (Exception ex) { Notice.Text = "Woodland Clock 更新失败，已保留上一帧：" + ex.Message; return still; }
        }
        var now = state.LocalTime;
        var seconds = state.Resources.TryGetValue(id, out var clockResource) && ClockRefreshesEverySecond(clockResource);
        if (last.Date == now.Date && last.Hour == now.Hour && last.Minute == now.Minute &&
            (!seconds || last.Second == now.Second)) return still;
        if (!state.Resources.TryGetValue(id, out var theme) || !IsHardwareClock(theme) || !StartupResourceAvailable(theme))
            return still with { ClockTime = now };
        try { return still with { Bytes = EncodeHardwareStill(theme, rotation, now), ClockTime = now }; }
        catch (Exception ex)
        {
            Notice.Text = "D92 时钟更新失败，已保留上一帧：" + ex.Message;
            return still with { ClockTime = now };
        }
    }

    bool ClockRefreshesEverySecond(Resource resource)
    {
        if (HardwareCanvas.Width < HardwareCanvas.Height ||
            state.Themes.GetValueOrDefault(resource.Id)?.EditorData is not { } data ||
            Workspace.BestLayout(resource, HardwareCanvas) is not { } match) return false;
        var layout = data.GetProperty("layouts")[match.Index];
        var follows = match.Index > 0 && JsFlag(layout, "follow");
        var elements = match.Index == 0 || follows || !layout.TryGetProperty("elements", out _) ?
            data.GetProperty("master") : layout.GetProperty("elements");
        return elements.EnumerateArray().Any(element =>
        {
            var hidden = JsFlag(element, "hide");
            if (follows && layout.TryGetProperty("vis", out var visibility) &&
                visibility.TryGetProperty(JsText(element, "id"), out var flag)) hidden = flag.GetBoolean();
            return !hidden && JsText(element, "kind") == "clock" && JsFlag(element, "sec");
        });
    }

    void TickHardwareClock()
    {
        if (hardwareStill is not { ClockTime: not null } still ||
            hardwareOutput is not { Identification: false, IsStopping: false } output ||
            output.Serial != hardwareApplySerial || HardwareVideo || hardwarePreviewEnds != null) return;
        var id = output.SubmissionId;
        var current = RefreshClockFrame(still, output.Rotation);
        if (ReferenceEquals(current, still)) return;
        if (ReferenceEquals(current.Bytes, still.Bytes) || output.RefreshStill(id, current.Bytes))
            hardwareStill = current;
    }

    HardwareOutputSnapshot? CaptureHardwareOutput() =>
        hardwareApplySerial is { } serial && editorPreviewReturns.TryGetValue(serial, out var preview)
            ? preview.Running ? preview.Snapshot : null :
        hardwareOutput is { Identification: false, IsStopping: false } output &&
        output.Serial == hardwareApplySerial && (hardwareStill?.ResourceId != null || HardwareVideo && hardwareVideoResourceId != null)
            ? new(output.Serial, hardwareStill?.ResourceId, HardwareVideo ? hardwareVideoResourceId : null)
            : pendingHardwareRestore;

    void EndHardwarePreviewOrVideo()
    {
        if (HardwareVideo) PauseHardwarePlaylist();
        RestoreHardwareStill();
    }

    void RestoreHardwareFromFooter(object sender, RoutedEventArgs e) => EndHardwarePreviewOrVideo();

    void PlayHardwareVideo(Resource resource, bool fromPlaylist = false, bool recordUse = true)
    {
        var path = ThemeVideoPath(resource) ?? library.MediaPath(Entry(resource.Id));
        if (path == null || !File.Exists(path))
            throw new FileNotFoundException("视频文件已丢失。");
        var serial = hardwareApplySerial ?? throw new InvalidOperationException("请先选择 D92 实机。");
        var rotation = hardwareOrientations.Get(serial);
        var still = hardwareStill;
        if (still == null && pendingHardwareRestore?.BaseResourceId is { } baseId &&
            state.Resources.TryGetValue(baseId, out var pendingBase) &&
            IsHardwareStill(pendingBase) && StartupResourceAvailable(pendingBase))
            still = PrepareHardwareStill(pendingBase, rotation);
        still ??= new HardwareStill(D92StaticImage.Encode(rotation, (_, _) => { }, HardwareModel), "空白画面");
        var request = new HardwareVideoRequest(resource.Id, path, resource.Name, D92VideoProfile);

        StartHardwareOutput(serial, rotation, still.Bytes, still.Name, resourceId: still.ResourceId,
            clockTime: still.ClockTime, video: request, recordUse: recordUse);
        if (!fromPlaylist) PauseHardwarePlaylist();
        Notice.Text = $"{HardwareTargetLabel} 正在准备视频 · 目标 {request.Profile.FramesPerSecond} FPS · JPEG Q{request.Profile.JpegQuality}";
    }

    void UseHardwareContent(Resource resource, bool fromPlaylist = false)
    {
        if (IsHardwareVideo(resource)) PlayHardwareVideo(resource, fromPlaylist);
        else UseHardwareStill(resource, fromPlaylist);
    }

    PlaylistItem[] HardwarePlaylistItems(Playlist playlist) => playlist.Items.Where(item =>
        state.Resources.TryGetValue(item.ResourceId, out var resource) && HardwarePlaylistIssue(resource) == null).ToArray();

    PlaylistItem[] AvailableHardwarePlaylistItems(Playlist playlist) => HardwarePlaylistItems(playlist)
        .Where(item => !hardwareFailedPlaylistItems.Contains(item.ResourceId)).ToArray();

    void ToggleHardwarePlaylist(Playlist playlist)
    {
        if (hardwarePlaylistId == playlist.Id) ToggleHardwarePause();
        else StartHardwarePlaylist(playlist);
    }

    void StartHardwarePlaylist(Playlist playlist)
    {
        if (IsHardwareSchedule(playlist.DeviceId) && playlist.DeviceId != HardwareSchedulePrefix + hardwareApplySerial)
            throw new InvalidOperationException("此轮播属于另一台 D92，请先切换输出设备。");
        var items = HardwarePlaylistItems(playlist);
        if (items.Length == 0) throw new InvalidOperationException("轮播没有适合 D92 当前方向且可用的本地内容。");
        hardwareFailedPlaylistItems.Clear();
        ShowHardwarePlaylistItem(playlist, items[0]);
        if (!IsHardwareSchedule(playlist.DeviceId))
            playlist.Assign(HardwareSchedulePrefix + hardwareApplySerial);
        playlist.LastUsed = DateTimeOffset.Now;
        Render();
    }

    void PauseHardwarePlaylist()
    {
        hardwarePlaylistId = null;
        hardwarePlaylistItem = null;
        hardwarePlaylistIndex = 0;
        hardwarePlaylistPaused = false;
        hardwareFailedPlaylistItems.Clear();
        RenderHardwareStatus();
    }

    void StepHardwarePlaylist(int direction, bool automatic = false)
    {
        var playlist = state.Playlists.Find(p => p.Id == hardwarePlaylistId);
        if (playlist == null) { PauseHardwarePlaylist(); return; }
        var items = AvailableHardwarePlaylistItems(playlist);
        if (items.Length == 0) { PauseHardwarePlaylist(); return; }
        var index = Array.FindIndex(items, item => item.ResourceId == hardwarePlaylistItem);
        var next = index < 0 ? 0 : (index + direction + items.Length) % items.Length;
        if (automatic && playlist.Shuffle && items.Length > 1 && index >= 0)
            next = (index + Random.Shared.Next(1, items.Length)) % items.Length;
        if (automatic && next == index)
        {
            hardwarePlaylistRemaining = HardwarePlaylistDuration(items[index]);
            return;
        }
        ShowHardwarePlaylistItem(playlist, items[next]);
    }

    void ShowHardwarePlaylistItem(Playlist playlist, PlaylistItem item)
    {
        UseHardwareContent(state.Resources[item.ResourceId], fromPlaylist: true);
        hardwarePlaylistId = playlist.Id;
        hardwarePlaylistItem = item.ResourceId;
        hardwarePlaylistIndex = Array.IndexOf(AvailableHardwarePlaylistItems(playlist), item);
        hardwarePlaylistRemaining = HardwarePlaylistDuration(item);
        RenderHardwareStatus();
    }

    static TimeSpan HardwarePlaylistDuration(PlaylistItem item) =>
        TimeSpan.FromSeconds(Math.Clamp(item.Duration.TotalSeconds, 10, 86400));

    void TickHardwarePlayback(TimeSpan elapsed)
    {
        if (hardwarePreviewEnds is { } end)
        {
            if (HardwareNow >= end) RestoreHardwareStill();
            else if (hardwarePreviewResourceId is { } previewId &&
                state.Themes.GetValueOrDefault(previewId) is { CraftPlugin: not null } craft &&
                hardwareOutput is { IsStopping: false } output)
            {
                try
                {
                    var bytes = EncodeWoodland(craft, output.Rotation);
                    if (!ReferenceEquals(bytes, output.CurrentStill)) output.SubmitStill(bytes, ResourceName(previewId), "preview");
                }
                catch (Exception ex) { Notice.Text = "Woodland Clock 预览更新失败：" + ex.Message; }
            }
            return;
        }

        TickHardwareClock();
        if (!HasHardwarePlaylist || !HardwareSending || hardwareStatus != "正在输出" || HardwareVideoPaused) return;
        try
        {
            var playlist = state.Playlists.Find(list => list.Id == hardwarePlaylistId);
            if (playlist == null) { PauseHardwarePlaylist(); return; }
            var items = AvailableHardwarePlaylistItems(playlist);
            if (items.Length == 0) { PauseHardwarePlaylist(); return; }
            var index = Array.FindIndex(items, item => item.ResourceId == hardwarePlaylistItem);
            if (index < 0)
            {
                if (!hardwarePlaylistPaused) ShowHardwarePlaylistItem(playlist, items[Math.Max(0, hardwarePlaylistIndex) % items.Length]);
                return;
            }
            hardwarePlaylistIndex = index;
            if (hardwarePlaylistPaused) return;
            hardwarePlaylistRemaining -= elapsed;
            if (hardwarePlaylistRemaining > TimeSpan.Zero) return;
            StepHardwarePlaylist(1, automatic: true);
        }
        catch (Exception ex)
        {
            PauseHardwarePlaylist();
            Notice.Text = "D92 实机轮播已暂停：" + ex.Message;
        }
    }

    void ToggleHardwarePause()
    {
        if (!HasHardwarePlaylist) return;
        hardwarePlaylistPaused = !hardwarePlaylistPaused;
        RenderHardwareStatus();
    }

    void ToggleHardwareVideoPause()
    {
        if (!CanToggleHardwareVideo || hardwareOutput is not { } output) return;
        if (!output.SetVideoPaused(!output.VideoPaused)) return;
        RenderHardwareStatus();
        RenderOutputs();
        playlistLastPaint = TimeSpan.MinValue;
        UpdatePlaylistStatus();
    }

    void PauseHardwareVideoFromFooter(object sender, RoutedEventArgs e) => ToggleHardwareVideoPause();

    static string PlaybackTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalHours >= 1 ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}" :
            $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }

    string? HardwareVideoProgressText(bool showPause = true)
    {
        if (!HardwareVideo || hardwareOutput is not { } output) return null;
        var progress = output.VideoProgress;
        var total = progress?.Duration;
        if (total == null && hardwareVideoResourceId is { } id)
            total = library.Entries.FirstOrDefault(entry => entry.Resource.Id == id)?.Duration;
        var time = PlaybackTime(progress?.Position ?? TimeSpan.Zero);
        return (showPause && HardwareVideoPaused ? "视频已暂停" : "视频") + " " + time +
            (total is { } length ? " / " + PlaybackTime(length) : "");
    }

    string HardwareOutputProgress() => string.Join(" · ",
        new[] { HardwareVideoProgressText(), HardwarePlaybackProgress() }.OfType<string>());

    void HandleHardwareVideoFailure(string? error)
    {
        if (hardwareVideoResourceId == null || HardwareVideo) return;
        var failed = hardwareVideoResourceId;
        RestoreHardwareStill();
        if (HasHardwarePlaylist)
        {
            hardwareFailedPlaylistItems.Add(failed);
            if (!hardwarePlaylistPaused && state.Playlists.Find(list => list.Id == hardwarePlaylistId) is { } playlist)
            {
                var remaining = AvailableHardwarePlaylistItems(playlist);
                if (remaining.Length == 0) PauseHardwarePlaylist();
                else
                {
                    try { ShowHardwarePlaylistItem(playlist, remaining[Math.Max(0, hardwarePlaylistIndex) % remaining.Length]); }
                    catch (Exception ex) { PauseHardwarePlaylist(); error += "；继续轮播失败：" + ex.Message; }
                }
            }
        }
        Notice.Text = "视频播放失败：" + error;
    }

    string? HardwarePlaybackProgress()
    {
        if (hardwarePreviewEnds is { } end)
            return $"临时预览 · 剩余 {Math.Max(0, Math.Ceiling((end - HardwareNow).TotalSeconds))} 秒";
        var playlist = state.Playlists.Find(list => list.Id == hardwarePlaylistId);
        if (playlist == null) return null;
        var items = AvailableHardwarePlaylistItems(playlist);
        var index = Array.FindIndex(items, item => item.ResourceId == hardwarePlaylistItem);
        var status = hardwarePlaylistPaused ? "轮播切换已暂停" :
            hardwareStatus.StartsWith("等待同一", StringComparison.Ordinal) ? "断线 · 计时暂停" :
            HardwareVideoPaused ? "视频暂停 · 计时暂停" : "轮播";
        return $"{playlist.Name} · {status} · {index + 1}/{items.Length} · 剩余 {Math.Max(0, Math.Ceiling(hardwarePlaylistRemaining.TotalSeconds))} 秒";
    }

    void PauseHardwareFromFooter(object sender, RoutedEventArgs e)
    {
        ToggleHardwarePause();
        Render();
    }

    void StepHardwareFromFooter(object sender, RoutedEventArgs e)
    {
        try { StepHardwarePlaylist(ReferenceEquals(sender, HardwarePrevious) ? -1 : 1); }
        catch (Exception ex) { Notice.Text = ex.Message; }
    }
}
