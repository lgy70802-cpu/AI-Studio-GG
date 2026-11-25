using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Display.DataProviders;

public sealed partial class StockSceneDataProvider : IDisposable
{
    private readonly StockScene _scene;
    private readonly ILogger _logger;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public StockSceneDataProvider(StockScene scene, ILogger logger)
    {
        _scene = scene;
        _logger = logger;
    }

    public async Task RefreshAsync(string symbols, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        var codes = Normalize(symbols);
        if (codes.Count == 0) { _scene.SetStatus("请输入 1–20 个有效的 A 股代码"); return; }
        if (!await _refreshLock.WaitAsync(0, token)) return;
        try
        {
            _scene.SetStatus("正在连接行情…");
            var bytes = await _client.GetByteArrayAsync("https://qt.gtimg.cn/q=" + string.Join(',', codes), token);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var text = Encoding.GetEncoding(936).GetString(bytes);
            var quotes = Parse(text);
            _scene.Update(quotes, DateTimeOffset.Now);
            if (quotes.Count < codes.Count) _scene.SetStatus($"已更新 {quotes.Count}/{codes.Count} 只股票；部分代码无行情");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Stock plugin quote refresh failed");
            _scene.SetStatus(_scene.Quotes.Count == 0 ? "连接异常，尚无可显示行情" : "连接异常，保留上次行情");
        }
        finally { _refreshLock.Release(); }
    }

    public static IReadOnlyList<string> Normalize(string input) => input
        .Split([',', '，', ';', '；', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => value.ToLowerInvariant())
        .Select(value => SixDigits().IsMatch(value) ? (value[0] == '6' ? "sh" : value[0] is '0' or '3' ? "sz" : "") + value : value)
        .Where(value => FullCode().IsMatch(value)).Distinct().Take(20).ToArray();

    internal static IReadOnlyList<StockQuote> Parse(string text)
    {
        var quotes = new List<StockQuote>();
        foreach (Match match in QuoteLine().Matches(text))
        {
            var fields = match.Groups[2].Value.Split('~');
            if (fields.Length < 35 || string.IsNullOrWhiteSpace(fields[1])) continue;
            if (!double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var price) || price <= 0) continue;
            if (!double.TryParse(fields[32], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)) continue;
            double? previous = double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : null;
            quotes.Add(new(match.Groups[1].Value, fields[1], price, percent, previous));
        }
        return quotes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _client.Dispose();
        _lifetime.Dispose();
    }

    [GeneratedRegex("^\\d{6}$", RegexOptions.CultureInvariant)] private static partial Regex SixDigits();
    [GeneratedRegex("^(?:sh|sz|bj)\\d{6}$", RegexOptions.CultureInvariant)] private static partial Regex FullCode();
    [GeneratedRegex("v_((?:sh|sz|bj)\\d{6})=\"([^\"]*)\"", RegexOptions.CultureInvariant)] private static partial Regex QuoteLine();
}
