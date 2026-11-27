using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Microsoft.Win32;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class VideoSceneViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly IVideoPlaybackService _playback;
    private bool _busy, _closing;
    private int _fps = 30;
    private Task _pending = Task.CompletedTask;
    public IReadOnlyList<int> FpsOptions { get; } = [30, 60];
    public int Fps { get => _fps; set { if (value is 30 or 60 && !_busy) SetProperty(ref _fps, value); } }
    public bool CanEdit => !_busy && !_closing;
    public bool IsPlaying => _playback.IsPlaying;
    public IVideoTransport? Transport => _playback as IVideoTransport;
    public void StopPlayback() => _playback.Stop();
    public string Status => _playback.Status;
    public AsyncCommand SelectCommand { get; }
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand UpdateFpsCommand { get; }

    public VideoSceneViewModel(MainViewModel main, IVideoPlaybackService playback, ILogger logger)
    {
        _main = main; _playback = playback;
        SelectCommand = new AsyncCommand(() =>
        {
            var dialog = new OpenFileDialog { Title = "选择 MP4/H.264 视频", Filter = "MP4 视频|*.mp4", CheckFileExists = true };
            return dialog.ShowDialog() == true ? OpenAsync(dialog.FileName) : Task.CompletedTask;
        }, () => CanEdit, logger);
        ApplyCommand = new AsyncCommand(() => _main.Devices.ApplySceneAsync(_main.Scene),
            () => CanEdit && _main.Scene is VideoScene && playback.IsPlaying && _main.Devices.TestDisplayCommand.CanExecute(null), logger);
        UpdateFpsCommand = new AsyncCommand(() => _main.Scene is VideoScene video ? OpenAsync(video.Path) : Task.CompletedTask,
            () => CanEdit && _main.Scene is VideoScene, logger);
        playback.StateChanged += OnStateChanged;
        main.PropertyChanged += OnMainChanged;
        main.Devices.TestDisplayCommand.CanExecuteChanged += OnStateChanged;
    }
    public Task OpenAsync(string path)
    {
        if (!CanEdit) return Task.CompletedTask;
        _busy = true;
        _main.PreviewScene(new VideoScene(path, _playback));
        Notify();
        _pending = OpenCoreAsync(path);
        return _pending;
    }
    private async Task OpenCoreAsync(string path)
    {
        try { await _playback.OpenAsync(path, Fps); }
        finally { _busy = false; Notify(); }
    }
    private void OnMainChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Notify();
    private void OnStateChanged(object? sender, EventArgs e) => Notify();
    private void Notify()
    {
        OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(CanEdit));
        SelectCommand.NotifyCanExecuteChanged(); ApplyCommand.NotifyCanExecuteChanged(); UpdateFpsCommand.NotifyCanExecuteChanged();
    }
    public Task PrepareForCloseAsync()
    {
        _closing = true;
        _playback.Stop();
        Notify();
        return _pending;
    }
    public void Dispose()
    {
        _closing = true;
        _playback.StateChanged -= OnStateChanged;
        _main.PropertyChanged -= OnMainChanged;
        _main.Devices.TestDisplayCommand.CanExecuteChanged -= OnStateChanged;
        _playback.Dispose();
    }
}
