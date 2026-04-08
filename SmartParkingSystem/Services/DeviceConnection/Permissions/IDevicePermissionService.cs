namespace SmartParkingSystem.Services.DeviceConnection.Permissions;

public interface IDevicePermissionService
{
    Task<bool> EnsureConnectionPermissionsAsync(CancellationToken cancellationToken = default);
}