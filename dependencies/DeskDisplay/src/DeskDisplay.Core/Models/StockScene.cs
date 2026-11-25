namespace DeskDisplay.Core.Models;

public sealed record StockQuote(string Symbol, string Name, double Price, double Percent, double? Previous = null);

/// <summary>Native stock scene shared by preview, display window and USB JPEG output.</summary>
public sealed class StockScene : Scene
{
    private IReadOnlyList<StockQuote> _quotes = [];
    private string _status = "等待刷新行情";
    private DateTimeOffset? _updatedAt;

    public IReadOnlyList<StockQuote> Quotes { get => _quotes; private set { _quotes = value; OnPropertyChanged(); } }
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public DateTimeOffset? UpdatedAt { get => _updatedAt; private set { _updatedAt = value; OnPropertyChanged(); } }

    public StockScene() => SetCanvasSize(1920, 462);

    public void Update(IReadOnlyList<StockQuote> quotes, DateTimeOffset updatedAt)
    {
        Quotes = quotes;
        UpdatedAt = updatedAt;
        Status = quotes.Count == 0 ? "未收到有效行情" : $"已更新 {quotes.Count} 只股票";
    }

    public void SetStatus(string status) => Status = status;
    public void InvalidateClock() => OnPropertyChanged(nameof(UpdatedAt));
}
