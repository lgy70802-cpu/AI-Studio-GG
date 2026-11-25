using System.Windows;
using DeskDisplay.App.ViewModels;
using Serilog;

namespace DeskDisplay.App.Views;

public partial class CreatorView
{
    private bool _quickPreviewBusy;
    private async void PreviewOnDevice(object sender, RoutedEventArgs e)
    {
        if (_quickPreviewBusy) return;
        if (DataContext is not MainViewModel { Creator: { } editor, Devices.Managed: { PreviewFactory: not null } devices }) return;
        CreatorDevicePreviewViewModel? model = null;
        _quickPreviewBusy=true; QuickDevicePreview.IsEnabled=false;
        try
        {
            CommitPreviewFields();
            model=new(editor.Project,devices,Log.Logger);
            var targets=new[]{devices.SelectConnectedD92(((MainViewModel)DataContext).Usb?.SelectedSerial)};
            model.Target=targets[0]; await model.PendingPreview;
            if(!model.ApplyTargetCommand.CanExecute(null))
            { MessageBox.Show(model.Readiness+"\n"+string.Join("\n",model.Issues),"设备预览"); return; }
            var main=(MainViewModel)DataContext;
            var previousScene=main.Usb is {IsRunning:true} currentUsb?currentUsb.Scene:main.Scene;
            var previousTheme=(previousScene as DeskDisplay.Core.Models.ThemeScene)?.Project.Clone();
            var previousResume=main.Usb?.SelectedSerial is { } previousSerial?main.DescribeLegacyOutput(previousSerial,previousScene):null;
            var restoreMode=model.Adaptation.Mode;
            var restore=await devices.PreviewTemporaryAsync(editor.Project,targets[0],model.Adaptation.Mode,async()=>
            {
                if(previousResume is {Kind:"ThemePlaylist" or "WallpaperPlaylist"})
                {await DeskDisplay.App.Services.D92StartupRestore.RestoreAsync(main,devices,previousResume);return;}
                if(previousTheme is not null)
                {
                    var revision=targets[0].AcceptedOutputRevision;
                    await devices.ApplySourceAsync(previousTheme,[targets[0].DeviceId],restoreMode,true);
                    if(targets[0].AcceptedOutputRevision==revision)throw new InvalidOperationException(targets[0].Message);
                    return;
                }
                if(main.Usb is not { } usb || usb.SelectedSerial!=targets[0].Device.Identity.SerialNumber)throw new InvalidOperationException("原 USB 目标已变化，请重新选择。");
                await usb.ApplySceneAsync(previousScene);
                if(!usb.IsRunning)throw new InvalidOperationException(usb.Status);
            });
            main.SetDevicePreview(editor.Project.Name,restore);
            editor.ReportDevicePreview("设备预览中，未保存主题。可在顶部结束预览并恢复原内容。");
        }
        catch(Exception ex) { Log.Warning(ex,"Quick device preview failed"); MessageBox.Show("设备预览失败："+ex.Message,"设备预览"); }
        finally { if(model is not null) await model.DisposeAsync(); _quickPreviewBusy=false; QuickDevicePreview.IsEnabled=true; }
    }
    private async void OpenDevicePreview(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Creator: { } editor, Devices.Managed: { PreviewFactory: not null } devices })
        { MessageBox.Show("请先在设备管理中启用并配置目标设备。", "按设备预览"); return; }
        CreatorDevicePreviewViewModel? model = null;
        try
        {
            CommitPreviewFields();
            model = new CreatorDevicePreviewViewModel(editor.Project, devices, Log.Logger);
            model.EditAdaptedLayout = editor.AddEditableLayout;
            model.SourceScene = editor.Scene;
            var project = editor.Project;
            model.SaveSourceAsync = async () => { if (!ReferenceEquals(editor.Project,project)) throw new InvalidOperationException("编辑项目已变化，请重新预览。"); await editor.SaveAsync(); };
            new CreatorDevicePreview(model) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (Exception ex) { Log.Error(ex, "Cannot open device preview"); MessageBox.Show("无法打开设备预览：" + ex.Message, "按设备预览"); }
        finally { if (model is not null) await model.DisposeAsync(); }
    }
    private void CommitPreviewFields()
    {
        Canvas.CommitInlineTextEdit();
        foreach(var text in Descendants<System.Windows.Controls.TextBox>(this).Where(t=>t.IsVisible && t.IsEnabled && !t.IsReadOnly).ToArray())
        {
            var binding=text.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            if(binding?.ParentBinding.Mode is System.Windows.Data.BindingMode.OneWay or System.Windows.Data.BindingMode.OneTime)continue;
            binding?.UpdateSource();
            if(binding?.HasValidationError==true)throw new InvalidOperationException("请先修正编辑器中尚未通过校验的输入。");
        }
        foreach(var combo in Descendants<System.Windows.Controls.ComboBox>(this).Where(c=>c.IsVisible && c.IsEnabled && c.IsEditable).ToArray())
            combo.GetBindingExpression(System.Windows.Controls.ComboBox.TextProperty)?.UpdateSource();
    }}


