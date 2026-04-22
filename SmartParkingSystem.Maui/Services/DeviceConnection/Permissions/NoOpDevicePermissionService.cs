namespace SmartParkingSystem.Maui.Services.DeviceConnection.Permissions;

public sealed class NoOpDevicePermissionService : IDevicePermissionService
{
    public Task<bool> EnsureConnectionPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}