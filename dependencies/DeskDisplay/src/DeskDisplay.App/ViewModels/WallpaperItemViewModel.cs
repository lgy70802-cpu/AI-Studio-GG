using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class WallpaperItemViewModel : ObservableObject
{
    private static readonly SemaphoreSlim ThumbnailGate = new(4, 4);
    private readonly string _thumbnailPath;
    private readonly Dispatcher _dispatcher;
    private BitmapSource? _thumbnail;
    private BitmapSource? _detailImage;
    private Task<BitmapSource?>? _thumbnailTask;
    private Task<BitmapSource?>? _detailTask;
    private int _detailGeneration;
    private int _thumbnailGeneration;
    private bool _isSelected;

    public WallpaperItemViewModel(WallpaperAsset asset, string thumbnailPath)
    {
        Asset = asset;
        _thumbnailPath = thumbnailPath;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public WallpaperAsset Asset { get; }
    public string Name => Asset.Name;
    public string Category => Asset.Category;
    public string Subcategory => Asset.Subcategory;
    public string Tags => Asset.Tags;
    public string Kind => Asset.IsVideo ? "动态" : "静态";
    public string ResourceType => "壁纸";
    public string UseLabel => Asset.IsVideo ? "使用视频" : "使用壁纸";
    public string CreatorUsageHint => !IsAvailable ? "素材文件缺失，请先修复" : Asset.IsVideo ? "可单独播放或作为主题视频背景；每个布局支持一个可见视频" : string.IsNullOrWhiteSpace(Asset.CloudResourceId) ? "本地素材 · 可加入主题" : "已下载云端素材 · 保留原资源授权";
    public string CarouselCategory => $"{ResourceType} · {Subcategory}";
    public bool IsPortrait => Asset.Height > Asset.Width;
    public double LibraryAspectRatio => IsPortrait ? 0.25 : 4d;
    public string LibraryStateLabel => !string.IsNullOrWhiteSpace(Asset.CloudResourceId) ? "✓ 已下载" : "本地资源";
    public string Resolution => $"{Asset.Width} × {Asset.Height}";
    public string Metadata => $"{Kind}壁纸 · {Resolution}" + (Asset.IsVideo ? $" · {TimeSpan.FromSeconds(Math.Max(0, Asset.DurationSeconds)):mm\\:ss}" : "");
    public string FavoriteLabel => Asset.Favorite ? "♥ 已收藏" : "♡ 收藏";
    public bool IsAvailable => File.Exists(Asset.Path);
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool HasCachedThumbnail => _thumbnail is not null;
    public long ThumbnailLastUsed { get; private set; }
    public BitmapSource? Thumbnail { get { ThumbnailLastUsed = Environment.TickCount64; _ = EnsureThumbnailAsync(); return _thumbnail; } }

    public Task<BitmapSource?> EnsureThumbnailAsync()
    {
        if (_thumbnail is not null) return Task.FromResult<BitmapSource?>(_thumbnail);
        return _thumbnailTask ??= LoadThumbnailAsync(_thumbnailGeneration);
    }

    private async Task<BitmapSource?> LoadThumbnailAsync(int generation)
    {
        await ThumbnailGate.WaitAsync();
        BitmapSource? loaded;
        try
        {
            loaded = await Task.Run(() =>
            {
                try
                {
                    var sourcePath = !Asset.IsVideo && File.Exists(Asset.Path) ? Asset.Path : _thumbnailPath;
                    if (!File.Exists(sourcePath)) return null;
                    using var stream = File.OpenRead(sourcePath);
                    var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    var width = frame.PixelWidth; var height = frame.PixelHeight; stream.Position = 0;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    if (height > width) bitmap.DecodePixelHeight = Math.Min(640, height); else bitmap.DecodePixelWidth = Math.Min(640, width);
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    return (BitmapSource?)bitmap;
                }
                catch (Exception exception) { Log.Warning(exception, "Cannot load library thumbnail {Path}", Asset.Path); return null; }
            });
        }
        finally { ThumbnailGate.Release(); }
        if (generation != _thumbnailGeneration) return null;
        await _dispatcher.InvokeAsync(() =>
        {
            if (generation != _thumbnailGeneration) return;
            _thumbnail = loaded;
            OnPropertyChanged(nameof(Thumbnail));
        });
        return loaded;
    }

    public BitmapSource? DetailImage { get { _ = EnsureDetailImageAsync(); return _detailImage ?? Thumbnail; } }
    public Task<BitmapSource?> EnsureDetailImageAsync()
    {
        if (Asset.IsVideo) return EnsureThumbnailAsync();
        if (_detailImage is not null) return Task.FromResult<BitmapSource?>(_detailImage);
        return _detailTask ??= LoadDetailImageAsync(_detailGeneration);
    }
    private async Task<BitmapSource?> LoadDetailImageAsync(int generation)
    {
        await ThumbnailGate.WaitAsync();
        BitmapSource? loaded;
        try
        {
            loaded = await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(Asset.Path)) return null;
                    using var stream = File.OpenRead(Asset.Path);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = Math.Min(2048, Math.Max(1, Asset.Width));
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    return (BitmapSource?)bitmap;
                }
                catch (Exception exception) { Log.Warning(exception, "Cannot load wallpaper detail preview {Path}", Asset.Path); return null; }
            });
        }
        finally { ThumbnailGate.Release(); }
        if (generation != _detailGeneration) return null;
        await _dispatcher.InvokeAsync(() =>
        {
            if (generation != _detailGeneration) return;
            _detailImage = loaded; OnPropertyChanged(nameof(DetailImage));
        }, DispatcherPriority.Background);
        return loaded;
    }
    public void ReleaseDetailImage() { ++_detailGeneration; _detailImage = null; _detailTask = null; }
    public void ReleaseThumbnail() { ++_thumbnailGeneration; _thumbnail = null; _thumbnailTask = null; ReleaseDetailImage(); }
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Category)); OnPropertyChanged(nameof(Subcategory)); OnPropertyChanged(nameof(Tags)); OnPropertyChanged(nameof(CarouselCategory)); OnPropertyChanged(nameof(IsPortrait)); OnPropertyChanged(nameof(LibraryAspectRatio)); OnPropertyChanged(nameof(LibraryStateLabel));
        OnPropertyChanged(nameof(FavoriteLabel)); OnPropertyChanged(nameof(IsAvailable));
    }
}
