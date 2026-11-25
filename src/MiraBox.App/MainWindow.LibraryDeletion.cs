using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    async Task ReconcileHardwareLibraryDeletion(string[] ids)
    {
        foreach (var serial in HardwareSessionSerials())
            await InHardwareSession(serial, () => ReconcileHardwareLibraryDeletionForCurrent(ids));
    }

    async Task ReconcileHardwareLibraryDeletionForCurrent(string[] ids)
    {
        var affectedSerial = hardwareApplySerial;
        bool Removed(HardwareOutputSnapshot? snapshot) => snapshot != null &&
            (snapshot.BaseResourceId != null && ids.Contains(snapshot.BaseResourceId) ||
             snapshot.VideoResourceId != null && ids.Contains(snapshot.VideoResourceId));
        try
        {
            if (Removed(stoppedHardwareOutput))
            {
                stoppedHardwareOutput = null;
                stoppedHardwareStill = null;
            }
            if (Removed(pendingHardwareRestore)) pendingHardwareRestore = null;
            var affected = hardwareStill?.ResourceId is { } still && ids.Contains(still) ||
                hardwareVideoResourceId is { } video && ids.Contains(video);
            if (!affected)
            {
                if (hardwarePreviewResourceId is { } preview && ids.Contains(preview))
                    RestoreHardwareStill();
                SaveWorkspace();
                return;
            }
            var serial = hardwareApplySerial;
            var output = hardwareOutput;
            var fallback = state.Recent.Select(use => state.Resources.GetValueOrDefault(use.ResourceId))
                .OfType<Resource>().FirstOrDefault(resource => !ids.Contains(resource.Id) &&
                    IsHardwareStill(resource) && HardwarePlaylistIssue(resource) == null);
            PauseHardwarePlaylist();
            if (fallback != null && output is { IsStopping: false })
            {
                UseHardwareStill(fallback);
                Notice.Text = $"已移入回收站，D92 正在切回「{fallback.Name}」。";
            }
            else
            {
                // Clear restore intent before waiting for USB shutdown so a later replug cannot replay deleted content.
                pendingHardwareRestore = null;
                stoppedHardwareOutput = null;
                stoppedHardwareStill = null;
                await StopHardwareTransport();
                if (hardwareApplySerial == serial) Notice.Text = "已移入回收站，D92 无可回退内容，已停止输出。";
            }
            SaveWorkspace();
            Render();
        }
        catch (Exception ex)
        {
            if (affectedSerial != null)
                InHardwareSession(affectedSerial, () =>
                {
                    hardwareOutput?.Stop();
                    stoppedHardwareOutput = null;
                    pendingHardwareRestore = null;
                });
            Notice.Text = "内容已移入回收站，D92 输出已停止，请重新选择内容。" + ex.Message;
        }
    }
}
