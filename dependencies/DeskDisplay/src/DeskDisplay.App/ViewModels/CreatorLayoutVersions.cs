using DeskDisplay.Core.Models;

namespace DeskDisplay.App.ViewModels;

public sealed record EditorLayoutChoice(Guid? Id, string Name, ThemeOrientation Orientation);

public sealed partial class CreatorViewModel
{
    public IReadOnlyList<EditorLayoutChoice> LayoutChoices => [new(null, "默认横屏", ThemeOrientation.Landscape), new(null, "默认竖屏", ThemeOrientation.Portrait), .. Project.LayoutVersions.Select(v => new EditorLayoutChoice(v.Id, v.Name, v.Layout.Width >= v.Layout.Height ? ThemeOrientation.Landscape : ThemeOrientation.Portrait))];
    public EditorLayoutChoice? SelectedLayoutChoice
    {
        get => LayoutChoices.FirstOrDefault(v => v.Id == Project.SelectedLayoutId && v.Orientation == Orientation);
        set
        {
            if (value is null || !CanEdit || SelectedLayoutChoice == value) return;
            LeavingEditorLayout?.Invoke(this, EventArgs.Empty);
            Change(() => { Project.SelectedLayoutId = value.Id; Project.Orientation = value.Orientation; });
            SelectLayers([]); RefreshLayers(); Track(RestoreLayoutViewportAsync());
        }
    }
    public string LayoutName
    {
        get => Project.LayoutVersions.FirstOrDefault(v => v.Id == Project.SelectedLayoutId)?.Name ?? "默认布局";
        set
        {
            if (!CanEdit || string.IsNullOrWhiteSpace(value) || value.Length > 80) return;
            var index = Project.LayoutVersions.FindIndex(v => v.Id == Project.SelectedLayoutId);
            if (index < 0 || Project.LayoutVersions[index].Name == value) return;
            Change(() => Project.LayoutVersions[index] = Project.LayoutVersions[index] with { Name = value.Trim(), Revision = Project.LayoutVersions[index].Revision + 1 });
        }
    }
    public string LayoutDimensions => $"{Project.ActiveLayout.Width:0.##} × {Project.ActiveLayout.Height:0.##}";
    private void AdvanceLayoutRevision()
    {
        var index = Project.LayoutVersions.FindIndex(v => v.Id == Project.SelectedLayoutId);
        if (index >= 0) Project.LayoutVersions[index] = Project.LayoutVersions[index] with { Revision = Project.LayoutVersions[index].Revision + 1 };
    }
    public void AddEditableLayout(NamedThemeLayout version)
    {
        if (!CanEdit || Project.LayoutVersions.Count >= 62) { Status = "最多保存62个自定义布局。"; return; }
        var candidate = Project.Clone(); candidate.Version = 2; candidate.LayoutVersions.Add(version); candidate.SelectedLayoutId = version.Id; candidate.Validate();
        LeavingEditorLayout?.Invoke(this, EventArgs.Empty);
        Change(() => { Project.Version = 2; Project.LayoutVersions.Add(version with { Layout = version.Layout.Clone() }); Project.SelectedLayoutId = version.Id; Project.Orientation = version.Layout.Width >= version.Layout.Height ? ThemeOrientation.Landscape : ThemeOrientation.Portrait; });
        _preview = false; SelectLayers([]); RefreshLayers(); NotifyPage(); Track(RestoreLayoutViewportAsync());
    }
    public ActionCommand CopyLayoutCommand => _copyLayoutCommand ??= new(_ => AddEditableLayout(new(Guid.NewGuid(), new string(LayoutName.Take(76).ToArray()) + " 副本", 1, Project.ActiveLayout.Clone(), Project.SelectedLayoutId)), _ => CanEdit && Project.LayoutVersions.Count < 62);
    public ActionCommand DeleteLayoutCommand => _deleteLayoutCommand ??= new(_ =>
    {
        if (Project.SelectedLayoutId is not { } id) return;
        Change(() => { Project.LayoutVersions.RemoveAll(v => v.Id == id); Project.SelectedLayoutId = null; });
        SelectLayers([]); Track(RestoreLayoutViewportAsync());
    }, _ => CanEdit && Project.SelectedLayoutId is not null);
    private ActionCommand? _copyLayoutCommand, _deleteLayoutCommand;
    private void NotifyLayoutVersions()
    {
        foreach (var name in new[] { nameof(LayoutChoices), nameof(SelectedLayoutChoice), nameof(LayoutName), nameof(LayoutDimensions) }) OnPropertyChanged(name);
        _copyLayoutCommand?.Notify(); _deleteLayoutCommand?.Notify();
    }
}
