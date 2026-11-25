using System.IO;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using Microsoft.Win32;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed record ImageDisplayModeOption(ImageDisplayMode Value, string Name);

/// <summary>Edits a preview draft; applying passes that exact scene to the external renderer.</summary>
public sealed class ImageSceneViewModel : ObservableObject, IDisposable
{
    private readonly DevicesViewModel _devices;
    private readonly ISettingsService _settings;
    private readonly ILogger _logger;
    private readonly Action<Scene> _preview;
    private readonly ClockScene _clock;
    private readonly ImageSceneLoader _loader = new();
    private readonly CancellationTokenSource _lifetime = new();
    private ImageScene? _image;
    private string? _path;
    private string _status = "选择 JPG / PNG 图片，在预览确认后应用到副屏。";
    private ImageDisplayMode _mode;
    private bool _busy, _closing, _disposed;
    private Task _pending = Task.CompletedTask;

    public IReadOnlyList<ImageDisplayModeOption> Modes { get; } =
    [
        new(ImageDisplayMode.Fill, "铺满"),
        new(ImageDisplayMode.Uniform, "完整显示"),
        new(ImageDisplayMode.Stretch, "拉伸"),
        new(ImageDisplayMode.Center, "居中")
    ];
    public string? ImagePath { get => _path; private set => SetProperty(ref _path, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool CanEdit => !_busy && !_closing;
    public ImageDisplayMode Mode
    {
        get => _mode;
        set
        {
            if (!CanEdit || !Enum.IsDefined(value) || !SetProperty(ref _mode, value)) return;
            if (_image is null) return;
            _image = new ImageScene(_image.Path, _image.Image, value);
            _preview(_image);
            Status = "预览已更新，点击“应用到副屏”生效并保存模式。";
        }
    }
    public AsyncCommand SelectImageCommand { get; }
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand ClockCommand { get; }

    public ImageSceneViewModel(DevicesViewModel devices, ISettingsService settings, AppSettings initial,
        ClockScene clock, Action<Scene> preview, ILogger logger)
    {
        _devices = devices; _settings = settings; _clock = clock; _preview = preview; _logger = logger;
        _path = initial.LastImagePath;
        _mode = Enum.IsDefined(initial.ImageMode) ? initial.ImageMode : ImageDisplayMode.Fill;
        SelectImageCommand = new AsyncCommand(SelectAsync, () => CanEdit, logger);
        ApplyCommand = new AsyncCommand(ApplyAsync,
            () => CanEdit && _devices.TestDisplayCommand.CanExecute(null) && (_image is not null || _path is null), logger);
        ClockCommand = new AsyncCommand(() =>
        {
            _image = null;
            ImagePath = null;
            _preview(_clock);
            Status = "时钟预览 · 点击“应用到副屏”切换副屏内容。";
            NotifyCommands();
            return Task.CompletedTask;
        }, () => CanEdit, logger);
        _devices.TestDisplayCommand.CanExecuteChanged += OnDeviceStateChanged;
    }

    private Task SelectAsync()
    {
        var dialog = new OpenFileDialog { Title = "选择副屏背景", Filter = "图片 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png", CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog() == true ? LoadAsync(dialog.FileName) : Task.CompletedTask;
    }

    public Task RestoreAsync() => string.IsNullOrWhiteSpace(_path) ? Task.CompletedTask : LoadAsync(_path, false);

    public Task LoadAsync(string path, bool save = true) => RunAsync(async () =>
    {
        var scene = await _loader.LoadAsync(path, Mode, _lifetime.Token);
        if (_closing) return;
        _image = scene;
        ImagePath = scene.Path;
        _preview(scene);
        Status = "图片预览已就绪，点击“应用到副屏”显示。";
        if (save && !await SaveImageAsync(scene)) Status += " 最近图片配置保存失败，请检查日志。";
    }, path);

    public Task ApplyAsync() => RunAsync(async () =>
    {
        if (!_devices.TestDisplayCommand.CanExecute(null))
        {
            Status = "目标副屏未连接，请在设备页面设置并刷新目标副屏。";
            return;
        }
        Scene scene = _clock;
        if (_path is null) _preview(_clock);
        if (_path is not null)
        {
            // Reopen on apply: detect deletion/replacement, without retaining an open source file.
            var image = await _loader.LoadAsync(_path, Mode, _lifetime.Token);
            if (_closing) return;
            _image = image;
            _preview(image);
            scene = image;
            if (!await SaveImageAsync(image))
            {
                Status = "图片配置保存失败，未应用。请检查日志后重试。";
                return;
            }
        }
        if (_closing) return;
        await _devices.ApplySceneAsync(scene);
        Status = _devices.RenderStatus;
    });

    private Task<bool> SaveImageAsync(ImageScene scene) => _settings.UpdateAsync(settings =>
    {
        settings.LastImagePath = scene.Path;
        settings.ImageMode = scene.Mode;
    });

    private Task RunAsync(Func<Task> action, string? operationPath = null)
    {
        if (!CanEdit) return Task.CompletedTask;
        _busy = true;
        NotifyCommands();
        _pending = ExecuteAsync(action, operationPath ?? ImagePath);
        return _pending;
    }

    private async Task ExecuteAsync(Func<Task> action, string? operationPath)
    {
        try { await action(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Error(exception, "Image scene operation failed for {ImagePath}", operationPath);
            Status = exception switch
            {
                FileNotFoundException or DirectoryNotFoundException => "图片不存在或已被删除，请重新选择 JPG / PNG 图片。",
                UnauthorizedAccessException => "无法读取图片，请检查文件访问权限或选择其它图片。",
                InvalidDataException => exception.Message,
                _ => "无法读取或应用图片，文件可能损坏或正在被占用。请重新选择图片。"
            };
        }
        finally { _busy = false; NotifyCommands(); }
    }

    private void OnDeviceStateChanged(object? sender, EventArgs e) => NotifyCommands();
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit));
        SelectImageCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        ClockCommand.NotifyCanExecuteChanged();
    }

    public Task PrepareForCloseAsync()
    {
        _closing = true;
        _lifetime.Cancel();
        NotifyCommands();
        return _pending;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _closing = true;
        _lifetime.Cancel();
        _devices.TestDisplayCommand.CanExecuteChanged -= OnDeviceStateChanged;
        _image = null;
        _lifetime.Dispose();
    }
}
