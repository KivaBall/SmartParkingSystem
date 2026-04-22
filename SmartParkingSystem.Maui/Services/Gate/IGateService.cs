using SmartParkingSystem.Domain.Models.Gate;

namespace SmartParkingSystem.Maui.Services.Gate;

public interface IGateService
{
    Task<GateSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<GateSnapshot> ForceOpenAsync(CancellationToken cancellationToken = default);
    Task<GateSnapshot> OpenTemporarilyAsync(CancellationToken cancellationToken = default);
    Task<GateSnapshot> CloseAsync(CancellationToken cancellationToken = default);
    Task<GateSnapshot> ToggleLockAsync(CancellationToken cancellationToken = default);
}