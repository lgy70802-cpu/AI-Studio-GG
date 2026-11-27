using DeskDisplay.Library;
using Serilog;
using System.ComponentModel;

namespace DeskDisplay.App.ViewModels;

public sealed class StudioAccountViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly StudioApiClient _client;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private string _email = "", _password = "", _status = "登录后可同步收藏、购买记录和 VIP 权益。";
    private StudioUserProfile? _profile;
    private bool _busy, _disposed;
    public string Email { get => _email; set { if (SetProperty(ref _email, value)) Notify(); } }
    public string Password { get => _password; set { if (SetProperty(ref _password, value)) Notify(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public StudioUserProfile? Profile { get => _profile; private set { if (SetProperty(ref _profile, value)) Notify(); } }
    public bool IsLoggedIn => Profile is not null;
    public bool IsLoggedOut => !IsLoggedIn;
    public string Membership => Profile?.IsVip == true ? Profile.VipExpiresAt is null ? "VIP · 长期有效" : $"VIP · 有效期至 {Profile.VipExpiresAt:yyyy-MM-dd}" : IsLoggedIn ? "普通用户" : "未登录";
    public AsyncCommand LoginCommand { get; }
    public AsyncCommand RegisterCommand { get; }
    public AsyncCommand LogoutCommand { get; }
    public StudioAccountViewModel(MainViewModel main, string serverUrl, ILogger logger, StudioApiClient? client = null)
    {
        _main = main; _logger = logger; _client = client ?? new StudioApiClient(logger); _initialServerUrl = serverUrl;
        LoginCommand = new(() => AuthenticateAsync(false), CanAuthenticate, logger);
        RegisterCommand = new(() => AuthenticateAsync(true), CanAuthenticate, logger);
        LogoutCommand = new(LogoutAsync, () => !_busy && IsLoggedIn, logger);
        if (_main.Cloud is not null) _main.Cloud.PropertyChanged += OnCloudChanged;
    }
    public string ServerUrl => _main.Cloud?.ServerUrl ?? _initialServerUrl;
    private readonly string _initialServerUrl;
    private bool CanAuthenticate() => !_busy && !string.IsNullOrWhiteSpace(Email) && Password.Length >= 8;
    public async Task InitializeAsync()
    {
        var server = ServerUrl;
        try { var profile = await _client.GetProfileAsync(server, _lifetime.Token); if (server != ServerUrl || _disposed) return; Profile = profile; if (Profile is not null) Status = "云端账号已连接。"; }
        catch (Exception exception) { _logger.Warning(exception, "Cannot restore Studio user session"); Status = "暂时无法验证云端账号。"; }
    }
    private async Task AuthenticateAsync(bool register)
    {
        _busy = true; Notify();
        try
        {
            var server = ServerUrl;
            var session = await _client.LoginAsync(server, Email.Trim(), Password, register, _lifetime.Token);
            if (server != ServerUrl || _disposed) return;
            Password = ""; Profile = session.Profile; Status = register ? "账号已创建并登录。" : "登录成功。";
            if (_main.Cloud is not null) await _main.Cloud.RefreshAfterAccountChangeAsync();
        }
        catch (Exception exception) { _logger.Warning(exception, "Studio user authentication failed"); Status = register ? "注册失败，请检查邮箱是否已使用。" : "登录失败，请检查邮箱和密码。"; }
        finally { _busy = false; Notify(); }
    }
    private async Task LogoutAsync()
    {
        _busy = true; Notify();
        try { await _client.LogoutAsync(ServerUrl, _lifetime.Token); }
        catch (Exception exception) { _logger.Warning(exception, "Cannot revoke Studio remote session"); }
        finally { Profile = null; Password = ""; Status = "已退出云端账号。"; if (_main.Cloud is not null) await _main.Cloud.RefreshAfterAccountChangeAsync(); _busy = false; Notify(); }
    }
    private void Notify()
    {
        OnPropertyChanged(nameof(IsLoggedIn)); OnPropertyChanged(nameof(IsLoggedOut)); OnPropertyChanged(nameof(Membership));
        LoginCommand.NotifyCanExecuteChanged(); RegisterCommand.NotifyCanExecuteChanged(); LogoutCommand.NotifyCanExecuteChanged();
    }
    private void OnCloudChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CloudLibraryViewModel.ServerUrl)) { Profile = null; Password = ""; Status = "服务器地址已更改，请重新登录或验证账号。"; }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; if (_main.Cloud is not null) _main.Cloud.PropertyChanged -= OnCloudChanged; _lifetime.Cancel(); _lifetime.Dispose(); _client.Dispose(); }
}
