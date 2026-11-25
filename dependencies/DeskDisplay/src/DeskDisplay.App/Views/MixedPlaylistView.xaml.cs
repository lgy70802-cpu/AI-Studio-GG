using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskDisplay.App.ViewModels;
namespace DeskDisplay.App.Views;
public partial class MixedPlaylistView:UserControl
{
    private Point _start;private Guid? _drag;
    private MixedPlaylistViewModel? Model=>DataContext as MixedPlaylistViewModel;
    public MixedPlaylistView(){InitializeComponent();Loaded+=async(_,_)=>{try{if(Model is { } model)await model.InitializeAsync();}catch(Exception ex){StudioDialog.ShowError("读取轮播失败", ex.Message, Window.GetWindow(this));}};}
    private async Task InvokeAsync(Func<MixedPlaylistViewModel,Task> action){try{if(Model is { } model)await action(model);}catch(Exception ex){Serilog.Log.Warning(ex,"Mixed playlist operation failed");StudioDialog.ShowError("轮播未完成", ex.Message, Window.GetWindow(this));}}
    private async void Start(object sender,RoutedEventArgs e){Rows.CommitEdit();Rows.CommitEdit(DataGridEditingUnit.Row,true);await InvokeAsync(model=>model.StartAsync());}
    private async void Next(object sender,RoutedEventArgs e)=>await InvokeAsync(model=>model.NextAsync());
    private void Pause(object sender,RoutedEventArgs e)=>Model?.TogglePause();
    private void Stop(object sender,RoutedEventArgs e)=>Model?.Stop();
    private void Add(object sender,RoutedEventArgs e){if(Candidate.SelectedItem is MixedCandidate row)Model?.Add(row.Content);}
    private async void SelectNamedList(object sender, SelectionChangedEventArgs e) { if (sender is ListBox { SelectedItem: NamedPlaylist list }) await InvokeAsync(model => model.SelectListAsync(list)); }
    private async void CreateNamedList(object sender, RoutedEventArgs e) { await InvokeAsync(model => model.CreateListAsync(NewListName.Text)); }
    private async void RenameList(object sender, RoutedEventArgs e) => await InvokeAsync(model => model.RenameListAsync(NewListName.Text));
    private async void CopyList(object sender, RoutedEventArgs e) => await InvokeAsync(model => model.CopyListAsync(NewListName.Text));
    private async void DeleteList(object sender, RoutedEventArgs e)
    {
        if (Model is not { CanEdit: true, SelectedList: { } selected }) return;
        if (MessageBox.Show(Window.GetWindow(this), $"删除轮播列表“{selected.Name}”？素材文件会保留。", "删除列表", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            await InvokeAsync(model => model.DeleteSelectedListAsync());
    }
    private void SetAllSeconds(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(AllSeconds.Text, out var seconds) && seconds is >= 10 and <= 86400) Model?.SetAllSeconds(seconds);
        else MessageBox.Show("请输入10至86400之间的秒数。", "停留时间");
    }
    private void Up(object sender,RoutedEventArgs e)=>Model?.Move(-1);
    private void Down(object sender,RoutedEventArgs e)=>Model?.Move(1);
    private void Remove(object sender,RoutedEventArgs e)=>Model?.Remove();
    private static T? Ancestor<T>(DependencyObject? source)where T:DependencyObject{while(source is not null){if(source is T found)return found;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}return null;}
    private void DragStart(object sender,MouseButtonEventArgs e){_start=e.GetPosition(Rows);_drag=(Ancestor<DataGridRow>(e.OriginalSource as DependencyObject)?.DataContext as MixedPlaylistRow)?.Entry.EntryId;}
    private void DragMove(object sender,MouseEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed||_drag is not Guid id||Model?.CanEdit!=true)return;var point=e.GetPosition(Rows);if(Math.Abs(point.X-_start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-_start.Y)<SystemParameters.MinimumVerticalDragDistance)return;_drag=null;DragDrop.DoDragDrop(Rows,new DataObject("D92.Mixed",id),DragDropEffects.Move);}
    private void DropItem(object sender,DragEventArgs e){if(e.Data.GetData("D92.Mixed") is Guid id&&Ancestor<DataGridRow>(e.OriginalSource as DependencyObject)?.DataContext is MixedPlaylistRow target){Model?.MoveTo(id,target.Entry.EntryId);e.Handled=true;}}
}
