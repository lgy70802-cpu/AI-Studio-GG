using System.Windows;
namespace MiraBox.App;
public partial class App : Application
{
    private Mutex? instance;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        if (e.Args is [WorkspaceIdentity.Argument, var workspace])
            Environment.SetEnvironmentVariable("MIRABOX_DATA_DIR", WorkspaceIdentity.Normalize(workspace));
        var directory=Environment.GetEnvironmentVariable("MIRABOX_DATA_DIR")??System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiraBoxStudio.Next");
        var hash=WorkspaceIdentity.Hash(directory);
        instance=new Mutex(false,"Local\\MiraBoxV67-"+hash);
        try{ownsInstance=instance.WaitOne(0);}catch(AbandonedMutexException){ownsInstance=true;}
        if(!ownsInstance){MessageBox.Show("此工作区已有 MiraBox Studio 正在运行。请切换到现有窗口，或先关闭后再启动。","MiraBox Studio");Shutdown();return;}
        try { var window = new StudioWindow(); MainWindow = window; if (e.Args.Contains("--autostart")) window.WindowState = WindowState.Minimized; window.Show(); }
        catch (Exception ex)
        {
            var recovery = new StudioRecoveryWindow(directory, ex.Message); MainWindow = recovery; recovery.Show();
            foreach (var orphan in Windows.Cast<Window>().Where(w => w != recovery).ToArray()) orphan.Close();
        }
    }
    protected override void OnExit(ExitEventArgs e){if(ownsInstance)instance?.ReleaseMutex();instance?.Dispose();base.OnExit(e);}
}
