using Microsoft.AspNetCore.SignalR;
using SmartParkingSystem.Domain.Models.BackendCommands;
using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.TelegramBot.Services.Commands;
using SmartParkingSystem.TelegramBot.Services.DeviceState;

namespace SmartParkingSystem.TelegramBot.Hubs;

public sealed class DeviceStateHub(
    DeviceStateIngestService ingestService,
    ConnectedDeviceHostRegistry connectionRegistry,
    BackendCommandDispatchService commandDispatchService) : Hub
{
    public const string Path = "/hubs/device-state";

    public override Task OnConnectedAsync()
    {
        connectionRegistry.SetActiveConnection(Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connectionRegistry.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task PushDeviceState(BackendDeviceStatePayload payload)
    {
        await ingestService.IngestAsync(payload, Context.ConnectionAborted);
    }

    public Task ReportCommandResult(BackendCommandExecutionResult result)
    {
        commandDispatchService.Complete(result);
        return Task.CompletedTask;
    }
}