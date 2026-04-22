using SmartParkingSystem.Domain.Models.DeviceConnection;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Connection;

public interface IDeviceConnectionService
{
    Task<IReadOnlyList<ConnectionTarget>> GetTargetsAsync();
    Task<ConnectionResult> TryAutoConnectAsync();
    Task<ConnectionResult> TryConnectAsync(string? targetId);
    Task<IReadOnlyList<ConnectionTarget>> RefreshTargetsAsync();
}