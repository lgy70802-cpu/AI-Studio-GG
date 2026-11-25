using Microsoft.Win32;
using System.IO;

namespace MiraBox.App;
public sealed partial class StudioWindow
{
    bool systemSuspended;
    readonly Dictionary<string, (int Attempts, DateTimeOffset Next)> retryOutput = new();
    async void OnSystemPower(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is not (PowerModes.Suspend or PowerModes.Resume)) return;
        await Dispatcher.InvokeAsync(async () =>
        {
            await commands.WaitAsync();
            try { if (!closing) await HandleSystemPower(e.Mode == PowerModes.Suspend); }
            catch (Exception ex) { Log(new { at = DateTimeOffset.Now, error = ex.Message, subsystem = "power-event" }); }
            finally { commands.Release(); }
        }).Task.Unwrap();
    }
    async Task HandleSystemPower(bool suspend)
    {
        systemSuspended = suspend;
        if (suspend)
        {
            foreach (var serial in saved.Keys.ToArray()) { InvalidatePlaylistWork(serial); await Stop(serial); }
            Save(); SaveSchedules();
        }
        else { retryOutput.Clear(); connectedSerials.Clear(); }
        Log(new { at = DateTimeOffset.Now, action = suspend ? "system-suspend" : "system-resume" });
    }
    void FailedRestore(string serial)
    {
        var attempts = retryOutput.GetValueOrDefault(serial).Attempts + 1;
        retryOutput[serial] = (attempts, DateTimeOffset.Now.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempts, 5)))));
    }
}
