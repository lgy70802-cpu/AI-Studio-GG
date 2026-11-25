using Serilog;
using Serilog.Debugging;

namespace DeskDisplay.Infrastructure.Logging;

public static class LoggingSetup
{
    public static string? ActiveLogDirectory { get; private set; }
    public static ILogger CreateLogger()
    {
        ActiveLogDirectory = null;
        SelfLog.Enable(message => System.Diagnostics.Trace.WriteLine(message));
        try
        {
            return CreateFileLogger(Path.Combine(AppPaths.DataDirectory, "logs"));
        }
        catch (Exception exception)
        {
            try
            {
                var logger = CreateFileLogger(Path.Combine(Path.GetTempPath(), "DeskDisplay", "logs"));
                logger.Error(exception, "Cannot initialize AppData logging; using temporary directory");
                return logger;
            }
            catch (Exception fallbackException)
            {
                System.Diagnostics.Trace.WriteLine(exception);
                System.Diagnostics.Trace.WriteLine(fallbackException);
                return new LoggerConfiguration().WriteTo.Sink(new TraceSink()).CreateLogger();
            }
        }
    }

    private static ILogger CreateFileLogger(string directory)
    {
        Directory.CreateDirectory(directory);
        // Probe explicitly: Serilog sinks can otherwise report open failures only through SelfLog.
        using (new FileStream(Path.Combine(directory, ".write-test"), FileMode.OpenOrCreate,
                   FileAccess.Write, FileShare.ReadWrite, 1, FileOptions.DeleteOnClose)) { }
        var logger = new LoggerConfiguration().MinimumLevel.Information()
            .WriteTo.File(Path.Combine(directory, "deskdisplay-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
            .CreateLogger();
        ActiveLogDirectory = directory;
        return logger;
    }

    private sealed class TraceSink : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) =>
            System.Diagnostics.Trace.WriteLine($"{logEvent.Timestamp:o} {logEvent.Level} {logEvent.RenderMessage()} {logEvent.Exception}");
    }
}
