using System.Text.Json;
using DeskDisplay.Core.Interfaces;
using DeskDisplay.Core.Models;
using Serilog;

namespace DeskDisplay.Infrastructure.Configuration;

public sealed class JsonSettingsService(ILogger logger, string? filePath = null) : ISettingsService
{
    private readonly string _filePath = filePath ?? AppPaths.SettingsFile;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync()
    {
        await _gate.WaitAsync();
        try { return await LoadCoreAsync(); }
        finally { _gate.Release(); }
    }

    public async Task<bool> UpdateAsync(Action<AppSettings> update)
    {
        await _gate.WaitAsync();
        try
        {
            var settings = await LoadCoreAsync();
            update(settings);
            return await SaveCoreAsync(settings);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Unable to update settings at {Path}", _filePath);
            return false;
        }
        finally { _gate.Release(); }
    }

    private async Task<AppSettings> LoadCoreAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new AppSettings();
                await SaveCoreAsync(defaults);
                return defaults;
            }

            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options);
            if (settings is null || settings.SchemaVersion != 1)
                throw new JsonException("Settings are null or use an unsupported schema version.");
            return settings;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Unable to load settings from {Path}; using defaults in memory", _filePath);
            // Preserve the original file for diagnosis; a broken or inaccessible file never blocks startup.
            return new AppSettings();
        }
    }

    public async Task<bool> SaveAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try { return await SaveCoreAsync(settings); }
        finally { _gate.Release(); }
    }

    private async Task<bool> SaveCoreAsync(AppSettings settings)
    {
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(stream, settings, Options);
            File.Move(temporaryPath, _filePath, overwrite: true);
            logger.Information("Settings saved to {Path}", _filePath);
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Unable to save settings to {Path}", _filePath);
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception exception) { logger.Warning(exception, "Unable to delete temporary settings file"); }
            }
        }
    }
}
