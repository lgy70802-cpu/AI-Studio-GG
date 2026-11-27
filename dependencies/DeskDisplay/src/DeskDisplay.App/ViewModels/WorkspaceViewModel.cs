using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Infrastructure;
using Microsoft.Win32;
using Serilog;

namespace DeskDisplay.App.ViewModels;

/// <summary>Coordinates user intent, scene restoration and output without owning native resources.</summary>
public sealed class WorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly ClockScene _clock;
    private readonly PerformanceScene _performance;
    private readonly DashboardScene _dashboard;
    private readonly IRenderWindowService _output;
    private readonly ISettingsService _settings;
    private readonly ISensorService _sensors;
    private readonly ILogger _logger;
    private readonly DispatcherTimer _diagnosticTimer;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly bool _watchPower;
    private string _selected;
    private string? _imagePath, _videoPath, _dashboardPath;
    private string _status = "请选择场景，确认预览后应用到副屏。";
    private string _diagnostics = "正在读取诊断信息…";
    private bool _autoDisplay, _busy, _restoring = true, _closing, _disposed, _suspended;
    private long _intent;
    private bool _fileReady;
    private Task _pending = Task.CompletedTask, _saveTail = Task.CompletedTask;
    public IReadOnlyList<string> SceneChoices { get; } = ["时钟", "图片", "组合", "视频", "性能"];
    public IReadOnlyList<string> DashboardLayouts { get; } = ["时钟居左", "时钟居中", "时钟与性能"];
    public string DashboardLayoutName
    {
        get => LayoutName(_dashboard.Layout);
        set
        {
            if (!CanEdit || !TryLayout(value, out var layout) || _dashboard.Layout == layout) return;
            _dashboard.Layout = layout;
            OnPropertyChanged();
            QueueSave();
        }
    }
    public bool DashboardShowPerformance
    {
        get => _dashboard.ShowPerformance;
        set
        {
            if (!CanEdit || _dashboard.ShowPerformance == value) return;
            _dashboard.ShowPerformance = value;
            OnPropertyChanged();
            QueueSave();
        }
    }
    public string SelectedScene
    {
        get => _selected;
        set
        {
            if (!CanEdit || !SceneChoices.Contains(value) || !SetProperty(ref _selected, value)) return;
            ++_intent;
            QueueSave();
            _ = RunAsync(() => EnterAsync(value));
        }
    }
    public bool AutoDisplayOnStartup
    {
        get => _autoDisplay;
        set { if (!_closing && SetProperty(ref _autoDisplay, value)) QueueSave(); }
    }
    public bool CanEdit => !_busy && !_closing && !_suspended && !_restoring
        && _main.Images?.CanEdit != false && _main.Videos?.CanEdit != false;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Diagnostics { get => _diagnostics; private set => SetProperty(ref _diagnostics, value); }
    public string PreviewStatus => $"当前预览：{Name(_main.Scene)}" + (Name(_main.Scene) != _selected ? $"（所选{_selected}尚未就绪）" : "");
    public string OutputStatus => _main.Usb?.IsRunning == true ? _main.Usb.Status : _output.CurrentScene is { } scene
        ? $"副屏正在显示：{Name(scene)} · {_main.Devices.TargetName}" + (ReferenceEquals(scene, _main.Scene) ? " · 与预览同步" : " · 预览尚未应用")
        : "副屏显示：已停止";
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand ApplyUsbCommand { get; }
    public AsyncCommand StopCommand { get; }
    public AsyncCommand SelectFileCommand { get; }
    public AsyncCommand OpenLogsCommand { get; }
    public Task PendingOperation => _pending;

    public WorkspaceViewModel(MainViewModel main, Scene clock, PerformanceScene performance,
        IRenderWindowService output, ISettingsService settings, ISensorService sensors, AppSettings initial,
        ILogger logger, bool watchPower = true)
    {
        _main = main; _clock = (ClockScene)clock; _performance = performance; _output = output;
        _settings = settings; _sensors = sensors; _logger = logger; _watchPower = watchPower;
        _dashboard = new DashboardScene(_clock, performance)
        {
            Layout = Enum.IsDefined(initial.DashboardLayout) ? initial.DashboardLayout : DashboardLayout.Split,
            ShowPerformance = initial.DashboardShowPerformance
        };
        _selected = SceneChoices.Contains(initial.LastScene) ? initial.LastScene : "时钟";
        _imagePath = initial.LastImagePath; _videoPath = initial.LastVideoPath; _dashboardPath = initial.DashboardImagePath;
        _autoDisplay = initial.AutoDisplayOnStartup;
        main.Videos!.Fps = initial.VideoFps is 30 or 60 ? initial.VideoFps : 30;
        ApplyCommand = new AsyncCommand(() => RunAsync(ApplyCoreAsync), () => CanEdit && Ready && main.Devices.TestDisplayCommand.CanExecute(null), logger);
        ApplyUsbCommand = new AsyncCommand(() => { main.Library?.StopRotation(); return ApplyUsbAsync(); },
            () => CanEdit && Ready && main.Usb?.CanApply == true, logger);
        StopCommand = new AsyncCommand(async () =>
        {
            ++_intent;
            main.Library?.StopRotation();
            main.Devices.CloseRender();
            if (_busy || !main.Videos.CanEdit) main.Videos.StopPlayback();
            Status = "副屏显示已停止；不会在稍后自动打开。";
            if (main.Usb is not null) await main.Usb.StopAsync();
        }, () => !_closing, logger);
        SelectFileCommand = new AsyncCommand(() =>
        {
            var dialog = new OpenFileDialog { Filter = _selected == "视频" ? "MP4 视频|*.mp4" : "图片|*.jpg;*.jpeg;*.png", CheckFileExists = true };
            return dialog.ShowDialog() == true ? RunAsync(() => LoadFileAsync(dialog.FileName)) : Task.CompletedTask;
        }, () => CanEdit && _selected is "图片" or "组合" or "视频", logger);
        OpenLogsCommand = new AsyncCommand(() =>
        {
            try
            {
                var path = DeskDisplay.Infrastructure.Logging.LoggingSetup.ActiveLogDirectory ?? Path.Combine(AppPaths.DataDirectory, "logs");
                Directory.CreateDirectory(path);
                using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception exception) { logger.Error(exception, "Cannot open log folder"); Status = "无法打开日志目录：" + AppPaths.DataDirectory; }
            return Task.CompletedTask;
        }, () => !_closing, logger);
        main.PropertyChanged += OnMainChanged;
        if (main.Usb is not null) main.Usb.PropertyChanged += OnUsbChanged;
        main.Images!.PropertyChanged += OnEditorChanged;
        main.Videos.PropertyChanged += OnEditorChanged;
        output.StateChanged += OnOutputChanged;
        main.Devices.TestDisplayCommand.CanExecuteChanged += OnOutputChanged;
        _diagnosticTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _diagnosticTimer.Tick += OnDiagnosticTick;
        _diagnosticTimer.Start();
        if (watchPower) SystemEvents.PowerModeChanged += OnPowerModeChanged;
        UpdateDiagnostics();
    }
    private bool Ready => Name(_main.Scene) == _selected
        && (_selected is not ("图片" or "组合") || _fileReady)
        && (_selected != "视频" || (_main.Videos!.IsPlaying && _main.Scene is VideoScene video && video.Playback.FramesPerSecond == _main.Videos.Fps));
    public bool IsReady => Ready && !_closing && !_suspended;
    public bool IsPreviewApplied => ReferenceEquals(_output.CurrentScene, _main.Scene);
    public IReadOnlyList<string> RememberedFiles => new[] { _imagePath, _videoPath, _dashboardPath }.OfType<string>().Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public Task OpenAssetAsync(string path, bool isVideo, bool apply = false, bool usb = false) => RunAsync(async () =>
    {
        var intent = ++_intent;
        _selected = isVideo ? "视频" : "图片";
        OnPropertyChanged(nameof(SelectedScene));
        await LoadFileAsync(path);
        if (apply && !_closing && !_suspended && intent == _intent && Ready)
        {
            if (usb) await ApplyUsbCoreAsync();
            else await ApplyCoreAsync();
        }
    });
    public Task OpenDashboardAssetAsync(string path, bool usb = false) => RunAsync(async () =>
    {
        var intent = ++_intent;
        _selected = "组合";
        OnPropertyChanged(nameof(SelectedScene));
        await LoadFileAsync(path);
        if (usb && !_closing && !_suspended && intent == _intent && Ready) await ApplyUsbCoreAsync();
    });
    public Task ApplyAsync() => RunAsync(ApplyCoreAsync);
    public Task ApplyUsbAsync() => RunAsync(ApplyUsbCoreAsync);
    public bool IsUsbPreviewApplied => _main.Usb?.IsRunning == true && _main.Usb.CanApply && Ready && (ReferenceEquals(_main.Usb.Scene,_main.Scene) || _main.Scene is VideoScene preview && _main.Usb.Scene is VideoFrameScene actual && string.Equals(preview.Path,actual.Path,StringComparison.OrdinalIgnoreCase));
    private async Task ApplyUsbCoreAsync()
    {
        if (_closing || _suspended || !Ready || _main.Usb?.CanApply != true) return;
        if (!_main.Usb.ValidateConfiguration())
        {
            Status = "请先在 USB屏 页面选择设备并填写画布尺寸，然后重试。";
            if (_main.Library is not null) _main.Library.SelectedTab = "USB屏";
            else _main.SelectedNavigation = "USB屏";
            return;
        }
        var intent = _intent;
        if (_selected is "图片" or "组合")
        {
            await LoadFileAsync((_selected == "组合" ? _dashboardPath : _imagePath)!);
            if (!_fileReady) return;
        }
        if (_closing || _suspended || intent != _intent) return;
        await _main.Usb.ApplyCurrentAsync();
        Status = _main.Usb.Status;
        QueueSave();
    }
    public Task RestoreAsync() => RunAsync(async () =>
    {
        var intent = _intent;
        try
        {
            await EnterAsync(_selected);
            if (_closing || _suspended || intent != _intent) return;
            if (_autoDisplay)
            {
                if (Ready && _main.Devices.TestDisplayCommand.CanExecute(null)) await ApplyCoreAsync();
                else Status = "自动显示未启动：目标副屏未连接或场景文件未就绪。请确认后手动应用。";
            }
        }
        finally { _restoring = false; }
    }, true);
    private async Task EnterAsync(string name)
    {
        if (_closing) return;
        _fileReady = false;
        if (_main.Scene is VideoScene && name != "视频")
        {
            _main.PreviewScene(_clock);
        }
        switch (name)
        {
            case "时钟": _main.PreviewScene(_clock); break;
            case "性能": _main.PreviewScene(_performance); break;
            case "图片":
                if (string.IsNullOrWhiteSpace(_imagePath)) { Status = "请选择本地 JPG / PNG 图片。"; return; }
                await LoadFileAsync(_imagePath); return;
            case "组合":
                if (string.IsNullOrWhiteSpace(_dashboardPath)) { Status = "请选择一张静态壁纸作为组合场景背景。"; return; }
                await LoadFileAsync(_dashboardPath); return;
            case "视频":
                if (string.IsNullOrWhiteSpace(_videoPath)) { Status = "请选择本地 MP4/H.264 视频。"; return; }
                await LoadFileAsync(_videoPath); return;
        }
        Status = "场景预览已就绪，点击“应用到副屏”显示。";
    }
    private async Task LoadFileAsync(string path)
    {
        _fileReady = false;
        if (!File.Exists(path)) { Status = "文件不存在或已被删除，请重新选择文件。"; return; }
        if (_selected is "图片" or "组合")
        {
            var previous = _main.Scene;
            await _main.Images!.LoadAsync(path, false);
            Status = _main.Images.Status;
            if ((!ReferenceEquals(previous, _main.Scene) || _main.Scene is ImageScene) && _main.Scene is ImageScene image && image.Path == Path.GetFullPath(path))
            {
                if (_selected == "组合")
                {
                    _dashboardPath = image.Path;
                    _dashboard.SetBackground(image);
                    _main.PreviewScene(_dashboard);
                    Status = "组合场景已就绪，可调整布局后显示到 D92。";
                }
                else _imagePath = image.Path;
                _fileReady = true;
            }
        }
        else
        {
            _videoPath = path;
            await _main.Videos!.OpenAsync(path);
            var timeout = DateTime.UtcNow.AddSeconds(20);
            while (!_closing && !_suspended && _main.Videos.Status == "正在打开视频…" && DateTime.UtcNow < timeout)
                await Task.Delay(50);
            Status = _main.Videos.Status;
        }
        QueueSave();
    }
    private async Task ApplyCoreAsync()
    {
        if (_closing || _suspended || !Ready) { Status = "当前场景尚未就绪，未打开副屏。"; return; }
        var intent = _intent;
        if (_selected is "图片" or "组合")
        {
            var path = _selected == "组合" ? _dashboardPath : _imagePath;
            if (!File.Exists(path)) { Status = "图片已被删除，请重新选择。"; return; }
            await LoadFileAsync(path!);
            if (!_fileReady) return;
        }
        if (_closing || _suspended || intent != _intent) return;
        await _main.Devices.ApplySceneAsync(_main.Scene);
        Status = _output.Status;
        QueueSave();
    }
    private Task RunAsync(Func<Task> action, bool startup = false)
    {
        if (startup ? _busy || _closing || _suspended : !CanEdit) return Task.CompletedTask;
        _busy = true; Notify();
        _pending = ExecuteAsync(action);
        return _pending;
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { _logger.Error(exception, "Scene workspace operation failed"); Status = OutputTroubleshooting.DescribeFailure("内容操作",exception.Message);_main.Output.Report(Status,true); }
        finally { _busy = false; Notify(); }
    }
    private void QueueSave(bool duringClose = false)
    {
        if (_restoring || (_closing && !duringClose)) return;
        var selected = _selected; var image = _imagePath; var video = _videoPath; var dashboardImage = _dashboardPath;
        var mode = _main.Images!.Mode; var fps = _main.Videos!.Fps; var auto = _autoDisplay;
        var dashboardLayout = _dashboard.Layout; var dashboardPerformance = _dashboard.ShowPerformance;
        _saveTail = SaveAfterAsync(_saveTail, settings =>
        {
            settings.LastScene = selected; settings.LastImagePath = image; settings.ImageMode = mode;
            settings.LastVideoPath = video; settings.VideoFps = fps; settings.AutoDisplayOnStartup = auto;
            settings.DashboardImagePath = dashboardImage; settings.DashboardLayout = dashboardLayout;
            settings.DashboardShowPerformance = dashboardPerformance;
        });
    }
    private async Task SaveAfterAsync(Task previous, Action<AppSettings> update)
    {
        try { await previous; if (!await _settings.UpdateAsync(update)) Status = "设置保存失败，本次修改可能无法在下次启动恢复。"; }
        catch (Exception exception) { _logger.Error(exception, "Scene preference write failed"); Status = "设置保存失败，请查看日志。"; }
    }
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Scene)) return;
        if (_main.Scene is ImageScene image) _imagePath = image.Path;
        if (_main.Scene is VideoScene video) _videoPath = video.Path;
        Notify();
    }
    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageSceneViewModel.Mode) or nameof(VideoSceneViewModel.Fps)) QueueSave();
        Notify();
    }
    private void OnOutputChanged(object? sender, EventArgs e) => Notify();
    private void OnUsbChanged(object? sender, PropertyChangedEventArgs e) => Notify();
    private void Notify()
    {
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(PreviewStatus)); OnPropertyChanged(nameof(OutputStatus));
        ApplyCommand.NotifyCanExecuteChanged(); SelectFileCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged();
        ApplyUsbCommand.NotifyCanExecuteChanged();
    }
    private void OnDiagnosticTick(object? sender, EventArgs e) => UpdateDiagnostics();
    private void UpdateDiagnostics()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var sample = _sensors.Current;
            static string Available(double? value) => value.HasValue ? "可用" : "--（不可用）";
            Diagnostics = $"{OutputStatus}\n视频：{_main.Videos!.Status}\nCPU 负载：{Available(sample.CpuLoad)}　温度：{Available(sample.CpuTemperature)}\nGPU 负载：{Available(sample.GpuLoad)}　温度：{Available(sample.GpuTemperature)}\nRAM：{Available(sample.MemoryLoad)}\n进程内存：{process.PrivateMemorySize64 / 1048576d:0.0} MB　句柄：{process.HandleCount}\n运行时间：{DateTime.Now - process.StartTime:hh\\:mm\\:ss}\n硬件解码：由 Windows 决定，当前未提供确认指标。";
        }
        catch (Exception exception) { _logger.Error(exception, "Diagnostic refresh failed"); Diagnostics = "诊断信息暂不可用。"; }
    }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        _ = _dispatcher.InvokeAsync(() => HandlePowerEvent(e.Mode));
    }
    private async void HandlePowerEvent(PowerModes mode)
    {
        try
        {
            if (_closing) return;
            if (mode == PowerModes.Suspend) Suspend();
            else if (mode == PowerModes.Resume) await ResumeAsync();
        }
        catch (Exception exception) { _logger.Error(exception, "Power transition failed"); Status = "电源状态恢复失败，请手动刷新设备。"; }
    }
    public void Suspend()
    {
        _main.Library?.StopRotation();
        _main.Usb?.RequestStop();
        ++_intent; _suspended = true;
        _main.Devices.CloseRender(); _main.Videos!.StopPlayback();
        Status = "系统休眠：已停止副屏和视频。"; Notify();
    }
    public async Task ResumeAsync()
    {
        if (_closing) return;
        _suspended = false;
        await _pending;
        if (_closing) return;
        await RunAsync(async () =>
        {
            await _main.Devices.RefreshAsync();
            await EnterAsync(_selected);
            Status = "唤醒后已刷新设备及预览，请确认目标副屏后手动应用。";
        });
    }
    public async Task PrepareForCloseAsync()
    {
        if (_closing) { await _pending; await _saveTail; return; }
        ++_intent; _closing = true; _diagnosticTimer.Stop();
        _main.Videos!.StopPlayback();
        await _pending;
        QueueSave(true);
        await _saveTail;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _closing = true;
        _dashboard.Dispose();
        _diagnosticTimer.Stop(); _diagnosticTimer.Tick -= OnDiagnosticTick;
        if (_watchPower) SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _main.PropertyChanged -= OnMainChanged;
        if (_main.Usb is not null) _main.Usb.PropertyChanged -= OnUsbChanged;
        _main.Images!.PropertyChanged -= OnEditorChanged; _main.Videos!.PropertyChanged -= OnEditorChanged;
        _output.StateChanged -= OnOutputChanged; _main.Devices.TestDisplayCommand.CanExecuteChanged -= OnOutputChanged;
    }
    private static string LayoutName(DashboardLayout layout) => layout switch
    {
        DashboardLayout.ClockLeft => "时钟居左",
        DashboardLayout.ClockCenter => "时钟居中",
        _ => "时钟与性能"
    };
    private static bool TryLayout(string value, out DashboardLayout layout)
    {
        layout = value switch
        {
            "时钟居左" => DashboardLayout.ClockLeft,
            "时钟居中" => DashboardLayout.ClockCenter,
            "时钟与性能" => DashboardLayout.Split,
            _ => (DashboardLayout)(-1)
        };
        return Enum.IsDefined(layout);
    }
    public static string Name(Scene scene) => scene switch { ThemeScene => "自定义主题", DashboardScene => "组合", ImageScene => "图片", VideoFrameScene => "视频", VideoScene => "视频", PerformanceScene => "性能", StockScene => "股票行情", RetroCameraClockScene => "复古相机翻页时钟", _ => "时钟" };
}
