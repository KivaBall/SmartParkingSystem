using SmartParkingSystem.Domain.Models.Admin;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Commands;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Admin;

public sealed class AdminService(
    IDeviceSessionService sessionService,
    IDeviceCommandService commandService) : IAdminService
{
    public Task<AdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildSnapshot(sessionService.CurrentSession?.Configuration));
    }

    public async Task<AdminSnapshot> SaveAsync(
        AdminEditableSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(settings);

        try
        {
            await EnsureSucceededAsync(
                commandService.SetOpenAngleAsync(normalized.ServoOpenAngle, cancellationToken),
                "CONFIG OPEN_ANGLE");
            await EnsureSucceededAsync(
                commandService.SetClosedAngleAsync(normalized.ServoClosedAngle, cancellationToken),
                "CONFIG CLOSED_ANGLE");
            await EnsureSucceededAsync(
                commandService.SetOpenDurationAsync(normalized.ServoOpenDurationMs, cancellationToken),
                "CONFIG OPEN_DURATION_MS");
            await EnsureSucceededAsync(
                commandService.SetThresholdAsync(normalized.OccupiedThresholdCm, cancellationToken),
                "CONFIG THRESHOLD_CM");
            await EnsureSucceededAsync(
                commandService.SetTelemetryIntervalAsync(normalized.ParkingStatusUpdateIntervalMs, cancellationToken),
                "CONFIG TELEMETRY_MS");
            await EnsureSucceededAsync(
                commandService.SetForceOpenAsync(normalized.ForceGateOpen, cancellationToken),
                "GATE FORCE_OPEN");
            await EnsureSucceededAsync(
                commandService.SetGateLockAsync(normalized.ForceGateLock, cancellationToken),
                "GATE LOCK");

            for (var index = 0; index < normalized.ParkingSpotEnabledStates.Count; index++)
            {
                var slotNumber = index + 1;
                await EnsureSucceededAsync(
                    commandService.SetSlotEnabledAsync(
                        slotNumber,
                        normalized.ParkingSpotEnabledStates[index],
                        cancellationToken),
                    $"PARKING SLOT {slotNumber}");
            }

            await RewriteCardsAsync(
                normalized.AllowedCardsText,
                "allowed card",
                commandService.ClearAllowedCardsAsync,
                commandService.AddAllowedCardAsync,
                cancellationToken);

            await RewriteCardsAsync(
                normalized.BlockedCardsText,
                "blocked card",
                commandService.ClearBlockedCardsAsync,
                commandService.AddBlockedCardAsync,
                cancellationToken);

            await EnsureSucceededAsync(commandService.SaveConfigurationAsync(cancellationToken), "CONFIG SAVE");
        }
        catch (Exception exception)
        {
            var refreshed = await TryRefreshActualControllerStateAsync(cancellationToken);
            var recoveryNote = refreshed
                ? " Controller state was refreshed after the failure and may now reflect a partial save."
                : " Controller state could not be refreshed after the failure and may now reflect a partial save.";

            throw new InvalidOperationException($"{exception.Message}{recoveryNote}", exception);
        }

        var configuration = await sessionService.RefreshConfigurationAsync(cancellationToken)
                            ?? sessionService.CurrentSession?.Configuration;

        return BuildSnapshot(configuration);
    }

    public async Task<AdminSnapshot> ResetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSucceededAsync(commandService.ResetConfigurationAsync(cancellationToken), "CONFIG RESET");

        var configuration = await sessionService.RefreshConfigurationAsync(cancellationToken)
                            ?? sessionService.CurrentSession?.Configuration;

        return BuildSnapshot(configuration);
    }

    private static async Task RewriteCardsAsync(
        string cardsText,
        string fieldName,
        Func<CancellationToken, Task<DeviceCommandResult>> clearAsync,
        Func<string, CancellationToken, Task<DeviceCommandResult>> addAsync,
        CancellationToken cancellationToken)
    {
        await EnsureSucceededAsync(clearAsync(cancellationToken), "CARDS CLEAR");

        foreach (var uid in ParseCards(cardsText, fieldName))
        {
            await EnsureSucceededAsync(addAsync(uid, cancellationToken), $"CARDS ADD {uid}");
        }
    }

    private static async Task EnsureSucceededAsync(Task<DeviceCommandResult> commandTask, string operation)
    {
        var result = await commandTask;
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Device command failed: {operation}. {DescribeFailure(result)}");
    }

    private static string DescribeFailure(DeviceCommandResult result)
    {
        return result.FailureKind switch
        {
            DeviceCommandFailureKind.TransportClosed => "Transport is not open.",
            DeviceCommandFailureKind.Timeout => $"Timed out while waiting for {result.Scope} acknowledgment.",
            DeviceCommandFailureKind.DeviceRejected when !string.IsNullOrWhiteSpace(result.ResponseLine) =>
                $"Controller rejected the command: {result.ResponseLine}",
            DeviceCommandFailureKind.DeviceRejected => "Controller rejected the command.",
            _ when !string.IsNullOrWhiteSpace(result.ResponseLine) => result.ResponseLine,
            _ => "Unknown controller failure."
        };
    }

    private static IReadOnlyList<string> ParseCards(string cardsText, string fieldName)
    {
        var parsedCards = new List<string>();
        var seenCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in cardsText.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedUid = NormalizeUidOrThrow(rawLine, fieldName);
            if (seenCards.Add(normalizedUid))
            {
                parsedCards.Add(normalizedUid);
            }
        }

        return parsedCards;
    }

    private static string NormalizeUidOrThrow(string uid, string fieldName)
    {
        var compact = new string(uid.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        if (compact.Length != 8)
        {
            throw new InvalidOperationException(
                $"Invalid {fieldName} entry: '{uid}'. Expected exactly 8 hexadecimal characters.");
        }

        return string.Join(' ', Enumerable.Range(0, 4).Select(index => compact.Substring(index * 2, 2)));
    }

    private static string FormatUid(string uid)
    {
        var compact = new string(uid.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        if (compact.Length != 8)
        {
            return uid.Trim();
        }

        return string.Join(' ', Enumerable.Range(0, 4).Select(index => compact.Substring(index * 2, 2)));
    }

    private static AdminEditableSettings Normalize(AdminEditableSettings settings)
    {
        var clone = settings.Clone();
        clone.ServoOpenAngle = Math.Clamp(clone.ServoOpenAngle, 0, 180);
        clone.ServoClosedAngle = Math.Clamp(clone.ServoClosedAngle, 0, 180);
        clone.ServoOpenDurationMs = Math.Max(250, clone.ServoOpenDurationMs);
        clone.OccupiedThresholdCm = Math.Max(1, clone.OccupiedThresholdCm);
        clone.ParkingStatusUpdateIntervalMs = Math.Max(250, clone.ParkingStatusUpdateIntervalMs);

        if (clone.ForceGateLock)
        {
            clone.ForceGateOpen = false;
        }

        clone.ParkingSpotEnabledStates = [.. clone.ParkingSpotEnabledStates];
        clone.AllowedCardsText = string.Join('\n', ParseCards(clone.AllowedCardsText, "allowed card"));
        clone.BlockedCardsText = string.Join('\n', ParseCards(clone.BlockedCardsText, "blocked card"));
        return clone;
    }

    private static AdminSnapshot BuildSnapshot(DeviceControllerConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new AdminSnapshot(new AdminEditableSettings());
        }

        var slots = configuration.SlotEnabled;
        return new AdminSnapshot(
            new AdminEditableSettings(
                configuration.OpenAngle,
                configuration.ClosedAngle,
                configuration.OpenDurationMs,
                configuration.ForceOpen,
                configuration.ForceLock,
                configuration.ThresholdCm,
                slots,
                configuration.TelemetryIntervalMs,
                string.Join('\n', configuration.AllowedCards.Select(FormatUid)),
                string.Join('\n', configuration.BlockedCards.Select(FormatUid))));
    }

    private async Task<bool> TryRefreshActualControllerStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await sessionService.RefreshSessionAsync(cancellationToken) is not null;
        }
        catch
        {
            return false;
        }
    }
}