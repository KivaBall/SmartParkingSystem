namespace SmartParkingSystem.Maui.Services.BackendSync;

public sealed class BackendSyncOptions
{
    public string WindowsBaseUrl { get; init; } = "http://localhost:5112";

    public string AndroidBaseUrl { get; init; } = "http://10.0.2.2:5112";

    public string DefaultBaseUrl { get; init; } = "http://localhost:5112";

    public string IngestPath { get; init; } = "api/device-state/ingest";

    public string ResolveBaseUrl()
    {
        var platform = DeviceInfo.Current.Platform;

        if (platform == DevicePlatform.Android)
        {
            return AndroidBaseUrl;
        }

        if (platform == DevicePlatform.WinUI)
        {
            return WindowsBaseUrl;
        }

        return DefaultBaseUrl;
    }
}
