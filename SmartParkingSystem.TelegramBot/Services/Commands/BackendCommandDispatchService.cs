using Microsoft.AspNetCore.SignalR;
using SmartParkingSystem.Domain.Models.BackendCommands;
using SmartParkingSystem.TelegramBot.Hubs;

namespace SmartParkingSystem.TelegramBot.Services.Commands;

public sealed class BackendCommandDispatchService(
    IHubContext<DeviceStateHub> hubContext,
    ConnectedDeviceHostRegistry connectionRegistry)
{
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(5);
    private readonly Dictionary<string, TaskCompletionSource<BackendCommandExecutionResult>> _pending = [];

    private readonly Lock _sync = new Lock();

    public async Task<BackendCommandExecutionResult> DispatchAsync(
        BackendCommandKind kind,
        string? argument,
        CancellationToken cancellationToken)
    {
        var connectionId = connectionRegistry.GetActiveConnection();
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return new BackendCommandExecutionResult(
                Guid.NewGuid().ToString("N"),
                kind,
                false,
                "No active MAUI device host is connected.",
                DateTimeOffset.UtcNow);
        }

        var request = new BackendCommandRequest(
            Guid.NewGuid().ToString("N"),
            kind,
            DateTimeOffset.UtcNow,
            argument);

        var completion = new TaskCompletionSource<BackendCommandExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _pending[request.CommandId] = completion;
        }

        try
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync(
                    "ExecuteBackendCommand",
                    request,
                    cancellationToken);

            using var timeoutCts = new CancellationTokenSource(DispatchTimeout);
            await using var timeoutRegistration = timeoutCts.Token.Register(() => completion.TrySetResult(
                new BackendCommandExecutionResult(
                    request.CommandId,
                    kind,
                    false,
                    "The MAUI host did not return a command result in time.",
                    DateTimeOffset.UtcNow)));
            await using var cancellationRegistration = cancellationToken.Register(() => completion.TrySetResult(
                new BackendCommandExecutionResult(
                    request.CommandId,
                    kind,
                    false,
                    "The command request was canceled.",
                    DateTimeOffset.UtcNow)));

            return await completion.Task;
        }
        finally
        {
            lock (_sync)
            {
                _pending.Remove(request.CommandId);
            }
        }
    }

    public void Complete(BackendCommandExecutionResult result)
    {
        TaskCompletionSource<BackendCommandExecutionResult>? completion;
        lock (_sync)
        {
            _pending.TryGetValue(result.CommandId, out completion);
        }

        completion?.TrySetResult(result);
    }
}