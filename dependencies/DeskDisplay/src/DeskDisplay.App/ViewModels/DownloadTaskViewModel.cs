using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public enum DownloadTaskState { Running, Completed, Failed, Cancelled }
public sealed class DownloadTaskViewModel(StudioResource resource, string serverUrl) : ObservableObject
{
    private DownloadTaskState _state = DownloadTaskState.Running;
    private int _progress;
    private string _message = "下载并校验中";
    public StudioResource Resource { get; } = resource;
    public string ServerUrl { get; } = serverUrl;
    public string Name => Resource.Name;
    public string Version => Resource.Version;
    public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }
    public DownloadTaskState State { get => _state; set { if (SetProperty(ref _state, value)) { OnPropertyChanged(nameof(CanRetry)); OnPropertyChanged(nameof(IsRunning)); } } }
    public bool CanRetry => State is DownloadTaskState.Failed or DownloadTaskState.Cancelled;
    public bool IsRunning => State == DownloadTaskState.Running;
}
