using SmartParkingSystem.Domain.Models.DeviceConnection;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Session;

public interface IDeviceSessionService
{
    DeviceControllerSession? CurrentSession { get; }
    bool IsValidated { get; }
    event Action<DeviceControllerSession?>? SessionChanged;

    Task<ConnectionResult> TryOpenSessionAsync(string targetId, CancellationToken cancellationToken = default);

    Task<ConnectionResult> TryAutoOpenSessionAsync(
        IReadOnlyList<ConnectionTarget> targets,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<DeviceControllerConfiguration?> RefreshConfigurationAsync(CancellationToken cancellationToken = default);
    Task<DeviceControllerSnapshot?> RefreshSnapshotAsync(CancellationToken cancellationToken = default);
    Task<DeviceControllerSession?> RefreshSessionAsync(CancellationToken cancellationToken = default);
}