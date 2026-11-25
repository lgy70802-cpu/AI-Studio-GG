namespace MiraBox.App;

public partial class MainWindow
{
    static string MaterialDisplayName(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value,
            @"\.(?:jpe?g|png|gif|bmp|webp|tiff?|mp4|m4v|mov|webm|mkv)(?=$|[」\] ·])", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
