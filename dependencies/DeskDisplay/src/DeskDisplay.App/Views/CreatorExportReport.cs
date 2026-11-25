using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DeskDisplay.App.Views;

public sealed class CreatorExportReport : Window
{
    public CreatorExportReport(string report, bool forExport = false)
    {
        Title = "导出检查报告"; Width = 960; Height = 620; MinWidth = 600; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 247, 251));
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DeskDisplay.App;component/Themes/Colors.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DeskDisplay.App;component/Themes/Controls.xaml", UriKind.Relative) });
        var panel = new DockPanel { Margin = new Thickness(20) }; Content = panel;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 14, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
        var save = new Button { Content = "保存检查报告", Style = (Style)FindResource("PrimaryButton"), Margin = new(0, 0, 8, 0) };
        save.Click += async (_, _) => { var dialog = new SaveFileDialog { Filter = "Markdown报告|*.md", DefaultExt = ".md", FileName = "主题导出检查", AddExtension = true }; if (dialog.ShowDialog(this) != true) return; try { if (!Path.GetExtension(dialog.FileName).Equals(".md", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请将检查报告保存为.md文件。"); await File.WriteAllTextAsync(dialog.FileName, report); } catch (Exception e) { MessageBox.Show(this, e.Message, "报告未保存"); } }; actions.Children.Add(save);
        if (forExport) { var proceed = new Button { Content = "继续导出", Style = (Style)FindResource("PrimaryButton"), Margin = new(0, 0, 8, 0) }; proceed.Click += (_, _) => DialogResult = true; actions.Children.Add(proceed); }
        var close = new Button { Content = "关闭", Style = (Style)FindResource("SoftButton") }; close.Click += (_, _) => Close(); actions.Children.Add(close);
        var document = new FlowDocument { PagePadding = new(16), FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(32, 44, 67)) };
        foreach (var line in report.Split('\n'))
        {
            if (line.StartsWith("|---") || line.StartsWith("| 布局")) continue;
            if (line.StartsWith('|'))
            {
                var fields = Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(f => f.Trim().Replace(@"\|", "|")).ToArray();
                if (fields.Length != 4) continue;
                document.Blocks.Add(new Paragraph(new Bold(new Run(fields[0] + " · " + fields[1]))) { Margin = new(0, 16, 0, 4) });
                document.Blocks.Add(new Paragraph(new Run(fields[2])) { Margin = new(0, 0, 0, 4) });
                if (fields[3].Length > 0) document.Blocks.Add(new Paragraph(new Run(fields[3])) { Foreground = Brushes.SlateGray, FontSize = 11, Margin = new(0, 0, 0, 4) });
            }
            else if (line.Length > 0) document.Blocks.Add(new Paragraph(new Run(line.TrimStart('#', ' '))) { FontSize = line.StartsWith('#') ? 22 : 13, FontWeight = line.StartsWith('#') ? FontWeights.Bold : FontWeights.Normal, Margin = new(0, 0, 0, 10) });
        }
        panel.Children.Add(new FlowDocumentScrollViewer { Document = document, IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White });
    }
}
