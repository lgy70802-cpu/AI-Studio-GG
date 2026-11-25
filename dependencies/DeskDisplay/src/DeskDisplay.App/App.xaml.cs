using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using DeskDisplay.App.ViewModels;
using DeskDisplay.App.Views;
using DeskDisplay.Display.Services;
using DeskDisplay.Infrastructure.Configuration;
using DeskDisplay.Infrastructure.Logging;
using Serilog;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Sensors;
using DeskDisplay.Library;
using DeskDisplay.Core.Devices;
using DeskDisplay.Core.Output;

namespace DeskDisplay.App;

public partial class App : Application
{
    private WindowsDisplayManager? _displayManager;
    private DevicesViewModel? _devicesViewModel;
    private RenderWindowService? _renderService;
    private ClockSceneDataProvider? _clockProvider;
    private MainViewModel? _mainViewModel;
    private SensorService? _sensors;
    private PerformanceSceneDataProvider? _performanceProvider;
    private bool _closeAfterSave;
    private Task? _closingTask;
    private WindowsDeviceDiscovery? _multiDeviceDiscovery;
    private Services.ResidentApplication? _resident;
    private bool _exitRequested;
    public DeviceRegistry? DeviceRegistry { get; private set; }
    public OutputSessionManager? OutputSessions { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        Log.Logger = LoggingSetup.CreateLogger();
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        try
        {
            base.OnStartup(e);
            SessionEnding+=(_,_)=>_exitRequested=true;
            Log.Information("DeskDisplay starting");
            var dataRoot=DeskDisplay.Core.Storage.StudioDataPaths.Root;
            var firstStartSmoke=Environment.GetEnvironmentVariable("D92_STUDIO_FIRST_START_SMOKE")=="1";
            if(firstStartSmoke&&(!DeskDisplay.Core.Storage.StudioDataPaths.IsIsolated||System.IO.File.Exists(System.IO.Path.Combine(dataRoot,"settings.json"))))throw new InvalidOperationException("首次启动自检必须使用全新的隔离数据目录。");
            var pendingRestore=System.IO.Path.Combine(dataRoot,"pending-restore.d92backup");
            if(System.IO.File.Exists(pendingRestore))
            {
                try{var restored=await Task.Run(()=>UserDataBackup.RestoreAsync(pendingRestore,dataRoot));System.IO.File.Move(pendingRestore,pendingRestore+".completed-"+Guid.NewGuid().ToString("N"));Log.Information("Restored user backup: {Files} files; {Conflicts} preserved conflicts",restored.Files,restored.Conflicts);}
                catch(Exception restoreError){Log.Warning(restoreError,"Backup restoration failed; preserving archive");MessageBox.Show("备份恢复未完成，原配置已保留："+restoreError.Message,"恢复备份");}
            }
            var settingsService = new JsonSettingsService(Log.Logger);
            var settings = await settingsService.LoadAsync();
            _displayManager = new WindowsDisplayManager(Log.Logger);
            var targetService = new TargetDisplayService(settings, settingsService, Log.Logger);
            var clockScene = new ClockScene();
            _clockProvider = new ClockSceneDataProvider(clockScene, Log.Logger);
            _clockProvider.Start();
            _renderService = new RenderWindowService(Log.Logger, clockScene);
            _devicesViewModel = new DevicesViewModel(_displayManager, targetService, Log.Logger, _renderService);
            _mainViewModel = new MainViewModel(_devicesViewModel, clockScene);
            _mainViewModel.ConfigureHome(settingsService, settings);
            _mainViewModel.ConfigureOutputExperience(settingsService,settings);
            _mainViewModel.SetLoginStartupAsync=Services.ResidentApplication.SetLoginStartupAsync;
            _mainViewModel.SetUsb(new UsbOutputViewModel(_mainViewModel, settingsService, settings, Log.Logger));
            _devicesViewModel.BeforeOpenOutputAsync = _mainViewModel.Usb!.StopAsync;
            _mainViewModel.SetImageEditor(new ImageSceneViewModel(_devicesViewModel, settingsService, settings,
                clockScene, _mainViewModel.PreviewScene, Log.Logger));
            _mainViewModel.SetVideoEditor(new VideoSceneViewModel(_mainViewModel, new WindowsVideoPlaybackService(Log.Logger), Log.Logger));
            _sensors = new SensorService(Log.Logger);
            await _sensors.StartAsync();
            var performanceScene = new PerformanceScene();
            _performanceProvider = new PerformanceSceneDataProvider(_sensors, performanceScene, Log.Logger,
                () => (_mainViewModel.SelectedNavigation == "首页" && (_mainViewModel.Scene is DashboardScene || ReferenceEquals(_mainViewModel.Scene, performanceScene)))
                    || (_renderService.CurrentScene is DashboardScene || ReferenceEquals(_renderService.CurrentScene, performanceScene))
                    || (_mainViewModel.Usb is {IsRunning:true} usb && (usb.Scene is DashboardScene || ReferenceEquals(usb.Scene,performanceScene))));
            _mainViewModel.SetPerformanceScene(performanceScene, Log.Logger);
            _mainViewModel.SetWorkspace(new WorkspaceViewModel(_mainViewModel, clockScene, performanceScene,
                _renderService, settingsService, _sensors, settings, Log.Logger));
            _mainViewModel.SetPlugins(new PluginManagerViewModel(_mainViewModel, settingsService, settings, Log.Logger));
            _mainViewModel.SetLibrary(new LibraryViewModel(_mainViewModel, new WallpaperCatalog(Log.Logger), Log.Logger));
            _mainViewModel.SetCloud(new CloudLibraryViewModel(_mainViewModel, settingsService, settings, Log.Logger));
            _mainViewModel.SetAccount(new StudioAccountViewModel(_mainViewModel, settings.StudioServerUrl, Log.Logger));
            _mainViewModel.SetCreator(new CreatorViewModel(_mainViewModel, Log.Logger));
            _mainViewModel.Creator!.ConfirmLeave = project => { var prompt = new CreatorSavePrompt(project.Name) { Owner = MainWindow }; prompt.ShowDialog(); return prompt.Choice; };
            MainWindow = new MainWindow { DataContext = _mainViewModel };
            if(firstStartSmoke){MainWindow.ShowActivated=false;MainWindow.ShowInTaskbar=false;MainWindow.Left=-20000;MainWindow.Top=-20000;}
            MainWindow.Closing += OnMainWindowClosing;
            MainWindow.Show();
            try { _resident=new(()=>Dispatcher.Invoke(()=>{MainWindow.Show();MainWindow.WindowState=WindowState.Normal;MainWindow.Activate();}),()=>Dispatcher.Invoke(()=>{_exitRequested=true;MainWindow.Close();})); }
            catch(Exception ex){Log.Warning(ex,"Tray unavailable; close-to-tray remains disabled");}
            Log.Information("Main window shown");
            _ = _mainViewModel.Cloud!.InitializeAsync();
            _ = _mainViewModel.Account!.InitializeAsync();
            await _devicesViewModel.RefreshAsync();
            await _mainViewModel.Workspace!.RestoreAsync();
            await _mainViewModel.Library!.InitializeAsync();
            await _mainViewModel.Creator!.InitializeAsync();
            await _mainViewModel.Usb!.RefreshAsync();
            // Discovery/UI are enabled by default; output still requires an explicit per-device action.
            if (Environment.GetEnvironmentVariable("DESKDISPLAY_MULTI_DEVICE") != "0")
            {
                DeviceRegistry = new DeviceRegistry();
                var factory = new WindowsThemeSessionFactory(Dispatcher, _displayManager, Log.Logger,
                    layer => _mainViewModel.Library?.AllItems.FirstOrDefault(i => i.Asset.Id == layer.AssetId)?.Asset.Path ?? layer.Path,
                    layer => layer.Kind switch
                    {
                        ThemeElementKind.StockPlugin when _mainViewModel.Plugins?.Enabled == true => _mainViewModel.Plugins.Scene,
                        ThemeElementKind.CameraClock when _mainViewModel.Plugins?.CameraEnabled == true => _mainViewModel.Plugins.CameraScene,
                        _ => null
                    }, videoOptions: serial => _mainViewModel.Usb!.GetVideoPreferences(serial),
                    videoMetrics: (serial, rate) => _mainViewModel.Usb!.ReportVideoThroughput(serial, rate),
                    videoCompleted: (serial, themeId) => Dispatcher.BeginInvoke(async () => { if (_mainViewModel.ActiveMixedPlaylist is { } list) await list.VideoCompletedAsync(serial, themeId); }),
                    videoPreview: (serial, themeId, frame) => _mainViewModel.Devices.Managed?.PublishVideoFrame(serial, themeId, frame));
                OutputSessions = new OutputSessionManager(DeviceRegistry, WindowsDeviceLeaseProvider.Shared, factory.CreateAsync);
                _multiDeviceDiscovery = new(DeviceRegistry, _displayManager, Log.Logger);
                _multiDeviceDiscovery.Start();
                var managed = new ManagedDevicesViewModel(DeviceRegistry, OutputSessions,
                    new DevicePreferenceStore(System.IO.Path.Combine(dataRoot, "managed-devices.v1.json")),
                    () => (_mainViewModel.Scene is ThemeScene current ? new[] { current.Project } : Array.Empty<ThemeProject>())
                        .Concat(_mainViewModel.Creator?.Projects.Select(c => c.Project) ?? []).ToArray(),
                    () => _multiDeviceDiscovery.DisplaySnapshot, _multiDeviceDiscovery.RefreshAsync, Log.Logger, settings.UsbOutput);
                _devicesViewModel.AttachManaged(managed);
                managed.Coordinator=_mainViewModel.Output;
                managed.ExplicitStopRequested=card=>_mainViewModel.ActiveMixedPlaylist?.ObserveStop(card);
                managed.ContentAccepted=(card,source)=>
                {
                    _mainViewModel.ActiveMixedPlaylist?.ObserveAccepted(card);
                    _mainViewModel.ClearDevicePreview();
                    if(card.Device.Identity.Confidence==IdentityConfidence.VerifiedStable && card.Device.Identity.SerialNumber is {Length:>0} serial)
                    {
                        var remember=_mainViewModel.DescribeManagedOutput(serial,source);
                        _mainViewModel.RememberOutput(remember,serial);
                    }
                    return _mainViewModel.RuntimePending;
                };
                managed.ThemeUseAccepted = id => _mainViewModel.Creator!.Projects.Any(p=>p.Project.Id==id)?_mainViewModel.Creator.RecordThemeUseAsync(id):Task.CompletedTask;
                managed.PreviewFactory = factory.CreatePreviewAsync;
                managed.HomeOutputChanged = _mainViewModel.SetHomeOutputScene;
                var legacyUsb=_mainViewModel.Usb!;
                legacyUsb.ReleaseManagedOutputAsync=managed.ReleaseForLegacyUsbAsync;
                managed.CaptureLegacyRestore=card=>
                {
                    if(!legacyUsb.OwnsOutputForSerial(card.Device.Identity.SerialNumber??""))return null;
                    var scene=legacyUsb.Scene;var serial=legacyUsb.SelectedSerial;var width=legacyUsb.Width;var height=legacyUsb.Height;var rotation=legacyUsb.Rotation;
                    return async()=>{if(legacyUsb.SelectedSerial!=serial||legacyUsb.Width!=width||legacyUsb.Height!=height||legacyUsb.Rotation!=rotation)throw new InvalidOperationException("原 USB 目标或参数已变化。");if(scene is VideoFrameScene video){if(!await legacyUsb.RestoreVideoAsync(video.Path))throw new InvalidOperationException(legacyUsb.Status);}else await legacyUsb.ApplySceneAsync(scene);if(!legacyUsb.IsRunning)throw new InvalidOperationException(legacyUsb.Status);};
                };
                legacyUsb.ContentAccepted=(serial,scene)=>
                {
                    _mainViewModel.ActiveMixedPlaylist?.ObserveLegacy(serial);
                    _mainViewModel.ClearDevicePreview();
                    _mainViewModel.RememberLegacyOutput(serial,scene);
                    return _mainViewModel.RuntimePending;
                };
                managed.HasLegacyOwnedOutput=card=>card.Device.Transport==DeviceTransport.UsbHid && card.Device.Identity.SerialNumber is { Length: >0 } serial && legacyUsb.OwnsOutputForSerial(serial);
                managed.ReleaseLegacyOwnedOutputAsync=async card=>
                {
                    if(card.Device.Identity.SerialNumber is not { Length: >0 } serial || !legacyUsb.OwnsOutputForSerial(serial))return false;
                    if(_mainViewModel.Library?.RotationTarget=="D92 USB")_mainViewModel.Library.StopRotation();
                    if(_mainViewModel.Creator?.ThemePlaylistTarget=="D92 USB")_mainViewModel.Creator.StopThemePlaylist();
                    return await legacyUsb.StopOwnedSerialAsync(serial);
                };
                await managed.InitializeAsync();
                try{await _mainViewModel.InitializeContentListsAsync();}catch(Exception listError){Log.Warning(listError,"Content lists unavailable; preserving files");_mainViewModel.Output.Report("固定内容或混合轮播未能读取："+listError.Message,true);}
                if(_mainViewModel.Output.CompletedTransitions==0)await _mainViewModel.RestoreRememberedOutputsAsync(content=>Services.D92StartupRestore.RestoreAsync(_mainViewModel,managed,content));
                Log.Information("Multi-device page enabled; startup restore preference {Restore}",_mainViewModel.RestoreD92OnStartup);
            }
            if(firstStartSmoke)
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var safe=!_mainViewModel.Usb!.IsRunning&&(OutputSessions?.Snapshot.Length??0)==0&&!settings.AutoDisplayOnStartup&&!settings.RestoreD92OnStartup&&!settings.LaunchAtLogin;
                var proof=new {Success=safe,Version=_mainViewModel.AppVersionLabel,DataRoot=dataRoot,RuntimeDirectory=System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),MainWindowLoaded=MainWindow.IsLoaded,StandaloneOutputRunning=_mainViewModel.Usb.IsRunning,ManagedOutputRecords=OutputSessions?.Snapshot.Length??0,settings.AutoDisplayOnStartup,settings.RestoreD92OnStartup,settings.LaunchAtLogin,Environment.OSVersion.VersionString};
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(dataRoot,"first-start-smoke.json"),System.Text.Json.JsonSerializer.Serialize(proof,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                if(!safe)throw new InvalidOperationException("首次启动自检检测到不应发生的自动输出。");
                _exitRequested=true;MainWindow.Close();
            }
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application startup failed");
            if(Environment.GetEnvironmentVariable("D92_STUDIO_FIRST_START_SMOKE")=="1"){Shutdown(1);return;}
            MessageBox.Show("程序启动失败，请查看 DeskDisplay 日志。", "DeskDisplay");
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if(!_exitRequested && _resident is not null && _mainViewModel?.CloseToTray==true && !_closeAfterSave)
        { e.Cancel=true;MainWindow.Hide();return; }
        if (_closeAfterSave || _devicesViewModel is null) return;
        e.Cancel = true;
        if (_closingTask is not null) return;
        _closingTask = PrepareForCloseAsync();
        if (_closingTask.IsCompletedSuccessfully)
        {
            _closeAfterSave = true;
            e.Cancel = false;
            return;
        }
        MainWindow.IsEnabled = false;
        try { await _closingTask; }
        catch (Exception exception)
        {
            if (exception is not OperationCanceledException) Log.Error(exception, "Theme save failed; keeping the editor open");
            _closingTask = null;
            _mainViewModel?.CancelOutputShutdown();
            MainWindow.IsEnabled = true;
            if (_mainViewModel is not null) _mainViewModel.SelectedPage = AppPage.Creator;
            return;
        }
        _closeAfterSave = true;
        _ = MainWindow.Dispatcher.BeginInvoke(new Action(MainWindow.Close));
    }

