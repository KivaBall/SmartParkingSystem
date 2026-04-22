using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Execution;
using SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Commands;

public sealed class DeviceCommandService(
    IDeviceTransportService transportService,
    IDeviceProtocolExecutionService protocolExecutionService) : IDeviceCommandService
{
    public Task<DeviceCommandResult> SetForceOpenAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"GATE FORCE_OPEN {(isEnabled ? "ON" : "OFF")}",
            "GATE",
            "OK|GATE|FORCE_OPEN_UPDATED",
            cancellationToken,
            ["ERR|GATE|MISSING_FORCE_OPEN_VALUE"]);
    }

    public Task<DeviceCommandResult> OpenTemporarilyAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "GATE OPEN_TEMP",
            "GATE",
            "OK|GATE|TEMP_OPEN_STARTED",
            cancellationToken,
            ["ERR|GATE|LOCKED"]);
    }

    public Task<DeviceCommandResult> CloseGateAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "GATE CLOSE",
            "GATE",
            "OK|GATE|CLOSED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetGateLockAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"GATE LOCK {(isEnabled ? "ON" : "OFF")}",
            "GATE",
            "OK|GATE|LOCK_UPDATED",
            cancellationToken,
            ["ERR|GATE|MISSING_LOCK_VALUE", "ERR|GATE|INVALID_LOCK_VALUE"]);
    }

    public Task<DeviceCommandResult> SaveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "CONFIG SAVE",
            "CONFIG",
            "OK|CONFIG|SAVED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> ResetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "CONFIG RESET",
            "CONFIG",
            "OK|CONFIG|RESET_TO_DEFAULTS",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetOpenAngleAsync(int value, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CONFIG OPEN_ANGLE {value}",
            "CONFIG",
            "OK|CONFIG|OPEN_ANGLE_UPDATED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetClosedAngleAsync(int value, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CONFIG CLOSED_ANGLE {value}",
            "CONFIG",
            "OK|CONFIG|CLOSED_ANGLE_UPDATED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetOpenDurationAsync(int value, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CONFIG OPEN_DURATION_MS {value}",
            "CONFIG",
            "OK|CONFIG|OPEN_DURATION_UPDATED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetThresholdAsync(int value, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CONFIG THRESHOLD_CM {value}",
            "CONFIG",
            "OK|CONFIG|THRESHOLD_UPDATED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetTelemetryIntervalAsync(int value, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CONFIG TELEMETRY_MS {value}",
            "CONFIG",
            "OK|CONFIG|TELEMETRY_UPDATED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetSlotEnabledAsync(
        int slotNumber,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        if (slotNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "Slot number must be greater than zero.");
        }

        return SendCommandAsync(
            $"PARKING {(isEnabled ? "ENABLE" : "DISABLE")} {slotNumber}",
            "PARKING",
            isEnabled ? "OK|PARKING|SLOT_ENABLED" : "OK|PARKING|SLOT_DISABLED",
            cancellationToken,
            ["ERR|PARKING|INVALID_COMMAND", "ERR|PARKING|INVALID_SLOT", "ERR|PARKING|UNKNOWN_ACTION"]);
    }

    public Task<DeviceCommandResult> AddAllowedCardAsync(string uid, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CARDS ALLOWED ADD {NormalizeUid(uid)}",
            "CARDS",
            "OK|CARDS|ALLOWED_ADDED",
            cancellationToken,
            ["ERR|CARDS|ALLOWED_ADD_FAILED", "ERR|CARDS|INVALID_UID", "ERR|CARDS|MISSING_UID"]);
    }

    public Task<DeviceCommandResult> RemoveAllowedCardAsync(string uid, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CARDS ALLOWED REMOVE {NormalizeUid(uid)}",
            "CARDS",
            "OK|CARDS|ALLOWED_REMOVED",
            cancellationToken,
            ["ERR|CARDS|ALLOWED_REMOVE_FAILED", "ERR|CARDS|INVALID_UID", "ERR|CARDS|MISSING_UID"]);
    }

    public Task<DeviceCommandResult> ClearAllowedCardsAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "CARDS ALLOWED CLEAR",
            "CARDS",
            "OK|CARDS|ALLOWED_CLEARED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> AddBlockedCardAsync(string uid, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CARDS BLOCKED ADD {NormalizeUid(uid)}",
            "CARDS",
            "OK|CARDS|BLOCKED_ADDED",
            cancellationToken,
            ["ERR|CARDS|BLOCKED_ADD_FAILED", "ERR|CARDS|INVALID_UID", "ERR|CARDS|MISSING_UID"]);
    }

    public Task<DeviceCommandResult> RemoveBlockedCardAsync(string uid, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"CARDS BLOCKED REMOVE {NormalizeUid(uid)}",
            "CARDS",
            "OK|CARDS|BLOCKED_REMOVED",
            cancellationToken,
            ["ERR|CARDS|BLOCKED_REMOVE_FAILED", "ERR|CARDS|INVALID_UID", "ERR|CARDS|MISSING_UID"]);
    }

    public Task<DeviceCommandResult> ClearBlockedCardsAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            "CARDS BLOCKED CLEAR",
            "CARDS",
            "OK|CARDS|BLOCKED_CLEARED",
            cancellationToken);
    }

    public Task<DeviceCommandResult> SetDisplayForceAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY FORCE {(isEnabled ? "ON" : "OFF")}",
            "DISPLAY",
            "OK|DISPLAY|FORCE_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_FORCE_VALUE", "ERR|DISPLAY|INVALID_FORCE_VALUE"]);
    }

    public Task<DeviceCommandResult> SetDisplayForcedTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT FORCED {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_FORCED_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    public Task<DeviceCommandResult> SetDisplayDefaultTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT DEFAULT {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_DEFAULT_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    public Task<DeviceCommandResult> SetDisplayAllowedTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT ALLOWED {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_ALLOWED_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    public Task<DeviceCommandResult> SetDisplayBlockedTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT BLOCKED {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_BLOCKED_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    public Task<DeviceCommandResult> SetDisplayInvalidTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT INVALID {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_INVALID_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    public Task<DeviceCommandResult> SetDisplayLockedTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(
            $"DISPLAY TEXT LOCKED {NormalizeDisplayText(text)}",
            "DISPLAY",
            "OK|DISPLAY|TEXT_LOCKED_UPDATED",
            cancellationToken,
            ["ERR|DISPLAY|MISSING_TEXT_KEY", "ERR|DISPLAY|UNKNOWN_TEXT_KEY"]);
    }

    private async Task<DeviceCommandResult> SendCommandAsync(
        string command,
        string expectedScope,
        string expectedSuccessPrefix,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? expectedErrorPrefixes = null)
    {
        return await protocolExecutionService.RunExclusiveAsync(
            async token =>
            {
                if (!transportService.IsOpen)
                {
                    return DeviceCommandResult.Failure(
                        expectedScope,
                        DeviceCommandFailureKind.TransportClosed);
                }

                await transportService.SendLineAsync(command, token);

                var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(2);
                while (DateTimeOffset.UtcNow < timeoutAt)
                {
                    var line = await transportService.ReadLineAsync(TimeSpan.FromMilliseconds(250), token);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.StartsWith(expectedSuccessPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return DeviceCommandResult.Success(expectedScope, line);
                    }

                    if (expectedErrorPrefixes is not null
                        && expectedErrorPrefixes.Any(prefix => line.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        return DeviceCommandResult.Failure(
                            expectedScope,
                            DeviceCommandFailureKind.DeviceRejected,
                            line);
                    }
                }

                return DeviceCommandResult.Failure(
                    expectedScope,
                    DeviceCommandFailureKind.Timeout);
            },
            cancellationToken);
    }

    private static string NormalizeUid(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        var normalized = new string(uid.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length != 8)
        {
            throw new ArgumentException("UID must contain exactly 8 hexadecimal characters.", nameof(uid));
        }

        return normalized;
    }

    private static string NormalizeDisplayText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var trimmed = text.Trim();
        if (trimmed.Length > 16)
        {
            throw new ArgumentException("Display text must be 16 characters or fewer.", nameof(text));
        }

        if (trimmed.Any(character => character < 32 || character > 126 || character == '|'))
        {
            throw new ArgumentException(
                "Display text must use printable ASCII only and cannot contain '|'.",
                nameof(text));
        }

        return trimmed;
    }
}