using SmartParkingSystem.Models.Localization;
using SmartParkingSystem.Models.Settings;

namespace SmartParkingSystem.Services.Settings.Environment;

public interface ISettingsService
{
    Task<IReadOnlyList<SettingsInfoItem>> GetDeviceItemsAsync(SettingsTexts texts);
    IReadOnlyList<SettingsInfoItem> GetEnvironmentItems(SettingsTexts texts);
}