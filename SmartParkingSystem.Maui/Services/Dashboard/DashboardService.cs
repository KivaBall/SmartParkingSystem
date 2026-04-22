using SmartParkingSystem.Domain.Models.Dashboard;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Gate;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Dashboard;

public sealed class DashboardService(IDeviceSessionService sessionService) : IDashboardService
{
    public DashboardSnapshot GetSnapshot()
    {
        return BuildSnapshot(sessionService.CurrentSession);
    }

    private static DashboardSnapshot BuildSnapshot(DeviceControllerSession? session)
    {
        if (session is null)
        {
            return new DashboardSnapshot(
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                GateMode.Closed,
                0,
                false,
                false,
                0,
                0,
                0);
        }

        var snapshot = session.Snapshot;
        var configuration = session.Configuration;
        var slotCapacity = snapshot.Slots.Count;
        var occupiedCount = snapshot.Slots.Count(slot => string.Equals(
            slot.State,
            "OCCUPIED",
            StringComparison.OrdinalIgnoreCase));
        var disabledCount = snapshot.Slots.Count(slot => string.Equals(
            slot.State,
            "DISABLED",
            StringComparison.OrdinalIgnoreCase) || !slot.Enabled);
        var freeCount = Math.Max(slotCapacity - occupiedCount - disabledCount, 0);

        var gateMode = snapshot.Mode.ToUpperInvariant() switch
        {
            "TEMP_OPEN" => GateMode.TemporaryOpen,
            "FORCED_OPEN" => GateMode.ForcedOpen,
            "LOCKED" => GateMode.Locked,
            _ => GateMode.Closed
        };

        return new DashboardSnapshot(
            true,
            session.Target.Label,
            session.Profile.Board,
            session.Profile.Transport,
            session.ConnectedAt,
            slotCapacity,
            freeCount,
            occupiedCount,
            disabledCount,
            snapshot.AllowedCount,
            snapshot.BlockedCount,
            gateMode,
            (int)Math.Ceiling(snapshot.RemainingMs / 1000d),
            snapshot.ForceOpen,
            snapshot.Locked,
            configuration.TelemetryIntervalMs,
            configuration.ThresholdCm,
            configuration.OpenDurationMs);
    }
}