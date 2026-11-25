using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    static readonly HttpClient CatalogClient = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    readonly OnlineResourceSource onlineSource = new(CatalogClient);
    readonly CancellationTokenSource onlineLifetime = new();
    readonly Dictionary<string, string> downloadFailures = new();
    bool catalogAttempted, catalogLoading;
    string? activeDownload, catalogStatus;
    string downloadProgress = "";
    TextBlock? onlineActivity;
    event Action? DownloadsChanged;

    bool OnlineCatalogConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OnlineResourceSource.ConfigurationKey));

    void RenderOnlineControls(WrapPanel toolbar)
    {
        toolbar.Children.Add(HomeButton($"下载记录 · {LocalDownloads().Count()}", ShowDownloads, "PillButton"));
        if (OnlineCatalogConfigured)
        {
            var refresh = HomeButton("刷新目录", () => _ = RefreshOnlineCatalog(), "PillButton");
            refresh.IsEnabled = !catalogLoading && activeDownload == null;
            toolbar.Children.Add(refresh);
        }
        else if (!catalogAttempted)
            catalogStatus = "当前显示演示资源。";
        if (!catalogAttempted && OnlineCatalogConfigured)
            _ = Dispatcher.InvokeAsync(() => _ = RefreshOnlineCatalog());
    }

    async Task RefreshOnlineCatalog()
    {
        if (catalogLoading || activeDownload != null) return;
        catalogAttempted = catalogLoading = true;
        catalogStatus = "正在读取在线资源目录…";
        if (page == "在线资源") Render();
        try
        {
            var address = OnlineResourceSource.ConfiguredCatalog();
            if (address == null) { catalogStatus = "当前显示演示资源。"; return; }
            var catalog = await onlineSource.ReadCatalog(address, onlineLifetime.Token);
            onlineLifetime.Token.ThrowIfCancellationRequested();
            library.RefreshOnlineEntries(catalog);
            SyncLibrary();
            SaveWorkspace();
            catalogStatus = null;
        }
        catch (Exception ex) when (!onlineLifetime.IsCancellationRequested)
        { catalogStatus = "读取目录失败，可刷新重试。" + NetworkError(ex); }
        catch (Exception) when (onlineLifetime.IsCancellationRequested) { }
        finally
        {
            catalogLoading = false;
            if (!onlineLifetime.IsCancellationRequested && page == "在线资源") Render();
        }
    }

    void RenderOnlineActivity()
    {
        onlineActivity = HomeText("", 13, muted: true);
        Body.Children.Add(onlineActivity);
        UpdateOnlineActivity();
    }

    void UpdateOnlineActivity()
    {
        if (page == "在线资源" && onlineActivity != null)
        {
            onlineActivity.Text = activeDownload != null ? downloadProgress : catalogStatus ?? "";
            onlineActivity.Visibility = onlineActivity.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        DownloadsChanged?.Invoke();
    }

    string DownloadLabel(Resource resource) => Entry(resource.Id).MediaUrl == null ? "加入演示资源库" :
        downloadFailures.ContainsKey(resource.Id) ? "重试下载" : "下载";

    void Download(Resource resource)
    {
        var entry = Entry(resource.Id);
        if (entry.Resource.Local) return;
        if (entry.MediaUrl != null)
        {
            _ = DownloadMedia(entry);
            return;
        }
        library.Update(resource.Id, value => value with
        {
            Resource = value.Resource with { Local = true }, Online = true, Downloaded = true, Added = DateTimeOffset.UtcNow
        });
        SyncLibrary();
        SaveWorkspace();
        Notice.Text = $"「{resource.Name}」已加入演示资源库。";
        Render();
    }

    async Task DownloadMedia(LibraryEntry entry)
    {
        if (activeDownload != null || catalogLoading)
        {
            Notice.Text = catalogLoading ? "正在读取在线目录，请稍后下载。" : "已有下载正在进行，可在下载记录查看进度。";
            return;
        }
        var id = entry.Resource.Id;
        activeDownload = id;
        downloadFailures.Remove(id);
        downloadProgress = $"正在下载「{entry.Resource.Name}」…";
        UpdateOnlineActivity();
        string? temporary = null;
        try
        {
            temporary = Path.Combine(Path.GetTempPath(), $"MiraBox-download-{Guid.NewGuid():N}" + OnlineResourceSource.Extension(entry));
            var progress = new Progress<(long Bytes, long? Total)>(value =>
            {
                if (activeDownload != id || onlineLifetime.IsCancellationRequested) return;
                downloadProgress = value.Total is > 0
                    ? $"正在下载「{entry.Resource.Name}」 · {(double)value.Bytes / value.Total.Value:P0}"
                    : $"正在下载「{entry.Resource.Name}」 · {value.Bytes / 1048576d:0.0} MB";
                UpdateOnlineActivity();
            });
            await onlineSource.Download(entry, temporary, progress, onlineLifetime.Token);
            downloadProgress = $"正在检查「{entry.Resource.Name}」…";
            UpdateOnlineActivity();
            VideoMetadata? video = null;
            MiraBox.Core.Size dimensions;
            if (entry.Type == "视频")
            {
                video = await videoProbe.ProbeAsync(temporary);
                dimensions = video.Dimensions;
            }
            else
            {
                var image = Load(temporary);
                dimensions = new(image.PixelWidth, image.PixelHeight);
            }
            onlineLifetime.Token.ThrowIfCancellationRequested();
            library.CompleteOnlineDownload(id, temporary, dimensions, video);
            SyncLibrary();
            SaveWorkspace();
            Notice.Text = $"「{entry.Resource.Name}」已下载，可在资源库的「已下载」查看。";
        }
        catch (Exception ex) when (!onlineLifetime.IsCancellationRequested)
        {
            if (library.Entries.Any(value => value.Resource.Id == id && value.Resource.Local))
                Notice.Text = "素材已入库，工作区保存失败：" + ex.Message;
            else
            {
                downloadFailures[id] = NetworkError(ex);
                Notice.Text = $"「{entry.Resource.Name}」下载失败。可在下载记录重试。";
            }
        }
        catch (Exception) when (onlineLifetime.IsCancellationRequested) { }
        finally
        {
            if (temporary != null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            activeDownload = null;
            if (!onlineLifetime.IsCancellationRequested)
            {
                if (page is "在线资源" or "资源库") Render();
                UpdateOnlineActivity();
            }
        }
    }

    static string NetworkError(Exception error) => error switch
    {
        OperationCanceledException => "请求超时。",
        HttpRequestException { StatusCode: { } code } => $"服务器返回 {(int)code}。",
        HttpRequestException => "无法连接资源服务器。",
        _ => error.Message
    };

    void ShowDownloads()
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var progress = HomeText("", 13, muted: true);
        var rows = new StackPanel();
        panel.Children.Add(progress);
        panel.Children.Add(rows);
        var dialog = Dialog("下载记录", panel, 600, 470);
        panel.Children.Add(HomeButton("关闭", dialog.Close, "PillButton"));
        DownloadsChanged += Refresh;
        try { Refresh(); dialog.ShowDialog(); }
        finally { DownloadsChanged -= Refresh; }

        void Refresh()
        {
            progress.Text = activeDownload == null ? "" : downloadProgress;
            progress.Visibility = activeDownload == null ? Visibility.Collapsed : Visibility.Visible;
            var key = string.Join('|', new[] { activeDownload }.Concat(downloadFailures.Select(item => item.Key + item.Value))
                .Concat(LocalDownloads().Select(item => item.Resource.Id)));
            if (Equals(rows.Tag, key)) return;
            rows.Tag = key;
            rows.Children.Clear();
            foreach (var (id, reason) in downloadFailures)
            {
                if (library.Entries.FirstOrDefault(item => item.Resource.Id == id) is not { } failed) continue;
                var retry = HomeButton("重试", () => Download(failed.Resource), "PillButton");
                retry.IsEnabled = activeDownload == null;
                rows.Children.Add(HomeCard(HomeHeader(HomeText($"{failed.Resource.Name} · {reason}", 13), retry)));
            }
            foreach (var downloaded in LocalDownloads())
            {
                var open = HomeButton("在资源库查看", () =>
                {
                    dialog.Close();
                    Navigate("资源库");
                    if (page != "资源库") return;
                    collection = "已下载";
                    query = downloaded.Resource.Name;
                    contentType = category = subcategory = "全部";
                    showAll = true;
                    Render();
                    PageScroll.ScrollToTop();
                }, "LinkButton");
                rows.Children.Add(HomeCard(HomeHeader(HomeText($"{downloaded.Resource.Name} · " +
                    (downloaded.MediaUrl == null ? "演示资源" : $"{downloaded.Added.ToLocalTime():MM-dd HH:mm} 入库"), 13), open)));
            }
            if (rows.Children.Count == 0 && activeDownload == null)
                rows.Children.Add(HomeText("暂无下载记录。", 13, muted: true));
        }
    }
}
