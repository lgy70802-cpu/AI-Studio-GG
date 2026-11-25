using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;

namespace DeskDisplay.App.Views;

public partial class CreatorSourceBrowser : Window
{
    private readonly CreatorViewModel _preview;
    private readonly Func<ThemeProject, string, Task>? _rename;
    private readonly Func<ThemeProject, Task>? _delete;
    private readonly List<ThemeProject> _sources;
    private bool _closed;
    private int _generation;
    public ThemeProject? SelectedSource => Sources.SelectedItem as ThemeProject;
    public CreatorSourceBrowser(IReadOnlyList<ThemeProject> sources, CreatorViewModel preview, string title, string action, Func<Guid, string, Task>? rename = null, Func<Guid, Task>? delete = null)
    {
        InitializeComponent(); _preview = preview; _sources = sources.Select(s => s.Clone()).ToList(); _rename = rename is null ? null : (p, name) => rename(p.Id, name); _delete = delete is null ? null : p => delete(p.Id); Manage.Visibility = rename is null ? Visibility.Collapsed : Visibility.Visible; DataContext = preview; Title = title; Heading.Text = title; UseButton.Content = action;
        Closed += (_, _) => { _closed = true; _generation++; };
        Sources.ItemsSource = _sources; Feedback.Text = sources.Count == 0 ? "暂无保存记录" : "";
        if (sources.Count > 0) Sources.SelectedIndex = 0;
    }
    private async void SelectSource(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedSource is not { } source) { UseButton.IsEnabled = false; return; }
        TemplateName.Text = source.Name; var generation = ++_generation; UseButton.IsEnabled = false;
        try { await _preview.OpenProjectAsync(source.Clone()); if (_closed || generation != _generation) return; Feedback.Text = _preview.Status; UseButton.IsEnabled = true; }
        catch (Exception exception) { if (!_closed && generation == _generation) Feedback.Text = "预览失败：" + exception.Message; }
    }
    private async void Rename(object sender, RoutedEventArgs e)
    {
        if (_rename is null || SelectedSource is not { } source) return;
        Manage.IsEnabled = Sources.IsEnabled = false;
        try { await _rename(source, TemplateName.Text); source.Name = TemplateName.Text.Trim(); Sources.Items.Refresh(); }
        catch (Exception exception) { Feedback.Text = exception.Message; }
        finally { Manage.IsEnabled = Sources.IsEnabled = true; }
    }
    private async void Delete(object sender, RoutedEventArgs e)
    {
        if (_delete is null || SelectedSource is not { } source || MessageBox.Show(this, "删除该模板？原主题和素材不会删除。", "删除模板", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Manage.IsEnabled = Sources.IsEnabled = false;
        try { await _delete(source); _sources.Remove(source); Sources.Items.Refresh(); Sources.SelectedIndex = _sources.Count > 0 ? 0 : -1; if (_sources.Count == 0) { Feedback.Text = "暂无自定义模板"; SourcePreview.Visibility = Visibility.Collapsed; } }
        catch (Exception exception) { Feedback.Text = exception.Message; }
        finally { Manage.IsEnabled = Sources.IsEnabled = true; }
    }
    private void UseSource(object sender, RoutedEventArgs e) { if (SelectedSource is not null) DialogResult = true; }
}
