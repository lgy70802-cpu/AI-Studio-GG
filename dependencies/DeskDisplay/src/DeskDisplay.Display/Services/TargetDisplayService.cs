using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Display.Services;

/// <summary>Matches saved identity against the latest snapshot without selecting an unrelated spare screen.</summary>
public sealed class TargetDisplayService : ITargetDisplayService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly int _schemaVersion;
    private TargetDisplaySettings? _saved;
    private TargetDisplaySettings? _persisted;
    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private DisplayInfo? _target;
    private bool _migrateLegacy;

    public TargetDisplaySettings? SavedTarget => _saved;
    public bool NeedsSave => _migrateLegacy || _saved != _persisted;

    public TargetDisplayService(AppSettings settings, ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _schemaVersion = settings.SchemaVersion;
        _saved = settings.TargetDisplay;
        if (_saved is null && !string.IsNullOrWhiteSpace(settings.TargetDisplayId))
        {
            var legacy = settings.TargetDisplayId.Trim();
            _saved = legacy.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase)
                ? new TargetDisplaySettings { DeviceName = legacy }
                : new TargetDisplaySettings { DeviceId = legacy };
            _migrateLegacy = true;
        }
        if (_saved is not null)
        {
            _saved = _saved with
            {
                DeviceId = string.IsNullOrWhiteSpace(_saved.DeviceId) ? null : _saved.DeviceId.Trim(),
                DeviceName = _saved.DeviceName?.Trim() ?? string.Empty,
                FriendlyName = _saved.FriendlyName?.Trim() ?? string.Empty,
                LastWidth = Math.Max(0, _saved.LastWidth),
                LastHeight = Math.Max(0, _saved.LastHeight)
            };
            if (_saved.DeviceId is null && _saved.DeviceName.Length == 0 && _saved.FriendlyName.Length == 0)
            {
                logger.Warning("Saved target has no usable identity; ignoring invalid target settings");
                _saved = null;
            }
        }
        _persisted = _saved;
    }

    public DisplayInfo? GetTargetDisplay() => _target;

    public void SetTargetDisplay(DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        // Revalidate against the latest snapshot, not just the UI's selected object.
        var current = _displays.SingleOrDefault(item => item == display);
        if (current is null || current.IsPrimary)
            throw new InvalidOperationException("Only a currently connected non-primary display can be selected.");
        _saved = Capture(current);
        _target = current;
        _logger.Information("Target display selected: {DeviceId} {DeviceName} {FriendlyName}", current.DeviceId, current.DeviceName, current.FriendlyName);
    }

    public void ClearTargetDisplay()
    {
        _saved = null;
        _target = null;
        _logger.Information("Target display selection cleared");
    }

    public void UpdateDisplays(IReadOnlyList<DisplayInfo> displays)
    {
        _displays = displays.ToArray();
        var resolved = ResolveSavedTargetDisplay();
        if (resolved is not null)
        {
            // Missing metadata during a transient driver failure must not erase a known stable ID.
            _saved = Capture(resolved) with
            {
                DeviceId = string.IsNullOrWhiteSpace(resolved.DeviceId) ? _saved?.DeviceId : resolved.DeviceId,
                FriendlyName = HasUsefulFriendlyName(resolved.FriendlyName, resolved.DeviceName)
                    ? resolved.FriendlyName : _saved?.FriendlyName ?? resolved.FriendlyName
            };
        }
    }

    public void MarkDisplaysUnavailable()
    {
        _displays = Array.Empty<DisplayInfo>();
        _target = null;
    }

    public DisplayInfo? ResolveSavedTargetDisplay()
    {
        _target = MatchSavedTarget();
        return _target;
    }

    private DisplayInfo? MatchSavedTarget()
    {
        if (_saved is null) return null;
        if (!string.IsNullOrWhiteSpace(_saved.DeviceId))
        {
            var idMatches = _displays.Where(item => Equal(item.DeviceId, _saved.DeviceId)).ToArray();
            // A matching primary display is not a target副屏; never fall through to another screen.
            if (idMatches.Length > 0) return UniqueSecondary(idMatches);
        }

        var usefulFriendlyName = HasUsefulFriendlyName(_saved.FriendlyName, _saved.DeviceName);
        if (!string.IsNullOrWhiteSpace(_saved.DeviceName))
        {
            var nameMatches = _displays.Where(item => Equal(item.DeviceName, _saved.DeviceName)).ToArray();
            var match = UniqueSecondary(nameMatches);
            if (match is not null)
            {
                var corroborated = usefulFriendlyName
                    ? Equal(match.FriendlyName, _saved.FriendlyName) && _displays.Count(item => Equal(item.FriendlyName, _saved.FriendlyName)) == 1
                    : string.IsNullOrWhiteSpace(_saved.DeviceId) || string.IsNullOrWhiteSpace(match.DeviceId);
                if (corroborated) return LogFallback(match, "DeviceName");
            }
        }

        if (usefulFriendlyName)
        {
            var match = UniqueSecondary(_displays.Where(item => Equal(item.FriendlyName, _saved.FriendlyName)).ToArray());
            if (match is not null) return LogFallback(match, "FriendlyName");
        }
        return null;
    }

    private DisplayInfo LogFallback(DisplayInfo display, string basis)
    {
        _logger.Information("Resolved saved target using {MatchBasis}: {DeviceName} {DeviceId}", basis, display.DeviceName, display.DeviceId);
        return display;
    }

    public async Task<bool> SaveAsync()
    {
        if (!NeedsSave) return true;
        var candidate = _saved;
        bool success;
        try
        {
            success = await _settingsService.UpdateAsync(settings =>
            {
                settings.SchemaVersion = _schemaVersion;
                settings.TargetDisplay = candidate;
                settings.TargetDisplayId = null;
            });
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Failed to persist target display");
            success = false;
        }
        if (success)
        {
            _persisted = candidate;
            _migrateLegacy = false;
        }
        else
        {
            _logger.Warning("Target settings were not saved; restoring the last persisted selection");
            _saved = _persisted;
            ResolveSavedTargetDisplay();
        }
        return success;
    }

    private static DisplayInfo? UniqueSecondary(DisplayInfo[] matches) => matches.Length == 1 && !matches[0].IsPrimary ? matches[0] : null;
    private static bool Equal(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static TargetDisplaySettings Capture(DisplayInfo display) => new()
    {
        DeviceId = display.DeviceId, DeviceName = display.DeviceName, FriendlyName = display.FriendlyName,
        LastWidth = display.Width, LastHeight = display.Height
    };

    private static bool HasUsefulFriendlyName(string? friendlyName, string deviceName) =>
        !string.IsNullOrWhiteSpace(friendlyName) && !Equal(friendlyName, deviceName) &&
        !friendlyName.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase) &&
        !new[] { "Generic PnP Monitor", "Generic Non-PnP Monitor", "Generic Monitor", "Unknown", "未知", "通用即插即用监视器", "通用非即插即用监视器" }
            .Contains(friendlyName, StringComparer.OrdinalIgnoreCase);
}
