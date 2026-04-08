using SmartParkingSystem.Models.DeviceConnection;

namespace SmartParkingSystem.Services.DeviceConnection.Commands;

public interface IDeviceCommandService
{
    Task<DeviceCommandResult> SetForceOpenAsync(bool isEnabled, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> OpenTemporarilyAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> CloseGateAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetGateLockAsync(bool isEnabled, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SaveConfigurationAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> ResetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetOpenAngleAsync(int value, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetClosedAngleAsync(int value, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetOpenDurationAsync(int value, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetThresholdAsync(int value, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetTelemetryIntervalAsync(int value, CancellationToken cancellationToken = default);

    Task<DeviceCommandResult> SetSlotEnabledAsync(
        int slotNumber,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<DeviceCommandResult> AddAllowedCardAsync(string uid, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> RemoveAllowedCardAsync(string uid, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> ClearAllowedCardsAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> AddBlockedCardAsync(string uid, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> RemoveBlockedCardAsync(string uid, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> ClearBlockedCardsAsync(CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayForceAsync(bool isEnabled, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayForcedTextAsync(string text, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayDefaultTextAsync(string text, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayAllowedTextAsync(string text, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayBlockedTextAsync(string text, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayInvalidTextAsync(string text, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> SetDisplayLockedTextAsync(string text, CancellationToken cancellationToken = default);
}