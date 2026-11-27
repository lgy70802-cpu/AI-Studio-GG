using System.Windows.Controls;
using System.Windows;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class DeviceManagementView : UserControl
{
    public DeviceManagementView() => InitializeComponent();
    private void ShowDeviceList(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.SelectedNavigation = "设备"; }
    private void ShowUsbOutput(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel main) main.SelectedNavigation = "USB屏"; }
}
