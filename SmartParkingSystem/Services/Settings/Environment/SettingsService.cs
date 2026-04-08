using System.Globalization;
using Microsoft.JSInterop;
using SmartParkingSystem.Models.Localization;
using SmartParkingSystem.Models.Settings;

namespace SmartParkingSystem.Services.Settings.Environment;

public sealed class SettingsService(IJSRuntime jsRuntime) : ISettingsService
{
    public async Task<IReadOnlyList<SettingsInfoItem>> GetDeviceItemsAsync(SettingsTexts texts)
    {
        var viewportLabel = await jsRuntime.InvokeAsync<string>("getViewportLabel");

        return
        [
            new SettingsInfoItem(texts.PlatformLabel, DeviceInfo.Current.Platform.ToString()),
            new SettingsInfoItem(texts.ScreenLabel, viewportLabel),
            new SettingsInfoItem(texts.OsVersionLabel, DeviceInfo.Current.VersionString),
            new SettingsInfoItem(texts.LocalizationLabel, CultureInfo.CurrentUICulture.Name)
        ];
    }

    public IReadOnlyList<SettingsInfoItem> GetEnvironmentItems(SettingsTexts texts)
    {
        return
        [
            new SettingsInfoItem(texts.FrameworkLabel, ".NET MAUI Blazor Hybrid"),
            new SettingsInfoItem(texts.DotNetVersionLabel, System.Environment.Version.ToString()),
            new SettingsInfoItem(texts.StylingLabel, "Tailwind"),
            new SettingsInfoItem(texts.IconSystemLabel, "Lucide Icons")
        ];
    }
}