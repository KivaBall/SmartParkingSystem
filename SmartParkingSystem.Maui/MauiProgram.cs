using Microsoft.Extensions.Logging;
using SmartParkingSystem.Maui.Services.Admin;
using SmartParkingSystem.Maui.Services.BackendSync;
using SmartParkingSystem.Maui.Services.BackendSync.Commands;
using SmartParkingSystem.Maui.Services.Dashboard;
using SmartParkingSystem.Maui.Services.DeviceConnection.Commands;
using SmartParkingSystem.Maui.Services.DeviceConnection.Connection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Execution;
using SmartParkingSystem.Maui.Services.DeviceConnection.Permissions;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.DeviceConnection.Telemetry;
using SmartParkingSystem.Maui.Services.DeviceConnection.Transport;
using SmartParkingSystem.Maui.Services.Events;
using SmartParkingSystem.Maui.Services.Gate;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Monitor;
using SmartParkingSystem.Maui.Services.Parking;
using SmartParkingSystem.Maui.Services.Settings.Environment;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var backendSyncOptions = new BackendSyncOptions();

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
        builder.Services.AddSingleton(backendSyncOptions);
        builder.Services.AddSingleton<IBackendCommandExecutionService, BackendCommandExecutionService>();
        builder.Services.AddSingleton<BackendSyncService>();
        builder.Services.AddScoped<ISettingsService, SettingsService>();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        _ = app.Services.GetRequiredService<IEventsService>();
        _ = app.Services.GetRequiredService<BackendSyncService>();

        return app;
    }
}