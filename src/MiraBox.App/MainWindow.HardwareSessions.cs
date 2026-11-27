using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    sealed class HardwareDeviceSession
    {
        public D92StaticOutput? hardwareOutput = null;
        public Task hardwareOutputCompletion = Task.CompletedTask;
        public string hardwareContent = "";
        public string hardwareStatus = "";
        public string? lastHardwareContentSerial = null;
        public string? hardwareLastWritten = null;
        public Guid? manualHardwareSubmission = null;
        public bool manualHardwareWasOffline = false;
        public HardwareStill? hardwareStill = null;
        public HardwareOutputSnapshot? stoppedHardwareOutput = null;
        public HardwareStill? stoppedHardwareStill = null;
        public string? hardwareVideoResourceId = null;
        public TimeSpan? hardwarePreviewEnds = null;
        public string? hardwarePreviewResourceId = null;
        public string? hardwarePlaylistId = null;
        public string? hardwarePlaylistItem = null;
        public TimeSpan hardwarePlaylistRemaining = TimeSpan.Zero;
        public int hardwarePlaylistIndex = 0;
        public bool hardwarePlaylistPaused = false;
        public HardwareOutputSnapshot? pendingHardwareRestore = null;
        public bool restoringStartup = false;
        public TimeSpan nextHardwareRestoreCheck = TimeSpan.Zero;
        public object? pendingHardwareScheduleStop = null;
        public HashSet<string> FailedItems = [];
    }

    readonly Dictionary<string, HardwareDeviceSession> hardwareSessions = new(StringComparer.Ordinal);
    bool backgroundHardwareContext;
    string? visibleHardwareSerial;

    HardwareDeviceSession CaptureDeviceSession() => new()
    {
        hardwareOutput = this.hardwareOutput,
        hardwareOutputCompletion = this.hardwareOutputCompletion,
        hardwareContent = this.hardwareContent,
        hardwareStatus = this.hardwareStatus,
        lastHardwareContentSerial = this.lastHardwareContentSerial,
        hardwareLastWritten = this.hardwareLastWritten,
        manualHardwareSubmission = this.manualHardwareSubmission,
        manualHardwareWasOffline = this.manualHardwareWasOffline,
        hardwareStill = this.hardwareStill,
        stoppedHardwareOutput = this.stoppedHardwareOutput,
        stoppedHardwareStill = this.stoppedHardwareStill,
        hardwareVideoResourceId = this.hardwareVideoResourceId,
        hardwarePreviewEnds = this.hardwarePreviewEnds,
        hardwarePreviewResourceId = this.hardwarePreviewResourceId,
        hardwarePlaylistId = this.hardwarePlaylistId,
        hardwarePlaylistItem = this.hardwarePlaylistItem,
        hardwarePlaylistRemaining = this.hardwarePlaylistRemaining,
        hardwarePlaylistIndex = this.hardwarePlaylistIndex,
        hardwarePlaylistPaused = this.hardwarePlaylistPaused,
        pendingHardwareRestore = this.pendingHardwareRestore,
        restoringStartup = this.restoringStartup,
        nextHardwareRestoreCheck = this.nextHardwareRestoreCheck,
        pendingHardwareScheduleStop = this.pendingHardwareScheduleStop,
        FailedItems = new(hardwareFailedPlaylistItems)
    };

    void LoadDeviceSession(string? serial)
    {
        hardwareApplySerial = serial;
        hardwareApplyRotation = serial == null ? 90 : hardwareOrientations.Get(serial);
        var session = serial != null && hardwareSessions.TryGetValue(serial, out var saved) ? saved : new HardwareDeviceSession();
        hardwareOutput = session.hardwareOutput;
        hardwareOutputCompletion = session.hardwareOutputCompletion;
        hardwareContent = session.hardwareContent;
        hardwareStatus = session.hardwareStatus;
        lastHardwareContentSerial = session.lastHardwareContentSerial;
        hardwareLastWritten = session.hardwareLastWritten;
        manualHardwareSubmission = session.manualHardwareSubmission;
        manualHardwareWasOffline = session.manualHardwareWasOffline;
        hardwareStill = session.hardwareStill;
        stoppedHardwareOutput = session.stoppedHardwareOutput;
        stoppedHardwareStill = session.stoppedHardwareStill;
        hardwareVideoResourceId = session.hardwareVideoResourceId;
        hardwarePreviewEnds = session.hardwarePreviewEnds;
        hardwarePreviewResourceId = session.hardwarePreviewResourceId;
        hardwarePlaylistId = session.hardwarePlaylistId;
        hardwarePlaylistItem = session.hardwarePlaylistItem;
        hardwarePlaylistRemaining = session.hardwarePlaylistRemaining;
        hardwarePlaylistIndex = session.hardwarePlaylistIndex;
        hardwarePlaylistPaused = session.hardwarePlaylistPaused;
        pendingHardwareRestore = session.pendingHardwareRestore;
        restoringStartup = session.restoringStartup;
        nextHardwareRestoreCheck = session.nextHardwareRestoreCheck;
        pendingHardwareScheduleStop = session.pendingHardwareScheduleStop;
        hardwareFailedPlaylistItems.Clear();
        hardwareFailedPlaylistItems.UnionWith(session.FailedItems);
    }

    void SelectHardwareSession(string? serial)
    {
        if (hardwareApplySerial == serial) return;
        if (hardwareApplySerial is { } previous) hardwareSessions[previous] = CaptureDeviceSession();
        LoadDeviceSession(serial);
    }

    T InHardwareSession<T>(string serial, Func<T> action)
    {
        if (hardwareApplySerial == serial) return action();
        var previous = hardwareApplySerial;
        var notice = Notice.Text;
        var background = backgroundHardwareContext;
        var visible = visibleHardwareSerial;
        if (previous != null) hardwareSessions[previous] = CaptureDeviceSession();
        visibleHardwareSerial = background ? visible : previous;
        backgroundHardwareContext = true;
        LoadDeviceSession(serial);
        try { return action(); }
        finally
        {
            hardwareSessions[serial] = CaptureDeviceSession();
            LoadDeviceSession(previous);
            backgroundHardwareContext = background;
            visibleHardwareSerial = visible;
            Notice.Text = notice;
        }
    }

    void InHardwareSession(string serial, Action action) => InHardwareSession(serial, () => { action(); return true; });

    string[] HardwareSessionSerials()
    {
        if (hardwareApplySerial is { } serial) hardwareSessions[serial] = CaptureDeviceSession();
        return hardwareSessions.Keys.ToArray();
    }

    void TickHardwareSessions(TimeSpan elapsed, TimeSpan now)
    {
        foreach (var serial in HardwareSessionSerials())
            InHardwareSession(serial, () =>
            {
                TickHardwarePlayback(elapsed);
                PollPendingHardwareRestore(now);
            });
    }

    D92StaticOutput? HardwareOutputFor(string serial) => hardwareApplySerial == serial ? hardwareOutput :
        hardwareSessions.GetValueOrDefault(serial)?.hardwareOutput;

    async Task StopAllHardwareTransport()
    {
        var sessions = HardwareSessionSerials().Select(serial => hardwareSessions[serial]).ToArray();
        foreach (var session in sessions) session.hardwareOutput?.Stop();
        await Task.WhenAll(sessions.Select(session => session.hardwareOutputCompletion));
    }

    List<HardwareOutputSnapshot> CaptureAllHardwareOutputs(bool stopped = false)
    {
        var outputs = new List<HardwareOutputSnapshot>();
        foreach (var serial in HardwareSessionSerials())
            InHardwareSession(serial, () =>
            {
                var output = stopped ? stoppedHardwareOutput : CaptureHardwareOutput();
                if (editorPreviewReturns.TryGetValue(serial, out var preview))
                    output = stopped ? preview.Running ? null : preview.Snapshot : preview.Running ? preview.Snapshot : null;
                if (output != null) outputs.Add(output);
            });
        return outputs;
    }
}
