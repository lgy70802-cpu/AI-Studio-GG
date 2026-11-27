using System.Diagnostics;

namespace DeskDisplay.App.ViewModels;

/// <summary>Coordinates transitions, not frame transmission. Drivers retain their own lifetimes and locks.</summary>
public sealed class OutputCoordinator : ObservableObject
{
    private bool _busy;
    private string _message="尚未开始输出";
    private bool _isError;
    private readonly AsyncLocal<Guid?> _owner=new();
    private Guid? _activeOwner;
    private Func<Task>? _retry;
    private AsyncCommand? _retryCommand;
    public bool HasRetry=>_retry is not null;
    public bool OwnsTransition=>IsBusy && _owner.Value==_activeOwner;
    public AsyncCommand RetryCommand=>_retryCommand??=new(RetryAsync,()=>HasRetry&&!IsBusy,Serilog.Log.Logger);
    public void SetRetry(Func<Task>? retry){_retry=retry;OnPropertyChanged(nameof(HasRetry));_retryCommand?.NotifyCanExecuteChanged();}
    public async Task RetryAsync()
    {
        if(IsBusy || _retry is not { } action)return;
        try{await action();}
        catch(Exception ex){Report("重试失败："+ex.Message,true);SetRetry(action);}
    }
    public bool IsError {get=>_isError;private set=>SetProperty(ref _isError,value);}
    public OutputRecoveryAdvice RecoveryAdvice => OutputTroubleshooting.Explain(Message);
    public string Details => IsError ? Message+Environment.NewLine+RecoveryAdvice.Hint : Message;
    public bool IsBusy { get=>_busy; private set=>SetProperty(ref _busy,value); }
    public string Message { get=>_message; private set=>SetProperty(ref _message,value); }
    public long CompletedTransitions { get; private set; }
    public double LastTransitionMilliseconds { get; private set; }
    public void Report(string message,bool isError=false){Message=message;IsError=isError;OnPropertyChanged(nameof(RecoveryAdvice));OnPropertyChanged(nameof(Details));}
    public async Task RunAsync(string operation,Func<Task> action)
    {
        if(IsBusy && _owner.Value is { } owner && owner==_activeOwner){await action();return;}
        if(IsBusy)throw new InvalidOperationException("正在切换内容，请等待当前操作完成。");
        SetRetry(null);
        var previous=_owner.Value;_activeOwner=Guid.NewGuid();_owner.Value=_activeOwner;
        IsBusy=true;Report(operation+"…");var watch=Stopwatch.StartNew();
        try { await action();CompletedTransitions++; }
        catch(Exception ex) {Report(operation+"失败："+ex.Message,true);throw;}
        finally {LastTransitionMilliseconds=watch.Elapsed.TotalMilliseconds;_owner.Value=previous;_activeOwner=null;IsBusy=false;_retryCommand?.NotifyCanExecuteChanged();OnPropertyChanged(nameof(LastTransitionMilliseconds));OnPropertyChanged(nameof(CompletedTransitions));}
    }
    public async Task<T> RunAsync<T>(string operation,Func<Task<T>> action)
    {T result=default!;await RunAsync(operation,async()=>{result=await action();});return result;}
}
