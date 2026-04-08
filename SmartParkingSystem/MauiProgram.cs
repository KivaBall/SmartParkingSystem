using Microsoft.Extensions.Logging;
using SmartParkingSystem.Services.Admin;
using SmartParkingSystem.Services.Dashboard;
using SmartParkingSystem.Services.DeviceConnection.Commands;
using SmartParkingSystem.Services.DeviceConnection.Connection;
using SmartParkingSystem.Services.DeviceConnection.Execution;
using SmartParkingSystem.Services.DeviceConnection.Permissions;
using SmartParkingSystem.Services.DeviceConnection.Session;
using SmartParkingSystem.Services.DeviceConnection.Telemetry;
using SmartParkingSystem.Services.DeviceConnection.Transport;
using SmartParkingSystem.Services.Events;
using SmartParkingSystem.Services.Gate;
using SmartParkingSystem.Services.Localization;
using SmartParkingSystem.Services.Monitor;
using SmartParkingSystem.Services.Parking;
using SmartParkingSystem.Services.Settings.Environment;
using SmartParkingSystem.Services.Settings.Preferences;

namespace SmartParkingSystem;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(_ => { });

#if ANDROID
        builder.Services.AddSingleton<IDevicePermissionService, AndroidBluetoothPermissionService>();
        builder.Services.AddSingleton<IDeviceTransportService, AndroidBluetoothTransportService>();
#elif WINDOWS
        builder.Services.AddSingleton<IDevicePermissionService, NoOpDevicePermissionService>();
        builder.Services.AddSingleton<IDeviceTransportService, SerialDeviceTransportService>();
#else
        builder.Services.AddSingleton<IDevicePermissionService, NoOpDevicePermissionService>();
        builder.Services.AddSingleton<IDeviceTransportService, UnsupportedDeviceTransportService>();
#endif
        builder.Services.AddSingleton<IDeviceProtocolExecutionService, DeviceProtocolExecutionService>();
        builder.Services.AddSingleton<IDeviceTelemetryService, DeviceTelemetryService>();
        builder.Services.AddSingleton<IDeviceCommandService, DeviceCommandService>();
        builder.Services.AddSingleton<IDeviceSessionService, DeviceSessionService>();
        builder.Services.AddSingleton<IDeviceConnectionService, DeviceConnectionService>();
        builder.Services.AddSingleton<IDashboardService, DashboardService>();
        builder.Services.AddSingleton<IMonitorService, MonitorService>();
        builder.Services.AddSingleton<IEventsService, EventsService>();
        builder.Services.AddSingleton<IAdminService, AdminService>();
        builder.Services.AddSingleton<IGateService, GateService>();
        builder.Services.AddSingleton<IParkingService, ParkingService>();
        builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
        builder.Services.AddSingleton<ISettingsPreferencesService, SettingsPreferencesService>();
        builder.Services.AddScoped<ISettingsService, SettingsService>();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        _ = app.Services.GetRequiredService<IEventsService>();

        return app;
    }
}