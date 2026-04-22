using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Settings;

namespace SmartParkingSystem.Maui.Services.Settings.Environment;

public interface ISettingsService
{
    Task<IReadOnlyList<SettingsInfoItem>> GetDeviceItemsAsync(SettingsTexts texts);
    IReadOnlyList<SettingsInfoItem> GetEnvironmentItems(SettingsTexts texts);
}