    private async Task PrepareForCloseAsync()
    {
        // Persist editable work before other services enter their irreversible closing state.
        await (_mainViewModel?.CaptureOutputShutdownAsync() ?? Task.CompletedTask);
        await (_mainViewModel?.Creator?.PrepareForCloseAsync() ?? Task.CompletedTask);
        try
        {
            await Task.WhenAll(_mainViewModel?.Library?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.Usb?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.Workspace?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _devicesViewModel?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.Images?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.Videos?.PrepareForCloseAsync() ?? Task.CompletedTask,
            _mainViewModel?.Cloud?.PrepareForCloseAsync() ?? Task.CompletedTask,
            OutputSessions?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            _multiDeviceDiscovery?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            _sensors?.StopAsync() ?? Task.CompletedTask);
        }
        catch (Exception exception) { Log.Error(exception, "Service shutdown failed after theme persistence"); }
    }
    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception");
        e.Handled = true;
        MessageBox.Show("发生未处理错误，程序将退出。详情已记录到日志。", "DeskDisplay");
        Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled process exception; terminating: {Terminating}", e.IsTerminating);
        Log.CloseAndFlush();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _resident?.Dispose();
        _performanceProvider?.Dispose();
        try { _sensors?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch (Exception exception) { Log.Error(exception, "Sensor service shutdown failed"); }
        try { _clockProvider?.Dispose(); }
        catch (Exception exception) { Log.Error(exception, "Failed to dispose shared clock provider"); }
        _mainViewModel?.Dispose();
        try { _devicesViewModel?.Dispose(); }
        catch (Exception exception) { Log.Error(exception, "Failed to dispose devices view model"); }
        try { _displayManager?.Dispose(); }
        catch (Exception exception) { Log.Error(exception, "Failed to dispose display manager"); }
        try { _renderService?.Dispose(); }
        catch (Exception exception) { Log.Error(exception, "Failed to dispose render service"); }
        Log.Information("DeskDisplay exiting with code {ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

