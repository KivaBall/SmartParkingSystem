using SmartParkingSystem.Domain.Models.DeviceConnection;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Protocol;

internal static class DeviceProtocolParser
{
    public static bool IsHelloOk(string line)
    {
        return line.StartsWith("HELLO_OK|", StringComparison.OrdinalIgnoreCase)
               && line.Contains("device=SMART_PARKING", StringComparison.OrdinalIgnoreCase);
    }

    public static DeviceControllerProfile? ParseProfile(string line)
    {
        if (!line.StartsWith("PROFILE|", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var values = ParseKeyValueSegments(line);
        return !TryGetRequiredString(values, "board", out var board)
               || !TryGetRequiredString(values, "rfid", out var rfid)
               || !TryGetRequiredString(values, "lcd", out var lcd)
               || !TryGetRequiredString(values, "gate", out var gate)
               || !TryGetRequiredString(values, "transport", out var transport)
               || !TryGetRequiredInt(values, "slots", out var slots)
            ? null
            : new DeviceControllerProfile(board, rfid, lcd, gate, transport, slots);
    }

    private static DeviceControllerConfiguration? ParseConfiguration(
        string configLine,
        IReadOnlyDictionary<int, bool> slotEnabled,
        IReadOnlyDictionary<string, string> displayTexts,
        bool displayForceEnabled,
        string displayForcedText,
        IReadOnlyList<string> allowedCards,
        IReadOnlyList<string> blockedCards)
    {
        var values = ParseKeyValueSegments(configLine);

        return !TryGetRequiredInt(values, "open_angle", out var openAngle)
               || !TryGetRequiredInt(values, "closed_angle", out var closedAngle)
               || !TryGetRequiredInt(values, "open_duration_ms", out var openDurationMs)
               || !TryGetRequiredInt(values, "threshold_cm", out var thresholdCm)
               || !TryGetRequiredInt(values, "telemetry_ms", out var telemetryMs)
               || !TryGetRequiredBool(values, "force_open", out var forceOpen)
               || !TryGetRequiredBool(values, "force_lock", out var forceLock)
            ? null
            : new DeviceControllerConfiguration(
                openAngle,
                closedAngle,
                openDurationMs,
                thresholdCm,
                telemetryMs,
                forceOpen,
                forceLock,
                Enumerable.Range(1, slotEnabled.Count)
                    .Select(index => slotEnabled.GetValueOrDefault(index))
                    .ToArray(),
                displayForceEnabled,
                displayForcedText,
                GetDisplayText(displayTexts, "DEFAULT"),
                GetDisplayText(displayTexts, "ALLOWED"),
                GetDisplayText(displayTexts, "BLOCKED"),
                GetDisplayText(displayTexts, "INVALID"),
                GetDisplayText(displayTexts, "LOCKED"),
                allowedCards,
                blockedCards);
    }

    public static DeviceControllerConfiguration? TryBuildConfiguration(
        string? configLine,
        int slotCount,
        IReadOnlyDictionary<int, bool> slotEnabled,
        IReadOnlyDictionary<string, string> displayTexts,
        bool hasDisplayConfig,
        bool displayForceEnabled,
        string displayForcedText,
        IReadOnlyList<string>? allowedCards,
        IReadOnlyList<string>? blockedCards)
    {
        if (configLine is null
            || slotEnabled.Count < slotCount
            || !hasDisplayConfig
            || !HasRequiredDisplayTexts(displayTexts)
            || allowedCards is null
            || blockedCards is null)
        {
            return null;
        }

        return ParseConfiguration(
            configLine,
            slotEnabled,
            displayTexts,
            displayForceEnabled,
            displayForcedText,
            allowedCards,
            blockedCards);
    }

    private static DeviceControllerSnapshot? ParseSnapshot(
        string snapshotLine,
        string displayText,
        bool displayForced,
        IReadOnlyList<DeviceSlotSnapshot> slots,
        int allowedCount,
        int blockedCount)
    {
        var values = ParseKeyValueSegments(snapshotLine);

        return !TryGetRequiredString(values, "mode", out var mode)
               || !TryGetRequiredInt(values, "remaining_ms", out var remainingMs)
               || !TryGetRequiredBool(values, "locked", out var locked)
               || !TryGetRequiredBool(values, "force_open", out var forceOpen)
               || !TryGetRequiredInt(values, "open_angle", out var openAngle)
               || !TryGetRequiredInt(values, "closed_angle", out var closedAngle)
               || !TryGetRequiredInt(values, "open_duration_ms", out var openDurationMs)
               || !TryGetRequiredInt(values, "threshold_cm", out var thresholdCm)
               || !TryGetRequiredInt(values, "telemetry_ms", out var telemetryMs)
            ? null
            : new DeviceControllerSnapshot(
                mode,
                remainingMs,
                locked,
                forceOpen,
                openAngle,
                closedAngle,
                openDurationMs,
                thresholdCm,
                telemetryMs,
                displayText,
                displayForced,
                slots,
                allowedCount,
                blockedCount);
    }

    public static DeviceControllerSnapshot? TryBuildSnapshot(
        string? snapshotLine,
        int slotCount,
        bool hasDisplayState,
        string displayText,
        bool displayForced,
        IReadOnlyDictionary<int, DeviceSlotSnapshot> slots,
        bool hasCounts,
        int allowedCount,
        int blockedCount)
    {
        if (snapshotLine is null || !hasDisplayState || slots.Count < slotCount || !hasCounts)
        {
            return null;
        }

        var orderedSlots = slots
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .ToArray();

        return ParseSnapshot(
            snapshotLine,
            displayText,
            displayForced,
            orderedSlots,
            allowedCount,
            blockedCount);
    }

    public static DeviceSlotSnapshot? ParseSlot(string line)
    {
        if (!line.StartsWith("SLOT|", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 || !int.TryParse(segments[1], out var slotNumber))
        {
            return null;
        }

        var values = ParseKeyValueSegments(segments.Skip(2));
        return !TryGetRequiredString(values, "state", out var state)
               || !TryGetRequiredBool(values, "enabled", out var enabled)
               || !TryGetRequiredInt(values, "distance_cm", out var distanceCm)
               || !TryGetRequiredLong(values, "occupied_ms", out var occupiedMs)
            ? null
            : new DeviceSlotSnapshot(slotNumber, state, enabled, distanceCm, occupiedMs);
    }

    public static bool TryParseSlotConfig(string line, out int slotNumber, out bool isEnabled)
    {
        slotNumber = 0;
        isEnabled = false;

        if (!line.StartsWith("SLOTCFG|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 || !int.TryParse(segments[1], out slotNumber))
        {
            return false;
        }

        var values = ParseKeyValueSegments(segments.Skip(2));
        return TryGetRequiredBool(values, "enabled", out isEnabled);
    }

    public static bool TryParseCards(string line, string expectedPrefix, out List<string> cards)
    {
        cards = [];
        if (!line.StartsWith(expectedPrefix + "|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 2; i < segments.Length; i++)
        {
            cards.Add(segments[i]);
        }

        return true;
    }

    public static bool TryParseCounts(string line, out int allowedCount, out int blockedCount)
    {
        allowedCount = 0;
        blockedCount = 0;

        if (!line.StartsWith("COUNTS|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = ParseKeyValueSegments(line);
        return TryGetRequiredInt(values, "allowed", out allowedCount)
               && TryGetRequiredInt(values, "blocked", out blockedCount);
    }

    public static bool TryParseDisplayConfig(
        string line,
        out bool forceEnabled,
        out string forcedText)
    {
        forceEnabled = false;
        forcedText = string.Empty;

        if (!line.StartsWith("DISPLAYCFG|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = ParseKeyValueSegments(line);
        return TryGetRequiredBool(values, "force", out forceEnabled)
               && TryGetRequiredString(values, "forced_text", out forcedText);
    }

    public static bool TryParseDisplayText(
        string line,
        out string key,
        out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (!line.StartsWith("DISPLAYTEXT|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = ParseKeyValueSegments(line);
        key = GetString(values, "key").ToUpperInvariant();
        value = GetString(values, "value");
        return !string.IsNullOrWhiteSpace(key);
    }

    public static bool TryParseDisplayState(
        string line,
        out string text,
        out bool forced)
    {
        text = string.Empty;
        forced = false;

        if (!line.StartsWith("DISPLAY|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = ParseKeyValueSegments(line);
        return TryGetRequiredString(values, "text", out text)
               && TryGetRequiredBool(values, "forced", out forced);
    }

    private static Dictionary<string, string> ParseKeyValueSegments(string line)
    {
        var segments = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ParseKeyValueSegments(segments.Skip(1));
    }

    private static Dictionary<string, string> ParseKeyValueSegments(IEnumerable<string> segments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex];
            var value = segment[(separatorIndex + 1)..];
            values[key] = value;
        }

        return values;
    }

    private static string GetString(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static bool TryGetRequiredString(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!values.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = rawValue;
        return true;
    }

    private static bool TryGetRequiredInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out value);
    }

    private static bool TryGetRequiredLong(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long value)
    {
        value = 0L;
        return values.TryGetValue(key, out var rawValue) && long.TryParse(rawValue, out value);
    }

    private static bool TryGetRequiredBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out var rawValue))
        {
            return false;
        }

        return rawValue switch
        {
            "1" => (value = true) || true,
            "0" => true,
            _ => false
        };
    }

    private static string GetDisplayText(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static bool HasRequiredDisplayTexts(IReadOnlyDictionary<string, string> displayTexts)
    {
        return displayTexts.ContainsKey("DEFAULT")
               && displayTexts.ContainsKey("ALLOWED")
               && displayTexts.ContainsKey("BLOCKED")
               && displayTexts.ContainsKey("INVALID")
               && displayTexts.ContainsKey("LOCKED");
    }
}