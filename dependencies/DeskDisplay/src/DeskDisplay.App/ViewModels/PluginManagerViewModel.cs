using System.Diagnostics;
using System.Windows.Threading;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class PluginManagerViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly ISettingsService _settings;
    private readonly StockSceneDataProvider _provider;
    private readonly StockScene _scene;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _cameraTimer;
    private readonly RetroCameraClockScene _cameraScene;
    private Scene _activeScene;
    private bool _enabled;
    private bool _cameraEnabled;
    private string _symbols;
    private string _message = "股票插件已安装，可在这里管理和预览。";
    private string _cameraMessage = "复古相机翻页时钟已安装，使用本地时间运行。";
    private bool _disposed;

    public string Name => "A 股行情";
    public string Version => "0.10 · DeskDisplay 原生版";
    public string Description => "自选行情超宽组件，沿用原 Craft 插件的代码规则和 1920×462 布局。";
    public StockScene Scene => _scene;
    public RetroCameraClockScene CameraScene => _cameraScene;
    public Scene ActiveScene { get => _activeScene; private set { if (SetProperty(ref _activeScene, value)) OnPropertyChanged(nameof(ActivePluginTitle)); } }
    public string ActivePluginTitle => ReferenceEquals(ActiveScene, _cameraScene) ? "复古相机翻页时钟" : "A 股行情";
    public IReadOnlyList<int> CameraDurations { get; } = [260, 440, 760];
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value)) return;
            OnPropertyChanged(nameof(StateLabel));
            PreviewCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); ApplyUsbCommand.NotifyCanExecuteChanged();
            _timer.IsEnabled = value;
            _clockTimer.IsEnabled = value;
            _ = SaveAsync();
            if (value) RefreshCommand.Execute(null);
            else Message = "股票插件已停用；当前输出不会被强制关闭。";
        }
    }
    public string StateLabel => Enabled ? "已启用" : "已停用";
    public string Symbols { get => _symbols; set => SetProperty(ref _symbols, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string CameraMessage { get => _cameraMessage; private set => SetProperty(ref _cameraMessage, value); }
    public bool CameraEnabled
    {
        get => _cameraEnabled;
        set
        {
            if (!SetProperty(ref _cameraEnabled, value)) return;
            OnPropertyChanged(nameof(CameraStateLabel));
            _cameraTimer.IsEnabled = value;
            PreviewCameraCommand.NotifyCanExecuteChanged(); ApplyCameraUsbCommand.NotifyCanExecuteChanged();
            SaveCameraCommand.NotifyCanExecuteChanged(); DemoCameraCommand.NotifyCanExecuteChanged();
            _ = SaveAsync();
            CameraMessage = value ? "复古相机翻页时钟已启用。" : "复古相机翻页时钟已停用；当前输出不会被强制关闭。";
        }
    }
    public string CameraStateLabel => CameraEnabled ? "已启用" : "已停用";
    public bool CameraAnimate { get => _cameraScene.Animate; set { if (_cameraScene.Animate == value) return; _cameraScene.Animate = value; OnPropertyChanged(); } }
    public bool CameraHour12 { get => _cameraScene.Hour12; set { if (_cameraScene.Hour12 == value) return; _cameraScene.Hour12 = value; OnPropertyChanged(); } }
    public int CameraDuration { get => _cameraScene.DurationMs; set { if (_cameraScene.DurationMs == value) return; _cameraScene.DurationMs = value; OnPropertyChanged(); } }
    public int CameraCropY { get => _cameraScene.CropY; set { if (_cameraScene.CropY == value) return; _cameraScene.CropY = value; OnPropertyChanged(); } }
    public AsyncCommand PreviewCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ApplyUsbCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand PreviewCameraCommand { get; }
    public AsyncCommand ApplyCameraUsbCommand { get; }
    public AsyncCommand SaveCameraCommand { get; }
    public AsyncCommand DemoCameraCommand { get; }
    public ActionCommand SelectStockCommand { get; }
    public ActionCommand SelectCameraCommand { get; }

    public PluginManagerViewModel(MainViewModel main, ISettingsService settings, AppSettings initial, ILogger logger)
    {
        _main = main; _settings = settings;
        _enabled = initial.StockPluginEnabled;
        _symbols = string.IsNullOrWhiteSpace(initial.StockPluginSymbols) ? "600519,300750,600036,688981" : initial.StockPluginSymbols;
        _scene = new StockScene();
        _cameraScene = new RetroCameraClockScene
        {
            Animate = initial.RetroCameraAnimate,
            Hour12 = initial.RetroCameraHour12,
            DurationMs = initial.RetroCameraDurationMs,
            CropY = initial.RetroCameraCropY
        };
        _cameraEnabled = initial.RetroCameraPluginEnabled;
        _activeScene = _cameraScene;
        _provider = new StockSceneDataProvider(_scene, logger);
        SelectStockCommand = new ActionCommand(_ => ActiveScene = _scene);
        SelectCameraCommand = new ActionCommand(_ => ActiveScene = _cameraScene);
        PreviewCommand = new AsyncCommand(() =>
        {
            _main.PreviewScene(_scene);
            Message = "股票插件已载入预览；行情刷新与画面渲染彼此独立。";
            return Task.CompletedTask;
        }, () => Enabled, logger);
        RefreshCommand = new AsyncCommand(async () =>
        {
            await _provider.RefreshAsync(Symbols);
            Message = _scene.Status;
        }, () => Enabled, logger);
        ApplyUsbCommand = new AsyncCommand(async () =>
        {
            _main.PreviewScene(_scene);
            if (_main.Usb is not null) await _main.Usb.ApplyCurrentAsync();
            Message = _main.Usb?.Status ?? "USB 输出尚未初始化。";
        }, () => Enabled && _main.Usb?.CanApply == true, logger);
        SaveCommand = new AsyncCommand(async () =>
        {
            if (StockSceneDataProvider.Normalize(Symbols).Count == 0)
            {
                Message = "请输入 1–20 个有效代码，例如 600519、300750。";
                return;
            }
            await SaveAsync();
            await _provider.RefreshAsync(Symbols);
            Message = "插件设置已保存；" + _scene.Status;
        }, () => Enabled, logger);
        PreviewCameraCommand = new AsyncCommand(() =>
        {
            ActiveScene = _cameraScene;
            _main.PreviewScene(_cameraScene);
            CameraMessage = "复古相机翻页时钟已载入预览。";
            return Task.CompletedTask;
        }, () => CameraEnabled, logger);
        ApplyCameraUsbCommand = new AsyncCommand(async () =>
        {
            ActiveScene = _cameraScene;
            _main.PreviewScene(_cameraScene);
            if (_main.Usb is not null) await _main.Usb.ApplyCurrentAsync();
            CameraMessage = _main.Usb?.Status ?? "USB 输出尚未初始化。";
        }, () => CameraEnabled && _main.Usb?.CanApply == true, logger);
        SaveCameraCommand = new AsyncCommand(async () =>
        {
            await SaveAsync();
            CameraMessage = "相机时钟设置已保存。";
        }, () => CameraEnabled, logger);
        DemoCameraCommand = new AsyncCommand(() =>
        {
            ActiveScene = _cameraScene;
            _cameraScene.Demonstrate(Stopwatch.GetTimestamp());
            CameraMessage = "正在演示一次翻页。";
            return Task.CompletedTask;
        }, () => CameraEnabled, logger);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10), IsEnabled = Enabled };
        _timer.Tick += OnTick;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1), IsEnabled = Enabled };
        _clockTimer.Tick += OnClockTick;
        _cameraTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33), IsEnabled = CameraEnabled };
        _cameraTimer.Tick += OnCameraTick;
        _cameraScene.Tick(DateTimeOffset.Now, Stopwatch.GetTimestamp());
        if (Enabled) RefreshCommand.Execute(null);
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!_disposed && Enabled && (_main.SelectedNavigation == "插件" || ReferenceEquals(_main.Scene, _scene) || _main.Creator?.UsesScene(_scene) == true))
            await _provider.RefreshAsync(Symbols);
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        if (!_disposed && Enabled && (_main.SelectedNavigation == "插件" || ReferenceEquals(_main.Scene, _scene)))
            _scene.InvalidateClock();
    }

    private void OnCameraTick(object? sender, EventArgs e)
    {
        if (!_disposed && CameraEnabled && (_main.SelectedNavigation == "插件" || ReferenceEquals(_main.Scene, _cameraScene) || _main.Creator?.UsesScene(_cameraScene) == true))
            _cameraScene.Tick(DateTimeOffset.Now, Stopwatch.GetTimestamp());
    }

    private async Task SaveAsync()
    {
        if (!await _settings.UpdateAsync(settings =>
            {
                settings.StockPluginEnabled = Enabled;
                settings.StockPluginSymbols = Symbols;
                settings.RetroCameraPluginEnabled = CameraEnabled;
                settings.RetroCameraAnimate = CameraAnimate;
                settings.RetroCameraHour12 = CameraHour12;
                settings.RetroCameraDurationMs = CameraDuration;
                settings.RetroCameraCropY = CameraCropY;
            })) Message = "插件设置保存失败，请查看日志。";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
        _cameraTimer.Stop();
        _cameraTimer.Tick -= OnCameraTick;
        _provider.Dispose();
    }
}
