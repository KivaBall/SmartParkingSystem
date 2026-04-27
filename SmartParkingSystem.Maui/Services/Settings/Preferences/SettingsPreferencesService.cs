using System.Globalization;

namespace SmartParkingSystem.Maui.Services.Settings.Preferences;

public sealed class SettingsPreferencesService : ISettingsPreferencesService
{
    private const int DefaultParkingSlotFloor = 1;
    private const int MaxParkingSlotFloor = 2;
    private const int MinParkingSlotFloor = 1;
    private const string EditParkingEnabledKey = "workspace.edit-parking-enabled";
    public event Action? PreferencesChanged;

    public bool EditParkingEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(EditParkingEnabledKey, false);
        set
        {
            var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(EditParkingEnabledKey, false);
            if (currentValue == value)
            {
                return;
            }

            Microsoft.Maui.Storage.Preferences.Default.Set(EditParkingEnabledKey, value);
            PreferencesChanged?.Invoke();
        }
    }

    public bool TryGetParkingSlotPosition(string slotId, out double leftPercent, out double topPercent)
    {
        leftPercent = 0;
        topPercent = 0;

        var serialized = Microsoft.Maui.Storage.Preferences.Default.Get(
            GetParkingSlotPositionKey(slotId),
            string.Empty);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return false;
        }

        var parts = serialized.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (double.TryParse(parts[0], CultureInfo.InvariantCulture, out leftPercent)
            && double.TryParse(parts[1], CultureInfo.InvariantCulture, out topPercent))
        {
            return true;
        }

        leftPercent = 0;
        topPercent = 0;
        return false;
    }

    public void SetParkingSlotPosition(string slotId, double leftPercent, double topPercent)
    {
        var serialized = FormattableString.Invariant($"{leftPercent:F1}|{topPercent:F1}");
        var key = GetParkingSlotPositionKey(slotId);
        var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(key, string.Empty);
        if (string.Equals(currentValue, serialized, StringComparison.Ordinal))
        {
            return;
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(key, serialized);
        PreferencesChanged?.Invoke();
    }

    public int GetParkingSlotFloor(string slotId)
    {
        var value = Microsoft.Maui.Storage.Preferences.Default.Get(
            GetParkingSlotFloorKey(slotId),
            DefaultParkingSlotFloor);

        return Math.Clamp(value, MinParkingSlotFloor, MaxParkingSlotFloor);
    }

    public void SetParkingSlotFloor(string slotId, int floor)
    {
        var normalizedFloor = Math.Clamp(floor, MinParkingSlotFloor, MaxParkingSlotFloor);
        var key = GetParkingSlotFloorKey(slotId);
        var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(key, DefaultParkingSlotFloor);
        if (currentValue == normalizedFloor)
        {
            return;
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(key, normalizedFloor);
        PreferencesChanged?.Invoke();
    }

    private static string GetParkingSlotPositionKey(string slotId)
    {
        return $"parking.slot-position.{slotId}";
    }

    private static string GetParkingSlotFloorKey(string slotId)
    {
        return $"parking.slot-floor.{slotId}";
    }
}
