using System.Windows.Input;
using Serilog;

namespace DeskDisplay.App.ViewModels;

public sealed class ActionCommand(Action<object?> action, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try { action(parameter); }
        catch (Exception exception) { Log.Error(exception, "UI action failed"); }
    }
    public event EventHandler? CanExecuteChanged;
    public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
