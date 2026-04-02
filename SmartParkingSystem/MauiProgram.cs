using Microsoft.Extensions.Logging;
using SmartParkingSystem.Services.DeviceConnection;
using SmartParkingSystem.Services.Localization;

namespace SmartParkingSystem;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(_ => { });

        builder.Services.AddSingleton<IDeviceConnectionService, FakeDeviceConnectionService>();
        builder.Services.AddSingleton<ILocalizationService, FakeLocalizationService>();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}