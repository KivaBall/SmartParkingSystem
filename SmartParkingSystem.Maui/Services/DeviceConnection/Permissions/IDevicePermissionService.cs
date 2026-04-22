namespace SmartParkingSystem.Maui.Services.DeviceConnection.Permissions;

public interface IDevicePermissionService
{
    Task<bool> EnsureConnectionPermissionsAsync(CancellationToken cancellationToken = default);
}