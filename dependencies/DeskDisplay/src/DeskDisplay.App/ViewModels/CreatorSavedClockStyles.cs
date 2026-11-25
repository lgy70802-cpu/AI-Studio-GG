using DeskDisplay.Core.Models;
using DeskDisplay.Library;

namespace DeskDisplay.App.ViewModels;
public sealed partial class CreatorViewModel
{
    public IReadOnlyList<SavedClockStyle> SavedClockStyles { get; private set; } = [];
    private ActionCommand? _saveClockStyle, _addSavedClockStyle, _deleteClockStyle;
    public ActionCommand SaveClockStyleCommand => _saveClockStyle ??= new(_ => Track(SaveClockStyleAsync()), _ => CanEditSelection && IsClock);
    public ActionCommand AddSavedClockStyleCommand => _addSavedClockStyle ??= new(p =>
    {
        if (p is not SavedClockStyle item || !CanEdit || Project.ActiveLayout.Layers.Count >= 100) return;
        var layer = item.Layer.Clone(); layer.Id = Guid.NewGuid(); layer.Name = item.Name;
        SizeNewClock(layer); layer.Locked=false; layer.Visible=true; layer.GroupId=null;
        Insert(layer);
    }, _ => CanEdit);
    public ActionCommand DeleteClockStyleCommand => _deleteClockStyle ??= new(p => { if (p is SavedClockStyle item) Track(DeleteClockStyleAsync(item)); }, _ => CanEdit);
    public async Task RefreshClockStylesAsync() { SavedClockStyles = await _store.ListClockStylesAsync(); OnPropertyChanged(nameof(SavedClockStyles)); }
    public async Task SaveClockStyleAsync()
    {
        if (!CanEditSelection || SelectedLayer is not { Kind: ThemeElementKind.DigitalClock } layer) return;
        try { await _store.SaveClockStyleAsync(layer.Name,layer.Clone()); await RefreshClockStylesAsync(); Status="已保存到左侧“我的时钟样式”，可在其他主题中添加。"; }
        catch(Exception ex) { _logger.Warning(ex,"Cannot save clock style"); Status="时钟样式未保存："+ex.Message; }
    }
    private async Task DeleteClockStyleAsync(SavedClockStyle item)
    {
        try { await _store.DeleteClockStyleAsync(item.Id); await RefreshClockStylesAsync(); Status="已移除个人样式；画布中的图层保持不变。"; }
        catch(Exception ex) { _logger.Warning(ex,"Cannot delete clock style"); Status="样式未移除："+ex.Message; }
    }
}

