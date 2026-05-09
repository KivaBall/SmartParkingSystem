using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.Maui.Services.DeviceConnection.Commands;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.Parking;

public sealed class ParkingService(
    IDeviceSessionService sessionService,
    IDeviceCommandService commandService,
    ISettingsPreferencesService settingsPreferencesService) : IParkingService
{
    private const int RouteAutoClearSeconds = 30;
    private readonly Lock _routeAutoClearSync = new Lock();
    private int _routeAutoClearVersion;

    public Task<IReadOnlyList<ParkingSlotSnapshot>> GetSlotsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ParkingSlotSnapshot>>(BuildSlots(sessionService.CurrentSession));
    }

    public async Task<IReadOnlyList<ParkingSlotSnapshot>> ToggleSlotEnabledAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        var slotNumber = ParseSlotNumber(slotId);
        if (slotNumber is null)
        {
            return await GetSlotsAsync(cancellationToken);
        }

        var snapshot = sessionService.CurrentSession?.Snapshot;

        var targetSlot = snapshot?.Slots.FirstOrDefault(slot => slot.SlotNumber == slotNumber.Value);
        var shouldEnable = targetSlot?.Enabled != true;

        await EnsureSucceededAsync(
            commandService.SetSlotEnabledAsync(slotNumber.Value, shouldEnable, cancellationToken),
            $"PARKING SLOT {slotNumber.Value}");
        await EnsureSucceededAsync(
            commandService.SaveConfigurationAsync(cancellationToken),
            "CONFIG SAVE");
        await sessionService.RefreshSessionAsync(cancellationToken);

        return BuildSlots(sessionService.CurrentSession);
    }

    public async Task ShowRouteToSlotAsync(string slotId, CancellationToken cancellationToken = default)
    {
        var slotNumber = ParseSlotNumber(slotId);
        if (slotNumber is null)
        {
            throw new ArgumentException("Slot id must use the P<number> format.", nameof(slotId));
        }

        await EnsureSucceededAsync(
            commandService.ShowRouteToSlotAsync(slotNumber.Value, cancellationToken),
            $"PARKING ROUTE {slotNumber.Value}");
        await sessionService.RefreshConfigurationAsync(cancellationToken);
        ScheduleRouteAutoClear();
    }

    public async Task ClearRouteAsync(CancellationToken cancellationToken = default)
    {
        CancelPendingRouteAutoClear();
        await EnsureSucceededAsync(
            commandService.ClearRouteAsync(cancellationToken),
            "PARKING ROUTE_CLEAR");
        await sessionService.RefreshConfigurationAsync(cancellationToken);
    }

    private void ScheduleRouteAutoClear()
    {
        int version;
        lock (_routeAutoClearSync)
        {
            version = ++_routeAutoClearVersion;
        }

        _ = ClearRouteAfterDelayAsync(version);
    }

    private void CancelPendingRouteAutoClear()
    {
        lock (_routeAutoClearSync)
        {
            _routeAutoClearVersion++;
        }
    }

    private async Task ClearRouteAfterDelayAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(RouteAutoClearSeconds));

        lock (_routeAutoClearSync)
        {
            if (version != _routeAutoClearVersion)
            {
                return;
            }

            _routeAutoClearVersion++;
        }

        try
        {
            await EnsureSucceededAsync(
                commandService.ClearRouteAsync(),
                "PARKING ROUTE_CLEAR");
            await sessionService.RefreshConfigurationAsync();
        }
        catch
        {
            // Route auto-clear is best-effort; the next manual command or session refresh will recover state.
        }
    }

    private ParkingSlotSnapshot[] BuildSlots(DeviceControllerSession? session)
    {
        if (session is null)
        {
            return [];
        }

        var snapshot = session.Snapshot;
        var slotCount = Math.Max(
            session.Profile.SlotCount,
            Math.Max(snapshot.Slots.Count, session.Configuration.SlotEnabled.Count));

        if (slotCount <= 0)
        {
            return [];
        }

        var slots = new ParkingSlotSnapshot[slotCount];

        for (var index = 0; index < slotCount; index++)
        {
            var slotNumber = index + 1;
            var slot = snapshot.Slots.FirstOrDefault(item => item.SlotNumber == slotNumber);
            var isEnabled = slot?.Enabled
                            ?? (slotNumber <= session.Configuration.SlotEnabled.Count &&
                                session.Configuration.SlotEnabled[slotNumber - 1]);

            var state = slot?.State.ToUpperInvariant() switch
            {
                "OCCUPIED" => ParkingSlotState.Occupied,
                "DISABLED" => ParkingSlotState.Disabled,
                _ when !isEnabled => ParkingSlotState.Disabled,
                _ => ParkingSlotState.Free
            };

            TimeSpan? occupiedDuration = slot is { OccupiedMs: > 0 }
                ? TimeSpan.FromMilliseconds(slot.OccupiedMs)
                : null;

            slots[index] = new ParkingSlotSnapshot(
                $"P{slotNumber}",
                $"P{slotNumber}",
                state,
                occupiedDuration,
                settingsPreferencesService.GetParkingSlotFloor($"P{slotNumber}"));
        }

        return slots;
    }

    private static int? ParseSlotNumber(string slotId)
    {
        return slotId.Length >= 2 && slotId[0] == 'P' && int.TryParse(slotId[1..], out var number)
            ? number
            : null;
    }

    private static async Task EnsureSucceededAsync(Task<DeviceCommandResult> commandTask, string operation)
    {
        var result = await commandTask;
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Device command failed: {operation}. {DescribeFailure(result)}");
    }

    private static string DescribeFailure(DeviceCommandResult result)
    {
        return result.FailureKind switch
        {
            DeviceCommandFailureKind.TransportClosed => "Transport is not open.",
            DeviceCommandFailureKind.Timeout => $"Timed out while waiting for {result.Scope} acknowledgment.",
            DeviceCommandFailureKind.DeviceRejected when !string.IsNullOrWhiteSpace(result.ResponseLine) =>
                $"Controller rejected the command: {result.ResponseLine}",
            DeviceCommandFailureKind.DeviceRejected => "Controller rejected the command.",
            _ when !string.IsNullOrWhiteSpace(result.ResponseLine) => result.ResponseLine,
            _ => "Unknown controller failure."
        };
    }
}