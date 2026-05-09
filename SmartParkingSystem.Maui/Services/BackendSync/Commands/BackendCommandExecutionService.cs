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
                BackendCommandKind.ShowParkingRoute => await ShowParkingRouteAsync(
                    request.Argument,
                    cancellationToken),
                BackendCommandKind.ClearParkingRoute => await ClearParkingRouteAsync(cancellationToken),
                BackendCommandKind.ShowSmartParkingRoute => await ShowSmartParkingRouteAsync(cancellationToken),
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

    private async Task<string> ShowParkingRouteAsync(string? slotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new InvalidOperationException("Parking slot id was not provided.");
        }

        await parkingService.ShowRouteToSlotAsync(slotId, cancellationToken);
        return $"Parking route to {slotId} enabled.";
    }

    private async Task<string> ClearParkingRouteAsync(CancellationToken cancellationToken)
    {
        await parkingService.ClearRouteAsync(cancellationToken);
        return "Parking route cleared.";
    }

    private async Task<string> ShowSmartParkingRouteAsync(CancellationToken cancellationToken)
    {
        var slots = await parkingService.GetSlotsAsync(cancellationToken);
        var targetSlot = slots
            .Where(slot => slot.State == ParkingSlotState.Free)
            .Select(slot => new
            {
                Slot = slot,
                Number = ParseSlotNumber(slot.Id)
            })
            .Where(item => item.Number is >= 1 and <= 3)
            .OrderBy(item => item.Number)
            .FirstOrDefault()
            ?.Slot;

        if (targetSlot is null)
        {
            return "No free physical parking route slot is available.";
        }

        await parkingService.ShowRouteToSlotAsync(targetSlot.Id, cancellationToken);
        return $"Smart parking route to {targetSlot.Label} enabled.";
    }

    private static int? ParseSlotNumber(string slotId)
    {
        return slotId.Length >= 2 && slotId[0] == 'P' && int.TryParse(slotId[1..], out var number)
            ? number
            : null;
    }
}