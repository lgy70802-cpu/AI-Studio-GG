using System.Collections.Concurrent;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Usb;

namespace D92.ProtocolProbe;

// Bounded diagnostic worker; uses the application's real Scene/SceneView/JPEG renderer.
internal sealed class DiagnosticClockFrames : IAsyncDisposable
{
    private readonly BlockingCollection<(DateTimeOffset Time, TaskCompletionSource<byte[]> Result)> _requests = new(1);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;

    public DiagnosticClockFrames()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "D69 diagnostic scene renderer" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            using var renderer = new SceneJpegRenderer();
            var scene = new ClockScene();
            scene.SetCanvasSize(1280, 480);
            _ready.SetResult();
            foreach (var request in _requests.GetConsumingEnumerable())
            {
                try
                {
                    scene.UpdateTime(request.Time);
                    request.Result.SetResult(renderer.Render(scene, 90, 85));
                }
                catch (Exception error) { request.Result.SetException(error); }
            }
        }
        catch (Exception error) { _ready.TrySetException(error); }
        finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
    }

    public async Task<byte[]> RenderAsync(DateTimeOffset time)
    {
        await _ready.Task;
        var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Add((time, completion));
        return await completion.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _requests.CompleteAdding();
        await Task.Run(() => _thread.Join());
        _requests.Dispose();
    }
}
