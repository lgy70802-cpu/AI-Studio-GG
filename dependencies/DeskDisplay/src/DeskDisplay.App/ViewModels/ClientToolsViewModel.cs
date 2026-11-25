using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;

public sealed class ClientToolsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private bool _busy;
    private string _status="";
    public string Status{get=>_status;private set=>SetProperty(ref _status,value);}
    public string Version=>_main.AppVersionLabel;
    public string ReleaseNotes=>"RC封闭测试：新增D69实验支持，请从设备管理或创作中心选择并确认参数，暂支持横屏静态与数字时钟主题；停止后可能返回设备Logo，视频及其他方向尚未开放。原有D92、本地壁纸、可编辑主题、视频背景和混合轮播保留。D92视频提供画质优先、流畅优先、自定义模式，实际帧率取决于素材与设备吞吐，不保证稳定30 FPS。首次启动不自动输出，安装升级保留用户数据。";
    public string Root{get;}
    public AsyncCommand BackupCommand{get;}
    public AsyncCommand RestoreCommand{get;}
    public AsyncCommand UpdateCommand{get;}
    public ActionCommand OpenInstallCommand{get;}
    public ClientToolsViewModel(MainViewModel main,string? root=null)
    {
        _main=main;Root=root??DeskDisplay.Core.Storage.StudioDataPaths.Root;
        BackupCommand=new(BackupAsync,()=>!_busy,Serilog.Log.Logger);RestoreCommand=new(RestoreAsync,()=>!_busy,Serilog.Log.Logger);UpdateCommand=new(UpdateAsync,()=>!_busy,Serilog.Log.Logger);
        OpenInstallCommand=new(_=>Open(AppContext.BaseDirectory));
    }
    private static void Open(string directory)=>Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={directory},UseShellExecute=true});
    private async Task RunAsync(Func<Task> action)
    {
        if(_busy)return;_busy=true;Notify();Status="正在处理…";
        try{await action();}catch(Exception ex){Status="未完成："+ex.Message;Serilog.Log.Warning(ex,"Client tools operation failed");}
        finally{_busy=false;Notify();}
    }
    private void Notify(){BackupCommand.NotifyCanExecuteChanged();RestoreCommand.NotifyCanExecuteChanged();UpdateCommand.NotifyCanExecuteChanged();}
    private Task BackupAsync()
    {
        var dialog=new SaveFileDialog{Filter="D92 备份|*.d92backup",FileName="D92-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".d92backup"};
        if(dialog.ShowDialog()!=true)return Task.CompletedTask;
        return RunAsync(async()=>{await _main.RuntimePending;await _main.FlushContentListsAsync();if(_main.Library is { } library)await library.FlushPersistenceAsync();if(_main.Creator is { } creator){await creator.PendingOperation;await creator.FlushThemePlaylistAsync();}
            var result=await Task.Run(()=>UserDataBackup.ExportAsync(Root,dialog.FileName));Status=$"备份完成：{result.Files.Count} 个文件，{result.Missing.Count} 个缺失素材。仅包含已保存项目，不含账号凭据与设备锁。";});
    }
    private Task RestoreAsync()
    {
        var dialog=new OpenFileDialog{Filter="D92 备份|*.d92backup"};if(dialog.ShowDialog()!=true)return Task.CompletedTask;
        return RunAsync(async()=>
        {
            var result=await Task.Run(()=>UserDataBackup.InspectAsync(dialog.FileName,Root));
            if(System.Windows.MessageBox.Show($"{result.Files} 个文件，{result.Conflicts} 个冲突，{result.Missing.Count} 个原始缺失素材。\n保留已有设置和同 ID 主题，资源库合并新增条目。下次启动时恢复；请先保存当前编辑。是否准备恢复？","导入备份",System.Windows.MessageBoxButton.OKCancel)!=System.Windows.MessageBoxResult.OK){Status="已取消恢复。";return;}
            var destination=Path.Combine(Root,"pending-restore.d92backup");if(File.Exists(destination))throw new IOException("已有待恢复备份，请先重启完成恢复。");
            await using(var input=File.OpenRead(dialog.FileName))await using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write))await input.CopyToAsync(output);
            Status="备份已准备，下次启动时合并恢复；已有配置不会被覆盖。";
        });
    }
    private Task UpdateAsync()
    {
        var dialog=new OpenFileDialog{Filter="D92 Windows 安装包|*.zip"};if(dialog.ShowDialog()!=true)return Task.CompletedTask;
        return RunAsync(async()=>{var staged=await Task.Run(()=>LocalUpdatePackage.StageAsync(dialog.FileName,Version,Path.Combine(Root,"Updates")));Open(staged);Status="更新包完整性校验通过。请保存并关闭软件后运行该目录中的 Install.ps1。哈希校验不代表发布者签名，仅使用可信来源的安装包。";});
    }
}
