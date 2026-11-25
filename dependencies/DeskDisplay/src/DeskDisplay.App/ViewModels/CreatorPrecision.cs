using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;

namespace DeskDisplay.App.ViewModels;

public sealed partial class CreatorViewModel
{
    public IReadOnlyList<CreatorVerticalChoice> TextVerticalChoices { get; } = [new(ThemeVerticalAlignment.Top, "顶部"), new(ThemeVerticalAlignment.Center, "居中"), new(ThemeVerticalAlignment.Bottom, "底部")];
    public ThemeVerticalAlignment TextVerticalAlignment { get => SelectedLayer?.VerticalAlignment ?? ThemeVerticalAlignment.Center; set { if (Enum.IsDefined(value)) Edit(l => l.VerticalAlignment = value); } }
    public bool AutoGrowTextHeight { get => SelectedLayer?.AutoGrowTextHeight ?? false; set => Edit(l => l.AutoGrowTextHeight = value); }
    public string TextOverflowHint => SelectedLayer is { Kind: ThemeElementKind.Text } text ? Overflow(text) : "";
    private static string Overflow(ThemeLayer layer)
    {
        var size = ThemeTextMetrics.Measure(layer);
        return size.Width > layer.Width + .5 && size.Height > layer.Height + .5 ? "文字宽高超出边框，部分内容会被裁切。" : size.Width > layer.Width + .5 ? "文字超出宽度，请换行或加宽。" : size.Height > layer.Height + .5 ? "文字超出高度，可自动增高或调整字号。" : "";
    }
    public ActionCommand FitTextHeightCommand => _fitTextHeight ??= new(_ => { if (SelectedLayer is { Kind: ThemeElementKind.Text } text) Edit(l => l.Height = Math.Clamp(Math.Ceiling(ThemeTextMetrics.Measure(text).Height), 1, 16384)); }, _ => CanEditSelection && IsLiteralText);
    public ActionCommand ResetImageCropCommand => _resetImageCrop ??= new(_ => { if (SelectedLayer is { } layer) Change(() => { layer.ImageZoom = 1; layer.CropX = layer.CropY = .5; layer.Fill = ImageDisplayMode.Uniform; }); }, _ => CanEditSelection && IsImage && SelectedLayer?.IsVideo == false);
    public ActionCommand DuplicateSelectionCommand => _duplicateSelection ??= new(_ => { CopyCommand.Execute(null); PasteCommand.Execute(null); }, _ => CanEdit && HasSelection);
    public ActionCommand ToggleSelectionLockCommand => _toggleSelectionLock ??= new(_ => ApplyBatch(SelectedLayers.All(l => l.Locked) ? "unlock" : "lock"), _ => CanEdit && HasSelection);
    public ActionCommand ToggleSelectionVisibilityCommand => _toggleSelectionVisibility ??= new(_ => { var visible = !SelectedLayers.All(l => l.Visible); Change(() => { foreach (var layer in SelectedLayers) layer.Visible = visible; }); }, _ => CanEdit && HasSelection);
    public ActionCommand SelectGroupMemberCommand => _selectGroupMember ??= new(_ => SelectNextGroupMember(), _ => CanEdit && SelectedLayer?.GroupId is not null);
    private ActionCommand? _fitTextHeight, _resetImageCrop, _duplicateSelection, _toggleSelectionLock, _toggleSelectionVisibility, _selectGroupMember;
    public void SelectNextGroupMember()
    {
        if (SelectedLayer?.GroupId is not Guid group) return;
        var members = GroupMembers(group); var next = members[(Array.FindIndex(members.ToArray(), l => l.Id == SelectedLayer.Id) + 1) % members.Count];
        _collapsedGroups.Remove(group); _selection.Clear(); _selection.Add(next.Id); _selectedId = next.Id; RefreshLayers(); NotifySelection(); FocusCanvasSelection?.Invoke(this, EventArgs.Empty);
        Status = "正在编辑组内图层：" + next.Name + "；组合关系保持不变。";
    }
    private void NotifyPrecision()
    {
        _fitClockContent?.Notify(); _addClockPreset?.Notify(); _saveClockStyle?.Notify(); _addSavedClockStyle?.Notify(); _deleteClockStyle?.Notify(); OnPropertyChanged(nameof(ClockAppearance)); OnPropertyChanged(nameof(ClockWeight)); OnPropertyChanged(nameof(ClockStretch)); OnPropertyChanged(nameof(SupportsClockFont)); OnPropertyChanged(nameof(ClockFaceHint));
        foreach (var command in new[] { _fitTextHeight, _resetImageCrop, _duplicateSelection, _toggleSelectionLock, _toggleSelectionVisibility, _selectGroupMember }) command?.Notify();
    }
}
public sealed record CreatorVerticalChoice(ThemeVerticalAlignment Value, string Label);



