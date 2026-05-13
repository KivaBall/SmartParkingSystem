using System.Globalization;
namespace SmartParkingSystem.Maui.Services.Settings.Preferences;

public sealed class SettingsPreferencesService : ISettingsPreferencesService
{
    private const int DefaultCameraAutoSnapshotDelayMs = 2000;
    private const int MaxCameraAutoSnapshotDelayMs = 30000;
    private const int MinCameraAutoSnapshotDelayMs = 250;
    private const int DefaultParkingSlotFloor = 1;
    private const int MaxParkingSlotFloor = 2;
    private const int MinParkingSlotFloor = 1;
    private const string AndroidBackendBaseUrl = "http://10.0.2.2:5000";
    private const string BackendBaseUrlKey = "backend.base-url";
    private const string BackendSyncEnabledKey = "backend.sync-enabled";
    private const string CameraAiAccessScanEnabledKey = "camera.ai-access.scan-enabled";
    private const string CameraAiAllowUnknownVehiclesKey = "camera.ai-access.allow-unknown-vehicles";
    private const string CameraAiApiKeyKey = "camera.ai-access.api-key";
    private const string CameraAiCaptureMissingRfidDescriptionsEnabledKey =
        "camera.ai-access.capture-missing-rfid-descriptions-enabled";
    private const string CameraAiLastStatusKey = "camera.ai-access.last-status";
    private const string CameraAutoSnapshotDelayMsKey = "camera.auto-snapshot-delay-ms";
    private const string CameraAutoSnapshotEnabledKey = "camera.auto-snapshot-enabled";
    private const string CameraLcdAiUnavailableTextKey = "camera.lcd.ai-unavailable-text";
    private const string CameraLcdAllowedTextKey = "camera.lcd.allowed-text";
    private const string CameraLcdUnavailableTextKey = "camera.lcd.unavailable-text";
    private const string CameraLcdUnknownDeniedTextKey = "camera.lcd.unknown-denied-text";
    private const string CameraLcdUnrecognizedTextKey = "camera.lcd.unrecognized-text";
    private const string DefaultCameraLcdAiUnavailableText = "AI Error";
    private const string DefaultCameraLcdAllowedText = "Camera Access";
    private const string DefaultCameraLcdUnavailableText = "Camera Error";
    private const string DefaultCameraLcdUnknownDeniedText = "Access Denied";
    private const string DefaultCameraLcdUnrecognizedText = "Unrecognized";
    private const string DefaultBackendBaseUrl = "http://127.0.0.1:5000";
    private const string EditParkingEnabledKey = "workspace.edit-parking-enabled";
    private const string KeepCameraEnabledOutsideGateKey = "camera.keep-enabled-outside-gate";
    private const string OpenAiUsageEnabledKey = "openai.usage-enabled";
    private const string WindowsBackendBaseUrl = "http://127.0.0.1:5000";
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

