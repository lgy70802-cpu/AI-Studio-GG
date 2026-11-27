using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    readonly IVideoProbe videoProbe = new WindowsVideoProbe();
    static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mov", ".webm", ".mkv" };
    bool importing;
    sealed record ImportResult(string File, string Result, string? Reason = null);
    readonly List<ImportResult> lastImportResults = [];
    TextBlock? libraryImportProgress;
    ProgressBar? libraryImportBar;
    int importCompleted, importTotal;
    string currentImportFile = "";

    async void Import()
    {
        if (importing) return;
        var picker = new OpenFileDialog { Filter = "图片或视频|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.m4v;*.mov;*.webm;*.mkv", Multiselect = true };
        if (picker.ShowDialog(this) != true) return;
        await ImportFiles(picker.FileNames);
    }

    async Task ImportFiles(string[] paths)
    {
        if (importing || paths.Length == 0) return;
        importing = true;
        lastImportResults.Clear();
        importCompleted = 0;
        importTotal = paths.Length;
        int imagesImported = 0, videos = 0, repeated = 0, limited = 0, failed = 0;
        Notice.Text = "正在读取素材…";
        Render();
        try
        {
            foreach (var path in paths)
            {
                currentImportFile = Path.GetFileName(path);
                UpdateImportProgress();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                try
                {
                    var file = new FileInfo(path);
                    if (library.IsDuplicate(file))
                    {
                        repeated++;
                        lastImportResults.Add(new(currentImportFile, "重复", "同名且大小相同"));
                        continue;
                    }
                    if (VideoExtensions.Contains(file.Extension))
                    {
                        if (VideoImportLimits.Exceeded(file.Length)) throw new VideoLimitException("单个视频超过 500 MB");
                        var metadata = await videoProbe.ProbeAsync(path);
                        if (VideoImportLimits.Exceeded(file.Length, metadata.Duration))
                            throw new VideoLimitException("视频超过 500 MB 或 10 分钟");
                        if (library.ImportVideo(path, metadata) == null)
                        {
                            repeated++;
                            lastImportResults.Add(new(currentImportFile, "重复", "同名且大小相同"));
                        }
                        else { videos++; lastImportResults.Add(new(currentImportFile, "已导入视频")); }
                    }
                    else
                    {
                        var image = Load(path);
                        if (library.Import(path, new(image.PixelWidth, image.PixelHeight)) == null)
                        {
                            repeated++;
                            lastImportResults.Add(new(currentImportFile, "重复", "同名且大小相同"));
                        }
                        else { imagesImported++; lastImportResults.Add(new(currentImportFile, "已导入图片")); }
                    }
                }
                catch (VideoLimitException ex)
                { limited++; lastImportResults.Add(new(currentImportFile, "超出限制", ex.Message)); }
                catch (Exception ex)
                { failed++; lastImportResults.Add(new(currentImportFile, "无法解析", ex.Message)); }
                finally { importCompleted++; UpdateImportProgress(); }
            }
            SyncLibrary();
            if (page == "资源库") { collection = "全部资源"; query = ""; contentType = category = subcategory = "全部"; sort = "最近添加"; showAll = true; }
            SaveWorkspace();
            Notice.Text = $"已导入 {imagesImported} 张图片、{videos} 个视频，跳过 {repeated} 个重复、{limited} 个超出限制、{failed} 个无法解析。";
        }
        catch (Exception ex) { Notice.Text = "保存导入结果失败：" + ex.Message; }
        finally
        {
            importing = false;
            if (page == "资源库") Render();
        }
    }

    void UpdateImportProgress()
    {
        if (libraryImportProgress != null)
            libraryImportProgress.Text = $"导入 {importCompleted} / {importTotal} · {currentImportFile}";
        if (libraryImportBar != null)
        {
            libraryImportBar.Maximum = Math.Max(1, importTotal);
            libraryImportBar.Value = importCompleted;
        }
    }

    void ShowImportResults()
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(HomeText($"本次处理 {lastImportResults.Count} 个文件", 16, bold: true));
        foreach (var item in lastImportResults)
        {
            var row = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            row.Children.Add(HomeText(item.File, 14, bold: true));
            row.Children.Add(HomeText(item.Result + (item.Reason == null ? "" : " · " + item.Reason), 12, muted: true));
            panel.Children.Add(row);
        }
        if (lastImportResults.Count == 0) panel.Children.Add(HomeText("暂无导入记录。", 13, muted: true));
        Dialog("导入结果", panel, 660, 500).ShowDialog();
    }
}
