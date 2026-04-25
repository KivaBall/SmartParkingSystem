using SmartParkingSystem.Domain.Models.BackendCommands;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Gate;
using SmartParkingSystem.Maui.Services.Parking;

namespace SmartParkingSystem.Maui.Services.BackendSync.Commands;

public sealed class BackendCommandExecutionService(
    IGateService gateService,
    IDeviceSessionService sessionService,
    IParkingService parkingService) : IBackendCommandExecutionService
{
    public async Task<BackendCommandExecutionResult> ExecuteAsync(
        BackendCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = request.Kind switch
            {
                BackendCommandKind.RefreshState => await RefreshStateAsync(cancellationToken),
                BackendCommandKind.ForceOpenGate => await ForceOpenGateAsync(cancellationToken),
                BackendCommandKind.OpenGateTemporarily => await OpenGateTemporarilyAsync(cancellationToken),
                BackendCommandKind.CloseGate => await CloseGateAsync(cancellationToken),
                BackendCommandKind.ToggleGateLock => await ToggleGateLockAsync(cancellationToken),
                BackendCommandKind.ToggleParkingSlot => await ToggleParkingSlotAsync(
                    request.Argument,
                    cancellationToken),
                _ => "Unsupported command."
            };

            return new BackendCommandExecutionResult(
                request.CommandId,
                request.Kind,
                true,
                message,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            return new BackendCommandExecutionResult(
                request.CommandId,
                request.Kind,
                false,
                exception.Message,
                DateTimeOffset.UtcNow);
        }
    }

    private async Task<string> RefreshStateAsync(CancellationToken cancellationToken)
    {
        await sessionService.RefreshSessionAsync(cancellationToken);
        return "System state refreshed.";
    }

    private async Task<string> OpenGateTemporarilyAsync(CancellationToken cancellationToken)
    {
        await gateService.OpenTemporarilyAsync(cancellationToken);
        return "Gate opened temporarily.";
    }

    private async Task<string> ForceOpenGateAsync(CancellationToken cancellationToken)
    {
        await gateService.ForceOpenAsync(cancellationToken);
        return "Gate forced open.";
    }

    private async Task<string> CloseGateAsync(CancellationToken cancellationToken)
    {
        await gateService.CloseAsync(cancellationToken);
        return "Gate closed.";
    }

    private async Task<string> ToggleGateLockAsync(CancellationToken cancellationToken)
    {
        var snapshot = await gateService.ToggleLockAsync(cancellationToken);
        return snapshot.IsLocked
            ? "Gate lock enabled."
            : "Gate lock disabled.";
    }

    private async Task<string> ToggleParkingSlotAsync(string? slotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new InvalidOperationException("Parking slot id was not provided.");
        }

        var slots = await parkingService.ToggleSlotEnabledAsync(slotId, cancellationToken);
        var targetSlot =
            slots.FirstOrDefault(slot => string.Equals(slot.Id, slotId, StringComparison.OrdinalIgnoreCase));
        if (targetSlot is null)
        {
            return $"Parking slot {slotId} updated.";
        }

        return targetSlot.State switch
        {
            ParkingSlotState.Disabled => $"Parking slot {targetSlot.Label} disabled.",
            ParkingSlotState.Free => $"Parking slot {targetSlot.Label} enabled and free.",
            ParkingSlotState.Occupied => $"Parking slot {targetSlot.Label} enabled and occupied.",
            _ => $"Parking slot {targetSlot.Label} updated."
        };
    }
}