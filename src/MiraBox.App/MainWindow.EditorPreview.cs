using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed record EditorPreviewReturn(HardwareOutputSnapshot? Snapshot, bool Running,
        string? VideoId, bool VideoPaused, HardwareStill? StoppedStill);
    readonly Dictionary<string, EditorPreviewReturn> editorPreviewReturns = new(StringComparer.Ordinal);
    readonly HashSet<string> editorRestoreVideoPaused = new(StringComparer.Ordinal);

    EditorPreviewReturn CaptureEditorPreviewReturn() => new(
        CaptureHardwareOutput() ?? stoppedHardwareOutput, hardwareOutput != null,
        hardwareVideoResourceId, HardwareVideoPaused, stoppedHardwareStill);

    bool RestoreEditorPreview()
    {
        if (hardwareApplySerial is not { } serial || !editorPreviewReturns.Remove(serial, out var previous)) return false;
        hardwarePreviewEnds = null;
        if (previous.Running)
        {
            RestoreHardwareStill();
            if (previous.VideoId is { } id && state.Resources.TryGetValue(id, out var video))
            {
                PlayHardwareVideo(video, fromPlaylist: true, recordUse: false);
                if (previous.VideoPaused) editorRestoreVideoPaused.Add(serial);
            }
        }
        else _ = RestoreStopped();
        return true;

        async Task RestoreStopped()
        {
            var output = hardwareOutput;
            await StopHardwareTransport();
            InHardwareSession(serial, () =>
            {
                if (hardwareOutput != null && !ReferenceEquals(hardwareOutput, output)) return;
                stoppedHardwareOutput = previous.Snapshot;
                stoppedHardwareStill = previous.StoppedStill;
                hardwareContent = previous.Snapshot is { } snapshot ? ResourceName(snapshot.VideoResourceId ?? snapshot.BaseResourceId) : "尚未应用内容";
                SaveWorkspace();
                RenderHardwareStatus();
            });
        }
    }
}
