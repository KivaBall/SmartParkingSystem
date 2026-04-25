using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Execution;
using SmartParkingSystem.Maui.Services.DeviceConnection.Protocol;
using SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Telemetry;

public sealed class DeviceTelemetryService(
    IDeviceTransportService transportService,
    IDeviceProtocolExecutionService protocolExecutionService) : IDeviceTelemetryService
{
    private const int ProtocolWindowMs = 2500;
    private static readonly TimeSpan ProtocolDrainWindow = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ProtocolReadTimeout = TimeSpan.FromMilliseconds(300);

    public async Task<DeviceControllerProfile?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        return await protocolExecutionService.RunExclusiveAsync(
            async token =>
            {
                await transportService.DrainAsync(ProtocolDrainWindow, token);
                await transportService.SendLineAsync("GET PROFILE", token);

                var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(ProtocolWindowMs);
                while (DateTimeOffset.UtcNow < timeoutAt)
                {
                    var line = await transportService.ReadLineAsync(ProtocolReadTimeout, token);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var profile = DeviceProtocolParser.ParseProfile(line);
                    if (profile is not null)
                    {
                        return profile;
                    }
                }

                return null;
            },
            cancellationToken);
    }

    public async Task<DeviceControllerConfiguration?> GetConfigurationAsync(
        int slotCount,
        CancellationToken cancellationToken = default)
    {
        return await protocolExecutionService.RunExclusiveAsync(
            async token =>
            {
                await transportService.DrainAsync(ProtocolDrainWindow, token);
                await transportService.SendLineAsync("GET CONFIG", token);

                string? configLine = null;
                var slotEnabled = new Dictionary<int, bool>();
                var displayTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                (bool ForceEnabled, string ForcedText)? displayConfig = null;
                List<string>? allowedCards = null;
                List<string>? blockedCards = null;

                var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(ProtocolWindowMs);
                while (DateTimeOffset.UtcNow < timeoutAt)
                {
                    var line = await transportService.ReadLineAsync(ProtocolReadTimeout, token);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.StartsWith("CONFIG|", StringComparison.OrdinalIgnoreCase))
                    {
                        configLine = line;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseSlotConfig(line, out var slotNumber, out var isEnabled))
                    {
                        slotEnabled[slotNumber] = isEnabled;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseDisplayConfig(
                            line,
                            out var displayForceEnabled,
                            out var displayForcedText))
                    {
                        displayConfig = (displayForceEnabled, displayForcedText);
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseDisplayText(line, out var displayKey, out var displayValue))
                    {
                        displayTexts[displayKey] = displayValue;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseCards(line, "CARDS_ALLOWED", out var parsedAllowed))
                    {
                        allowedCards = parsedAllowed;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseCards(line, "CARDS_BLOCKED", out var parsedBlocked))
                    {
                        blockedCards = parsedBlocked;
                    }

                    var configuration = DeviceProtocolParser.TryBuildConfiguration(
                        configLine,
                        slotCount,
                        slotEnabled,
                        displayTexts,
                        displayConfig is not null,
                        displayConfig?.ForceEnabled ?? false,
                        displayConfig?.ForcedText ?? string.Empty,
                        allowedCards,
                        blockedCards);
                    if (configuration is not null)
                    {
                        return configuration;
                    }
                }

                return null;
            },
            cancellationToken);
    }

    public async Task<DeviceControllerSnapshot?> GetSnapshotAsync(
        int slotCount,
        CancellationToken cancellationToken = default)
    {
        return await protocolExecutionService.RunExclusiveAsync(
            async token =>
            {
                await transportService.DrainAsync(ProtocolDrainWindow, token);
                await transportService.SendLineAsync("GET SNAPSHOT", token);

                string? snapshotLine = null;
                (string Text, bool Forced)? displayState = null;
                var slots = new Dictionary<int, DeviceSlotSnapshot>();
                (int Allowed, int Blocked)? counts = null;

                var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(ProtocolWindowMs);
                while (DateTimeOffset.UtcNow < timeoutAt)
                {
                    var line = await transportService.ReadLineAsync(ProtocolReadTimeout, token);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.StartsWith("SNAPSHOT|", StringComparison.OrdinalIgnoreCase))
                    {
                        snapshotLine = line;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseDisplayState(line, out var displayText, out var displayForced))
                    {
                        displayState = (displayText, displayForced);
                        continue;
                    }

                    var slot = DeviceProtocolParser.ParseSlot(line);
                    if (slot is not null)
                    {
                        slots[slot.SlotNumber] = slot;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseCounts(line, out var allowedCount, out var blockedCount))
                    {
                        counts = (allowedCount, blockedCount);
                    }

                    var snapshot = DeviceProtocolParser.TryBuildSnapshot(
                        snapshotLine,
                        slotCount,
                        displayState is not null,
                        displayState?.Text ?? string.Empty,
                        displayState?.Forced ?? false,
                        slots,
                        counts is not null,
                        counts?.Allowed ?? 0,
                        counts?.Blocked ?? 0);
                    if (snapshot is not null)
                    {
                        return snapshot;
                    }
                }

                return null;
            },
            cancellationToken);
    }
}