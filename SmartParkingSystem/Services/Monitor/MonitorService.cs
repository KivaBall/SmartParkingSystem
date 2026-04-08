using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Models.Monitor;
using SmartParkingSystem.Services.DeviceConnection.Commands;
using SmartParkingSystem.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Services.Monitor;

public sealed class MonitorService(
    IDeviceSessionService sessionService,
    IDeviceCommandService commandService) : IMonitorService
{
    private static readonly MonitorEditableSettings DefaultSettings = new MonitorEditableSettings(
        false,
        "FORCED TEXT",
        "SMART PARKING",
        "ACCESS GRANTED",
        "BLOCKED CARD",
        "INVALID CARD",
        "ACCESS LOCKED");

    public Task<MonitorSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildSnapshot(sessionService.CurrentSession));
    }

    public async Task<MonitorSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await sessionService.RefreshSessionAsync(cancellationToken);
        return BuildSnapshot(sessionService.CurrentSession);
    }

    public async Task<MonitorSnapshot> SaveAsync(
        MonitorEditableSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = ValidateAndNormalize(settings);

        await EnsureSucceededAsync(
            commandService.SetDisplayForceAsync(normalized.ForceEnabled, cancellationToken),
            "DISPLAY FORCE");
        await EnsureSucceededAsync(
            commandService.SetDisplayForcedTextAsync(normalized.ForcedText, cancellationToken),
            "DISPLAY FORCED");
        await EnsureSucceededAsync(
            commandService.SetDisplayDefaultTextAsync(normalized.DefaultText, cancellationToken),
            "DISPLAY DEFAULT");
        await EnsureSucceededAsync(
            commandService.SetDisplayAllowedTextAsync(normalized.AllowedText, cancellationToken),
            "DISPLAY ALLOWED");
        await EnsureSucceededAsync(
            commandService.SetDisplayBlockedTextAsync(normalized.BlockedText, cancellationToken),
            "DISPLAY BLOCKED");
        await EnsureSucceededAsync(
            commandService.SetDisplayInvalidTextAsync(normalized.InvalidText, cancellationToken),
            "DISPLAY INVALID");
        await EnsureSucceededAsync(
            commandService.SetDisplayLockedTextAsync(normalized.LockedText, cancellationToken),
            "DISPLAY LOCKED");
        await EnsureSucceededAsync(commandService.SaveConfigurationAsync(cancellationToken), "CONFIG SAVE");

        await RefreshConfigurationOrThrowAsync(cancellationToken);
        _ = TryRefreshSnapshotAsync();
        return BuildSnapshot(sessionService.CurrentSession);
    }

    public async Task<MonitorSnapshot> ResetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSucceededAsync(
            commandService.SetDisplayForceAsync(DefaultSettings.ForceEnabled, cancellationToken),
            "DISPLAY FORCE");
        await EnsureSucceededAsync(
            commandService.SetDisplayForcedTextAsync(DefaultSettings.ForcedText, cancellationToken),
            "DISPLAY FORCED");
        await EnsureSucceededAsync(
            commandService.SetDisplayDefaultTextAsync(DefaultSettings.DefaultText, cancellationToken),
            "DISPLAY DEFAULT");
        await EnsureSucceededAsync(
            commandService.SetDisplayAllowedTextAsync(DefaultSettings.AllowedText, cancellationToken),
            "DISPLAY ALLOWED");
        await EnsureSucceededAsync(
            commandService.SetDisplayBlockedTextAsync(DefaultSettings.BlockedText, cancellationToken),
            "DISPLAY BLOCKED");
        await EnsureSucceededAsync(
            commandService.SetDisplayInvalidTextAsync(DefaultSettings.InvalidText, cancellationToken),
            "DISPLAY INVALID");
        await EnsureSucceededAsync(
            commandService.SetDisplayLockedTextAsync(DefaultSettings.LockedText, cancellationToken),
            "DISPLAY LOCKED");
        await EnsureSucceededAsync(commandService.SaveConfigurationAsync(cancellationToken), "CONFIG SAVE");

        await RefreshConfigurationOrThrowAsync(cancellationToken);
        _ = TryRefreshSnapshotAsync();
        return BuildSnapshot(sessionService.CurrentSession);
    }

    private static MonitorSnapshot BuildSnapshot(DeviceControllerSession? session)
    {
        if (session is null)
        {
            return new MonitorSnapshot(
                string.Empty,
                false,
                new MonitorEditableSettings());
        }

        var configuration = session.Configuration;
        return new MonitorSnapshot(
            session.Snapshot.DisplayText,
            session.Snapshot.DisplayForced,
            new MonitorEditableSettings(
                configuration.DisplayForceEnabled,
                configuration.DisplayForcedText,
                configuration.DisplayDefaultText,
                configuration.DisplayAllowedText,
                configuration.DisplayBlockedText,
                configuration.DisplayInvalidText,
                configuration.DisplayLockedText));
    }

    private static MonitorEditableSettings ValidateAndNormalize(MonitorEditableSettings settings)
    {
        return new MonitorEditableSettings(
            settings.ForceEnabled,
            ValidateDisplayText(settings.ForcedText, "Forced text"),
            ValidateDisplayText(settings.DefaultText, "Default text"),
            ValidateDisplayText(settings.AllowedText, "Allowed text"),
            ValidateDisplayText(settings.BlockedText, "Blocked text"),
            ValidateDisplayText(settings.InvalidText, "Invalid text"),
            ValidateDisplayText(settings.LockedText, "Locked text"));
    }

    private static string ValidateDisplayText(string value, string fieldName)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{fieldName} cannot be empty.");
        }

        if (trimmed.Length > 16)
        {
            throw new InvalidOperationException($"{fieldName} must be 16 characters or fewer.");
        }

        if (trimmed.Any(character => character < 32 || character > 126 || character == '|'))
        {
            throw new InvalidOperationException($"{fieldName} must use printable ASCII only and cannot contain '|'.");
        }

        return trimmed;
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

    private async Task RefreshConfigurationOrThrowAsync(CancellationToken cancellationToken)
    {
        var configuration = await sessionService.RefreshConfigurationAsync(cancellationToken);
        if (configuration is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Monitor configuration was saved, but the controller configuration could not be refreshed.");
    }

    private async Task TryRefreshSnapshotAsync()
    {
        try
        {
            await sessionService.RefreshSnapshotAsync();
        }
        catch
        {
            // Keep the confirmed configuration state; the next background refresh can recover the live display state.
        }
    }
}