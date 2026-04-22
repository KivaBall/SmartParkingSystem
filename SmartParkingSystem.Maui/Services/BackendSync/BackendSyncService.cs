using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.Maui.Services.Admin;
using SmartParkingSystem.Maui.Services.Dashboard;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Events;
using SmartParkingSystem.Maui.Services.Gate;
using SmartParkingSystem.Maui.Services.Monitor;
using SmartParkingSystem.Maui.Services.Parking;

namespace SmartParkingSystem.Maui.Services.BackendSync;

public sealed class BackendSyncService : IDisposable
{
    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly BackendSyncOptions _options;
    private readonly IDashboardService _dashboardService;
    private readonly IDeviceSessionService _sessionService;
    private readonly IGateService _gateService;
    private readonly IMonitorService _monitorService;
    private readonly IParkingService _parkingService;
    private readonly IAdminService _adminService;
    private readonly IEventsService _eventsService;
    private readonly ILogger<BackendSyncService> _logger;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly SemaphoreSlim _pushGate = new(1, 1);
    private readonly PeriodicTimer _timer = new(PushInterval);
    private readonly Task _loopTask;

    public BackendSyncService(
        HttpClient httpClient,
        BackendSyncOptions options,
        IDashboardService dashboardService,
        IDeviceSessionService sessionService,
        IGateService gateService,
        IMonitorService monitorService,
        IParkingService parkingService,
        IAdminService adminService,
        IEventsService eventsService,
        ILogger<BackendSyncService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _dashboardService = dashboardService;
        _sessionService = sessionService;
        _gateService = gateService;
        _monitorService = monitorService;
        _parkingService = parkingService;
        _adminService = adminService;
        _eventsService = eventsService;
        _logger = logger;
        _loopTask = Task.Run(RunLoopAsync);
    }

    public void Dispose()
    {
        _shutdownTokenSource.Cancel();
        _timer.Dispose();
        _pushGate.Dispose();
        _shutdownTokenSource.Dispose();
    }

    private async Task RunLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_shutdownTokenSource.Token))
            {
                await PushIfIdleAsync(_shutdownTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PushIfIdleAsync(CancellationToken cancellationToken)
    {
        if (!await _pushGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var payload = await BuildPayloadAsync(cancellationToken);
            var response = await _httpClient.PostAsJsonAsync(
                _options.IngestPath,
                payload,
                PayloadJsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            _logger.LogWarning(
                "Backend sync failed with status code {StatusCode}",
                response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Backend sync push failed for {BaseAddress}/{Path}",
                _httpClient.BaseAddress,
                _options.IngestPath);
        }
        finally
        {
            _pushGate.Release();
        }
    }

    private async Task<BackendDeviceStatePayload> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        var dashboard = _dashboardService.GetSnapshot();
        var gate = await _gateService.GetSnapshotAsync(cancellationToken);
        var monitor = await _monitorService.GetSnapshotAsync(cancellationToken);
        var parkingSlots = await _parkingService.GetSlotsAsync(cancellationToken);
        var admin = await _adminService.GetSnapshotAsync(cancellationToken);
        var events = _eventsService.GetRecentEvents();

        return new BackendDeviceStatePayload(
            DateTimeOffset.UtcNow,
            DeviceInfo.Current.Platform.ToString(),
            AppInfo.Current.VersionString,
            dashboard,
            _sessionService.CurrentSession,
            gate,
            monitor,
            parkingSlots,
            admin,
            events);
    }
}
