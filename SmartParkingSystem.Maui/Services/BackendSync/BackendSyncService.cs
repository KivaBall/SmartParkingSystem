using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using SmartParkingSystem.Domain.Models.BackendCommands;
using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.Maui.Services.Admin;
using SmartParkingSystem.Maui.Services.BackendSync.Commands;
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
    private readonly IAdminService _adminService;
    private readonly IBackendCommandExecutionService _commandExecutionService;
    private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1);
    private readonly IDashboardService _dashboardService;
    private readonly IEventsService _eventsService;
    private readonly IGateService _gateService;
    private readonly HubConnection _hubConnection;
    private readonly IMonitorService _monitorService;
    private readonly IParkingService _parkingService;
    private readonly SemaphoreSlim _pushGate = new SemaphoreSlim(1, 1);
    private readonly IDeviceSessionService _sessionService;
    private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
    private readonly PeriodicTimer _timer = new PeriodicTimer(PushInterval);

    public BackendSyncService(
        BackendSyncOptions options,
        IDashboardService dashboardService,
        IDeviceSessionService sessionService,
        IGateService gateService,
        IMonitorService monitorService,
        IParkingService parkingService,
        IAdminService adminService,
        IEventsService eventsService,
        IBackendCommandExecutionService commandExecutionService)
    {
        _dashboardService = dashboardService;
        _sessionService = sessionService;
        _gateService = gateService;
        _monitorService = monitorService;
        _parkingService = parkingService;
        _adminService = adminService;
        _eventsService = eventsService;
        _commandExecutionService = commandExecutionService;
        _hubConnection = BuildHubConnection(options);
        _hubConnection.On<BackendCommandRequest>(
            "ExecuteBackendCommand",
            async request =>
            {
                var result = await _commandExecutionService.ExecuteAsync(
                    request,
                    _shutdownTokenSource.Token);

                try
                {
                    await _hubConnection.InvokeAsync(
                        "ReportCommandResult",
                        result,
                        _shutdownTokenSource.Token);
                }
                catch (Exception)
                {
                    // The next state sync keeps the backend in a recoverable state if reporting fails.
                }
            });
        _ = Task.Run(RunLoopAsync);
    }

    public void Dispose()
    {
        _shutdownTokenSource.Cancel();
        _timer.Dispose();

        try
        {
            _hubConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Disposal must stay best-effort because app shutdown can race the SignalR transport.
        }

        _pushGate.Dispose();
        _connectGate.Dispose();
        _shutdownTokenSource.Dispose();
    }

    private static HubConnection BuildHubConnection(BackendSyncOptions options)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                options.ResolveHubUrl(),
                transportOptions => { transportOptions.Transports = HttpTransportType.LongPolling; })
            .AddJsonProtocol(protocolOptions =>
            {
                protocolOptions.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .WithAutomaticReconnect()
            .Build();
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
            if (!await EnsureConnectedAsync(cancellationToken))
            {
                return;
            }

            var payload = await BuildPayloadAsync(cancellationToken);
            await _hubConnection.InvokeAsync("PushDeviceState", payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // Backend sync is opportunistic; the periodic loop will retry on the next tick.
        }
        finally
        {
            _pushGate.Release();
        }
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection.State == HubConnectionState.Connected)
        {
            return true;
        }

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                return true;
            }

            if (_hubConnection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                return false;
            }

            await _hubConnection.StartAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _connectGate.Release();
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