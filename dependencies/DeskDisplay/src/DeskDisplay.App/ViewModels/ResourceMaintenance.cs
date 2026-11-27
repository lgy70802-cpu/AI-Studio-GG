using DeskDisplay.Core.Models;
namespace DeskDisplay.App.ViewModels;
public sealed partial class MainViewModel
{
    public ActionCommand OpenResourceMaintenanceCommand=>_maintenance??=new(_=>new DeskDisplay.App.Views.ResourceMaintenance(this){Owner=System.Windows.Application.Current?.MainWindow}.ShowDialog(),_=>Library?.CanEdit==true);
    private ActionCommand? _maintenance;
    public async Task RelinkLibraryResourceAsync(Guid assetId,string path)
    {
        var old=Library?.AllItems.SingleOrDefault(i=>i.Asset.Id==assetId)?.Asset.Path??throw new InvalidOperationException("资源已移除。");
        await Library!.RelinkAssetAsync(assetId,path);
        if(Creator is { } creator)await creator.RepairSavedReferenceAsync(assetId,old,path);
    }
}
public sealed partial class CreatorViewModel
{
    public async Task RepairSavedReferenceAsync(Guid assetId,string oldPath,string newPath)
    {
        foreach(var source in await _store.ListAsync())
        {
            var layers=source.AllLayouts.SelectMany(l=>l.Layers).Where(l=>l.Kind is ThemeElementKind.Image or ThemeElementKind.Wallpaper).Where(l=>l.AssetId==assetId||string.Equals(l.Path,oldPath,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(layers.Length==0)continue;foreach(var layer in layers){layer.AssetId=assetId;layer.Path=newPath;}await _store.SaveAsync(source);
        }
        await RefreshProjectsAsync();Status="已更新资源库和已保存主题引用；当前编辑内容未改动，需要时请重新打开主题。";
    }
}
