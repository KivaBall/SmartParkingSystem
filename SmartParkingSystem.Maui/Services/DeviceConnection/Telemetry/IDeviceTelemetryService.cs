using SmartParkingSystem.Domain.Models.DeviceConnection;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Telemetry;

public interface IDeviceTelemetryService
{
    Task<DeviceControllerProfile?> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<DeviceControllerConfiguration?> GetConfigurationAsync(
        int slotCount,
        CancellationToken cancellationToken = default);

    Task<DeviceControllerSnapshot?> GetSnapshotAsync(
        int slotCount,
        CancellationToken cancellationToken = default);
}