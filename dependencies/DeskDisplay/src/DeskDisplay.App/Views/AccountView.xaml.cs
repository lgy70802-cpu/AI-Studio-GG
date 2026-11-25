using System.Windows;
using System.Windows.Controls;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;
public partial class AccountView : UserControl
{
    public AccountView() => InitializeComponent();
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Account: not null } main && sender is PasswordBox box) main.Account.Password = box.Password;
    }
}