    public bool CameraAutoSnapshotEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(CameraAutoSnapshotEnabledKey, false);
        set => SetPreference(CameraAutoSnapshotEnabledKey, value, false);
    }

    public int CameraAutoSnapshotDelayMs
    {
        get
        {
            var value = Microsoft.Maui.Storage.Preferences.Default.Get(
                CameraAutoSnapshotDelayMsKey,
                DefaultCameraAutoSnapshotDelayMs);

            return Math.Clamp(value, MinCameraAutoSnapshotDelayMs, MaxCameraAutoSnapshotDelayMs);
        }
        set => SetPreference(
            CameraAutoSnapshotDelayMsKey,
            Math.Clamp(value, MinCameraAutoSnapshotDelayMs, MaxCameraAutoSnapshotDelayMs),
            DefaultCameraAutoSnapshotDelayMs);
    }

    public bool KeepCameraEnabledOutsideGate
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(KeepCameraEnabledOutsideGateKey, false);
        set => SetPreference(KeepCameraEnabledOutsideGateKey, value, false);
    }

    public bool CameraAiAccessScanEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(CameraAiAccessScanEnabledKey, false);
        set => SetPreference(CameraAiAccessScanEnabledKey, value, false);
    }

    public bool CameraAiAllowUnknownVehicles
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(CameraAiAllowUnknownVehiclesKey, false);
        set => SetPreference(CameraAiAllowUnknownVehiclesKey, value, false);
    }

    public bool CameraAiCaptureMissingRfidDescriptionsEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(
            CameraAiCaptureMissingRfidDescriptionsEnabledKey,
            false);
        set => SetPreference(CameraAiCaptureMissingRfidDescriptionsEnabledKey, value, false);
    }

    public bool OpenAiUsageEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(OpenAiUsageEnabledKey, false);
        set => SetPreference(OpenAiUsageEnabledKey, value, false);
    }

    public string CameraAiApiKey
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(CameraAiApiKeyKey, string.Empty).Trim();
        set => SetPreference(CameraAiApiKeyKey, value.Trim(), string.Empty);
    }

    public string CameraAiLastStatus
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(CameraAiLastStatusKey, string.Empty);
        set => SetPreference(CameraAiLastStatusKey, value.Trim(), string.Empty);
    }

    public string CameraLcdUnavailableText
    {
        get => GetDisplayTextPreference(CameraLcdUnavailableTextKey, DefaultCameraLcdUnavailableText);
        set => SetPreference(CameraLcdUnavailableTextKey, NormalizeDisplayText(value), DefaultCameraLcdUnavailableText);
    }

    public string CameraLcdUnrecognizedText
    {
        get => GetDisplayTextPreference(CameraLcdUnrecognizedTextKey, DefaultCameraLcdUnrecognizedText);
        set => SetPreference(CameraLcdUnrecognizedTextKey, NormalizeDisplayText(value), DefaultCameraLcdUnrecognizedText);
    }

    public string CameraLcdAiUnavailableText
    {
        get => GetDisplayTextPreference(CameraLcdAiUnavailableTextKey, DefaultCameraLcdAiUnavailableText);
        set => SetPreference(CameraLcdAiUnavailableTextKey, NormalizeDisplayText(value), DefaultCameraLcdAiUnavailableText);
    }

    public string CameraLcdUnknownDeniedText
    {
        get => GetDisplayTextPreference(CameraLcdUnknownDeniedTextKey, DefaultCameraLcdUnknownDeniedText);
        set => SetPreference(CameraLcdUnknownDeniedTextKey, NormalizeDisplayText(value), DefaultCameraLcdUnknownDeniedText);
    }

    public string CameraLcdAllowedText
    {
        get => GetDisplayTextPreference(CameraLcdAllowedTextKey, DefaultCameraLcdAllowedText);
        set => SetPreference(CameraLcdAllowedTextKey, NormalizeDisplayText(value), DefaultCameraLcdAllowedText);
    }

    public bool BackendSyncEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Default.Get(BackendSyncEnabledKey, true);
        set => SetPreference(BackendSyncEnabledKey, value, true);
    }

    public string BackendBaseUrl
    {
        get
        {
            var value = Microsoft.Maui.Storage.Preferences.Default.Get(BackendBaseUrlKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? GetDefaultBackendBaseUrl() : NormalizeBackendBaseUrl(value);
        }
        set => SetPreference(BackendBaseUrlKey, NormalizeBackendBaseUrl(value), GetDefaultBackendBaseUrl());
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

    private static string GetDefaultBackendBaseUrl()
    {
        return DeviceInfo.Current.Platform switch
        {
            var platform when platform == DevicePlatform.Android => AndroidBackendBaseUrl,
            var platform when platform == DevicePlatform.WinUI => WindowsBackendBaseUrl,
            _ => DefaultBackendBaseUrl
        };
    }

    private static string NormalizeBackendBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetDefaultBackendBaseUrl();
        }

        var normalizedValue = value.Trim();
        if (!normalizedValue.Contains("://", StringComparison.Ordinal))
        {
            normalizedValue = $"http://{normalizedValue}";
        }

        normalizedValue = normalizedValue.TrimEnd('/');
        return Uri.TryCreate(normalizedValue, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? normalizedValue
            : GetDefaultBackendBaseUrl();
    }

    private static string GetDisplayTextPreference(string key, string defaultValue)
    {
        return NormalizeDisplayText(Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue), defaultValue);
    }

    private static string NormalizeDisplayText(string? value, string defaultValue = "")
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        normalized = new string(normalized.Where(character => character is >= ' ' and <= '~' && character != '|').ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = defaultValue;
        }

        return normalized.Length <= 16 ? normalized : normalized[..16];
    }

    private void SetPreference(string key, bool value, bool defaultValue)
    {
        var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
        if (currentValue == value)
        {
            return;
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
        PreferencesChanged?.Invoke();
    }

    private void SetPreference(string key, int value, int defaultValue)
    {
        var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
        if (currentValue == value)
        {
            return;
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
        PreferencesChanged?.Invoke();
    }

    private void SetPreference(string key, string value, string defaultValue)
    {
        var currentValue = Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
        if (string.Equals(currentValue, value, StringComparison.Ordinal))
        {
            return;
        }

        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
        PreferencesChanged?.Invoke();
    }
}
