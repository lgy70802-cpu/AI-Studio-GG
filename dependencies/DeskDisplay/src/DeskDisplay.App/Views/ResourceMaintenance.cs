using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Library;
using Microsoft.Win32;
namespace DeskDisplay.App.Views;
public sealed class ResourceMaintenance:Window
{
    private readonly MainViewModel _main;private readonly DataGrid _grid=new(){AutoGenerateColumns=false,IsReadOnly=true};private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap};private readonly CancellationTokenSource _life=new();private bool _busy;
    public ResourceMaintenance(MainViewModel main)
    {
        _main=main;Title="资源检查与修复";Width=1040;Height=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=System.Windows.Media.Brushes.White;
        foreach(var dictionary in new[]{"Colors","Typography","Controls"})Resources.MergedDictionaries.Add(new(){Source=new Uri($"/DeskDisplay.App;component/Themes/{dictionary}.xaml",UriKind.Relative)});
        SetResourceReference(BackgroundProperty,"Brush.Background");SetResourceReference(ForegroundProperty,"Brush.Text");FontFamily=new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=13;
        _grid.SetResourceReference(StyleProperty,"SoftDataGrid");
        var panel=new DockPanel{Margin=new(24)};Content=panel;var toolbar=new WrapPanel{Margin=new(0,0,0,12)};DockPanel.SetDock(toolbar,Dock.Top);panel.Children.Add(toolbar);
        foreach(var (name,action) in new (string,Func<Task>)[]{("重新检查",ScanAsync),("重新定位所选缺失资源",LocateAsync),("按文件夹匹配缺失资源",FolderAsync)}){var button=new Button{Content=name,Padding=new(12,8,12,8),Margin=new(0,0,8,0)};button.Click+=async(_,_)=>{if(_busy)return;_busy=true;toolbar.IsEnabled=false;try{await action();}catch(OperationCanceledException){}catch(Exception ex){_status.Text=ex.Message;}finally{_busy=false;toolbar.IsEnabled=true;}};toolbar.Children.Add(button);}
        foreach(Button button in toolbar.Children)button.SetResourceReference(StyleProperty,"SoftButton");
        DockPanel.SetDock(_status,Dock.Bottom);_status.Margin=new(0,12,0,0);panel.Children.Add(_status);
        _grid.Columns.Add(new DataGridTextColumn{Header="名称",Binding=new System.Windows.Data.Binding("Name"),Width=200});_grid.Columns.Add(new DataGridTextColumn{Header="问题",Binding=new System.Windows.Data.Binding("Issue"),Width=130});_grid.Columns.Add(new DataGridTextColumn{Header="重复组",Binding=new System.Windows.Data.Binding("Group"),Width=110});_grid.Columns.Add(new DataGridTextColumn{Header="原路径",Binding=new System.Windows.Data.Binding("Path"),Width=new(1,DataGridLengthUnitType.Star)});panel.Children.Add(_grid);
        Loaded+=async(_,_)=>{try{await ScanAsync();}catch(OperationCanceledException){}catch(Exception ex){_status.Text=ex.Message;}};Closed+=(_,_)=>_life.Cancel();
    }
    private async Task ScanAsync(){_status.Text="正在检查文件和重复内容…";var assets=_main.Library!.AllItems.Select(i=>i.Asset).ToArray();var issues=await Task.Run(()=>ResourceAudit.ScanAsync(assets,_life.Token));_grid.ItemsSource=issues;_status.Text=$"发现 {issues.Count(i=>i.Issue=="文件缺失")} 个缺失资源、{issues.Count(i=>i.Issue=="内容重复")} 个重复条目。不会自动删除文件或资源。";}
    private async Task LocateAsync()
    {
        if(_grid.SelectedItem is not ResourceIssue {Issue:"文件缺失"} item){_status.Text="请选择一项缺失资源。";return;}
        var dialog=new OpenFileDialog{Filter="图片或视频|*.png;*.jpg;*.jpeg;*.mp4",Title="重新定位："+item.Name};if(dialog.ShowDialog(this)!=true)return;
        await _main.RelinkLibraryResourceAsync(item.AssetId,dialog.FileName);await ScanAsync();
    }
    private async Task FolderAsync()
    {
        var dialog=new OpenFolderDialog{Title="选择原素材所在文件夹"};if(dialog.ShowDialog(this)!=true)return;
        var assets=_main.Library!.AllItems.Select(i=>i.Asset).ToArray();var matches=await Task.Run(()=>ResourceAudit.FindFolderMatches(assets,dialog.FolderName));
        if(matches.Count==0){_status.Text="没有找到唯一同名匹配；重名文件不会自动选择。";return;}
        var preview=string.Join("\n",matches.Take(10).Select(m=>assets.First(a=>a.Id==m.Key).Name+" → "+System.IO.Path.GetFileName(m.Value)));
        if(MessageBox.Show(this,$"找到 {matches.Count} 项唯一同名匹配：\n{preview}\n确认更新路径？","预览修复",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
        foreach(var match in matches){_life.Token.ThrowIfCancellationRequested();await _main.RelinkLibraryResourceAsync(match.Key,match.Value);}await ScanAsync();
    }
}
