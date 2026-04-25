namespace SmartParkingSystem.Maui.Services.BackendSync;

public sealed class BackendSyncOptions
{
    private const string WindowsBaseUrl = "http://127.0.0.1:5112";

    private const string AndroidBaseUrl = "http://10.0.2.2:5112";

    private const string DefaultBaseUrl = "http://127.0.0.1:5112";

    private const string HubPath = "hubs/device-state";

    public string ResolveHubUrl()
    {
        return CombineUrl(ResolveBaseUrl(), HubPath);
    }

    private string ResolveBaseUrl()
    {
        return DeviceInfo.Current.Platform switch
        {
            var platform when platform == DevicePlatform.Android => AndroidBaseUrl,
            var platform when platform == DevicePlatform.WinUI => WindowsBaseUrl,
            _ => DefaultBaseUrl
        };
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}