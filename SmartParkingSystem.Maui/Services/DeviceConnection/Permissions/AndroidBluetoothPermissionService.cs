#if ANDROID
namespace SmartParkingSystem.Maui.Services.DeviceConnection.Permissions;

public sealed class AndroidBluetoothPermissionService : IDevicePermissionService
{
    public async Task<bool> EnsureConnectionPermissionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return true;
        }

        var connectStatus = await Microsoft.Maui.ApplicationModel.Permissions
            .CheckStatusAsync<BluetoothConnectPermission>();
        if (connectStatus != PermissionStatus.Granted)
        {
            connectStatus = await Microsoft.Maui.ApplicationModel.Permissions
                .RequestAsync<BluetoothConnectPermission>();
        }

        var scanStatus = await Microsoft.Maui.ApplicationModel.Permissions
            .CheckStatusAsync<BluetoothScanPermission>();
        if (scanStatus != PermissionStatus.Granted)
        {
            scanStatus = await Microsoft.Maui.ApplicationModel.Permissions
                .RequestAsync<BluetoothScanPermission>();
        }

        return connectStatus == PermissionStatus.Granted
               && scanStatus == PermissionStatus.Granted;
    }

    private sealed class BluetoothConnectPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.BLUETOOTH_CONNECT", true)];
    }

    private sealed class BluetoothScanPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.BLUETOOTH_SCAN", true)];
    }
}
#endif