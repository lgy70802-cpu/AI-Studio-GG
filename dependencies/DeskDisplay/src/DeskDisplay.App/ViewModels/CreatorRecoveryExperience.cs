using DeskDisplay.Library;
namespace DeskDisplay.App.ViewModels;
public sealed partial class CreatorViewModel
{
    public Func<ThemeDraft,Task<bool>>? ConfirmDraftRecovery{get;set;}
    public string EditorSaveSummary=>_saving?"正在保存…":_dirty?_lastDraftProject==Project.Id&&_lastDraftRevision==_revision?"未保存修改 · 自动草稿已保护":"未保存修改 · 等待自动草稿":Projects.Any(p=>p.Project.Id==Project.Id)?"已保存的主题":"新主题 · 尚未保存";
    private async Task PreviewDraftRecoveryAsync(ThemeDraft draft)
    {
        if(ConfirmDraftRecovery is { } confirm){if(!await confirm(draft))return;}
        else
        {
            using var preview=new CreatorViewModel(_main,_logger,_store);
            var dialog=new DeskDisplay.App.Views.CreatorSourceBrowser([draft.Project],preview,"恢复草稿预览 · "+draft.SavedAt.LocalDateTime.ToString("MM-dd HH:mm:ss"),"恢复为独立副本"){Owner=System.Windows.Application.Current?.MainWindow};
            if(dialog.ShowDialog()!=true)return;
        }
        await ResolveUnsavedAsync();await RecoverDraftAsync(draft);
    }
}
