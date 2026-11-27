using System.ComponentModel;
using System.Windows;
using DeskDisplay.App.ViewModels;

namespace DeskDisplay.App.Views;

public partial class CreatorDevicePreview : Window
{
    private bool _closing, _released, _closed;

    public CreatorDevicePreview(CreatorDevicePreviewViewModel model)
    {
        InitializeComponent(); DataContext = model; PreviewSurface.RenderFailed += model.ReportRenderFailure; Closing += ReleaseOnClose;
    }
    private void ClosePreview(object sender, RoutedEventArgs e) => Close();
    private void EditAdaptation(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreatorDevicePreviewViewModel { CanAdapt: true } model) return;
        try { model.EditAdaptedLayout?.Invoke(model.CreateEditableLayout()); Close(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "无法创建布局"); }
    }
    private void FillBackgroundAdaptation(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreatorDevicePreviewViewModel { CanAdapt: true } model) return;
        try { model.EditAdaptedLayout?.Invoke(model.CreateEditableLayout(fillBackground: true)); Close(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法生成铺满布局"); }
    }
    private async void ReleaseOnClose(object? sender, CancelEventArgs e)
    {
        if (_released) return; e.Cancel = true; if (_closing) return;
        _closing = true; IsEnabled = false;
        try { if (DataContext is CreatorDevicePreviewViewModel model) { PreviewSurface.RenderFailed -= model.ReportRenderFailure; await model.DisposeAsync(); } }
        catch (Exception ex) { Serilog.Log.Error(ex, "Device preview cleanup failed"); }
        finally
        {
            _released = true;
            // DisposeAsync may complete synchronously (including a failed/missing-asset preview).
            // Never call Close again on the original Closing event's stack.
            if (!_closed && !Dispatcher.HasShutdownStarted)
                _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    if (!_closed && !Dispatcher.HasShutdownStarted) Close();
                }));
        }
    }
    protected override void OnClosed(EventArgs e) { _closed = true; base.OnClosed(e); }
}



