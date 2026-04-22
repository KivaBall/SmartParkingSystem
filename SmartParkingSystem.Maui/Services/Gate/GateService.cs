using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Gate;
using SmartParkingSystem.Maui.Services.DeviceConnection.Commands;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Gate;

public sealed class GateService(
    IDeviceSessionService sessionService,
    IDeviceCommandService commandService) : IGateService
{
    public Task<GateSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapSnapshot(sessionService.CurrentSession?.Snapshot));
    }

    public async Task<GateSnapshot> ForceOpenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSucceededAsync(
            commandService.SetForceOpenAsync(true, cancellationToken),
            "GATE FORCE_OPEN ON");
        return await RefreshSnapshotAsync(cancellationToken);
    }

    public async Task<GateSnapshot> OpenTemporarilyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSucceededAsync(
            commandService.OpenTemporarilyAsync(cancellationToken),
            "GATE OPEN_TEMP");
        return await RefreshSnapshotAsync(cancellationToken);
    }

    public async Task<GateSnapshot> CloseAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSucceededAsync(
            commandService.CloseGateAsync(cancellationToken),
            "GATE CLOSE");
        return await RefreshSnapshotAsync(cancellationToken);
    }

    public async Task<GateSnapshot> ToggleLockAsync(CancellationToken cancellationToken = default)
    {
        var currentSnapshot = sessionService.CurrentSession?.Snapshot;

        var isLocked = string.Equals(currentSnapshot?.Mode, "LOCKED", StringComparison.OrdinalIgnoreCase);
        await EnsureSucceededAsync(
            commandService.SetGateLockAsync(!isLocked, cancellationToken),
            isLocked ? "GATE LOCK OFF" : "GATE LOCK ON");
        return await RefreshSnapshotAsync(cancellationToken);
    }

    private async Task<GateSnapshot> RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await sessionService.RefreshSnapshotAsync(cancellationToken);
        if (snapshot is not null)
        {
            return MapSnapshot(snapshot);
        }

        throw new InvalidOperationException(
            "Gate command succeeded, but the controller snapshot could not be refreshed.");
    }

    private static GateSnapshot MapSnapshot(DeviceControllerSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new GateSnapshot(GateMode.Closed, false, false, 0);
        }

        var mode = snapshot.Mode.ToUpperInvariant() switch
        {
            "TEMP_OPEN" => GateMode.TemporaryOpen,
            "FORCED_OPEN" => GateMode.ForcedOpen,
            "LOCKED" => GateMode.Locked,
            _ => GateMode.Closed
        };

        return new GateSnapshot(
            mode,
            mode is GateMode.ForcedOpen or GateMode.TemporaryOpen,
            mode == GateMode.Locked,
            (int)Math.Ceiling(snapshot.RemainingMs / 1000d));
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