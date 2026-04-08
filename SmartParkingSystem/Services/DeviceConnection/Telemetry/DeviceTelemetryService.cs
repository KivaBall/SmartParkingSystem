using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Services.DeviceConnection.Execution;
using SmartParkingSystem.Services.DeviceConnection.Protocol;
using SmartParkingSystem.Services.DeviceConnection.Transport;

namespace SmartParkingSystem.Services.DeviceConnection.Telemetry;

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
                var hasDisplayConfig = false;
                var displayForceEnabled = false;
                var displayForcedText = string.Empty;
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
                            out displayForceEnabled,
                            out displayForcedText))
                    {
                        hasDisplayConfig = true;
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
                        hasDisplayConfig,
                        displayForceEnabled,
                        displayForcedText,
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
                var displayText = string.Empty;
                var displayForced = false;
                var hasDisplayState = false;
                var slots = new Dictionary<int, DeviceSlotSnapshot>();
                var allowedCount = 0;
                var blockedCount = 0;
                var hasCounts = false;

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

                    if (DeviceProtocolParser.TryParseDisplayState(line, out displayText, out displayForced))
                    {
                        hasDisplayState = true;
                        continue;
                    }

                    var slot = DeviceProtocolParser.ParseSlot(line);
                    if (slot is not null)
                    {
                        slots[slot.SlotNumber] = slot;
                        continue;
                    }

                    if (DeviceProtocolParser.TryParseCounts(line, out allowedCount, out blockedCount))
                    {
                        hasCounts = true;
                    }

                    var snapshot = DeviceProtocolParser.TryBuildSnapshot(
                        snapshotLine,
                        slotCount,
                        hasDisplayState,
                        displayText,
                        displayForced,
                        slots,
                        hasCounts,
                        allowedCount,
                        blockedCount);
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