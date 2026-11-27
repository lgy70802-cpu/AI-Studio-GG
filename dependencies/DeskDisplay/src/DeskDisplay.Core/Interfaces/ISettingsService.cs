using DeskDisplay.Core.Models;

namespace DeskDisplay.Core.Interfaces;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync();
    Task<bool> SaveAsync(AppSettings settings);
    async Task<bool> UpdateAsync(Action<AppSettings> update)
    {
        var settings = await LoadAsync();
        update(settings);
        return await SaveAsync(settings);
    }
}
