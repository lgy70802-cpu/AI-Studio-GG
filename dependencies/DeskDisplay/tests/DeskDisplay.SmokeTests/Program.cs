using DeskDisplay.Core.Models;
using DeskDisplay.Infrastructure.Configuration;
using Serilog;

var directory = Path.Combine(Path.GetTempPath(), "DeskDisplay-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var sink = new CaptureSink();
    using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
    var path = Path.Combine(directory, "settings.json");
    var service = new JsonSettingsService(logger, path);
    var defaults = await service.LoadAsync();
    Check(defaults.SchemaVersion == 1 && defaults.TargetDisplayId is null && File.Exists(path), "Default configuration created");
    Check(await service.SaveAsync(new AppSettings { TargetDisplayId = "test-display" }), "Save succeeded");
    Check((await service.LoadAsync()).TargetDisplayId == "test-display", "Configuration round trip");

    foreach (var content in new[] { "{broken", "null", "{\"SchemaVersion\":99}", "{\"SchemaVersion\":\"bad\"}" })
    {
        await File.WriteAllTextAsync(path, content);
        var before = sink.ErrorCount;
        Check((await service.LoadAsync()).SchemaVersion == 1, "Invalid configuration uses defaults");
        Check(await File.ReadAllTextAsync(path) == content, "Invalid original preserved");
        Check(sink.ErrorCount > before, "Invalid configuration logged");
    }

    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        var before = sink.ErrorCount;
        Check((await service.LoadAsync()).SchemaVersion == 1 && sink.ErrorCount > before, "Locked configuration falls back and logs");
    }

    var blocker = Path.Combine(directory, "file-instead-of-directory");
    await File.WriteAllTextAsync(blocker, "blocked");
    var unwritable = new JsonSettingsService(logger, Path.Combine(blocker, "settings.json"));
    var errorsBefore = sink.ErrorCount;
    Check((await unwritable.LoadAsync()).SchemaVersion == 1, "Creation failure still returns defaults");
    Check(!await unwritable.SaveAsync(new AppSettings()), "Save failure reported");
    Check(sink.ErrorCount >= errorsBefore + 2, "I/O failures logged");
    Console.WriteLine("All configuration smoke tests passed.");
}
finally { Directory.Delete(directory, recursive: true); }

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine($"PASS: {name}");
}

sealed class CaptureSink : Serilog.Core.ILogEventSink
{
    public int ErrorCount { get; private set; }
    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        if (logEvent.Level >= Serilog.Events.LogEventLevel.Error) ErrorCount++;
    }
}
