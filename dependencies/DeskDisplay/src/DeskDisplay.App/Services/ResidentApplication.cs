using Microsoft.Win32;

namespace DeskDisplay.App.Services;

/// <summary>Desktop shell integration only; no device/output ownership.</summary>
internal sealed class ResidentApplication : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    public ResidentApplication(Action show,Action exit)
    {
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开 D92 Studio",null,(_,_)=>show());
        menu.Items.Add("退出并停止输出",null,(_,_)=>exit());
        _icon=new(){Icon=System.Drawing.SystemIcons.Application,Text="D92 Studio",ContextMenuStrip=menu,Visible=true};
        _icon.DoubleClick+=(_,_)=>show();
    }
    public static Task SetLoginStartupAsync(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        if(enabled)
        {
            var path=Environment.ProcessPath??throw new InvalidOperationException("无法确定程序路径。");
            if(!string.Equals(System.IO.Path.GetFileName(path),"DeskDisplay.App.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请在已安装版本中启用登录启动。");
            key.SetValue("D92Studio",'"'+path+'"');
        }
        else key.DeleteValue("D92Studio",false);
        return Task.CompletedTask;
    }
    public void Dispose(){_icon.Visible=false;_icon.ContextMenuStrip?.Dispose();_icon.Dispose();}
}
