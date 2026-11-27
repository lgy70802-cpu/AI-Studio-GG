using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;

namespace DeskDisplay.App.Views;

public sealed class ThemeRepairChoice(ThemeRepairMatch match) : ObservableObject
{
    public ThemeRepairMatch Match { get; } = match;
    private bool _selected = match.CanRepair;
    public bool Selected { get => _selected; set => SetProperty(ref _selected, Match.CanRepair && value); }
}
public partial class CreatorRepairPreview : Window
{
    private readonly ThemeRepairChoice[] _choices;
    private int _generation;
    private bool _closed;
    public IReadOnlyList<ThemeRepairMatch> SelectedMatches => _choices.Where(c => c.Selected).Select(c => c.Match).ToArray();
    public CreatorRepairPreview(ThemeRepairPlan plan)
    {
        InitializeComponent(); _choices = plan.Items.Select(i => new ThemeRepairChoice(i)).ToArray(); DataContext = new { Items = _choices };
        foreach (var choice in _choices) choice.PropertyChanged += ChoiceChanged;
        UpdateSummary();
        Closed += (_, _) => { _closed = true; _generation++; CandidateImage.Source = null; foreach (var choice in _choices) choice.PropertyChanged -= ChoiceChanged; };
    }
    private void ChoiceChanged(object? sender, PropertyChangedEventArgs e) => UpdateSummary();
    private void UpdateSummary() { var selected = SelectedMatches.Count; Summary.Text = $"已选 {selected} 项，跳过 {_choices.Length - selected} 项。确认后统一修改引用，可一次撤销。"; ApplyButton.IsEnabled = selected > 0; }
    private async void PreviewCandidate(object sender, SelectionChangedEventArgs e)
    {
        var generation = ++_generation; CandidateImage.Source = null;
        if ((sender as DataGrid)?.SelectedItem is not ThemeRepairChoice choice) return;
        CandidateDetails.Text = choice.Match.CandidatePath + "\n" + choice.Match.Result;
        if (!choice.Match.CanRepair || System.IO.Path.GetExtension(choice.Match.CandidatePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase)) return;
        try { var image = await new ImageSceneLoader().LoadAsync(choice.Match.CandidatePath, ImageDisplayMode.Uniform); if (!_closed && generation == _generation) CandidateImage.Source = ((WpfSceneImage)image.Image).Bitmap; }
        catch (Exception exception) { if (!_closed && generation == _generation) CandidateDetails.Text = "无法预览：" + exception.Message; }
    }
    private void Apply(object sender, RoutedEventArgs e) => DialogResult = true;
}
