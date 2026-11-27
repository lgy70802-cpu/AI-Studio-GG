using System.Windows.Input;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute, ILogger logger) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute();

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try { await execute(); }
        catch (Exception exception) { logger.Error(exception, "Asynchronous UI command failed"); }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
