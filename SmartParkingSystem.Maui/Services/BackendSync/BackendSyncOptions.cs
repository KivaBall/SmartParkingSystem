using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.BackendSync;

public sealed class BackendSyncOptions(ISettingsPreferencesService preferencesService)
{
    private const string HubPath = "hubs/device-state";

    private readonly string _hubUrl = CombineUrl(preferencesService.BackendBaseUrl, HubPath);

    public bool IsEnabled { get; } = preferencesService.BackendSyncEnabled;

    public string ResolveHubUrl()
    {
        return _hubUrl;
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
