namespace SmartParkingSystem.Maui.Services.Settings.Preferences;

public interface ISettingsPreferencesService
{
    bool EditParkingEnabled { get; set; }
    bool CameraAutoSnapshotEnabled { get; set; }
    int CameraAutoSnapshotDelayMs { get; set; }
    bool KeepCameraEnabledOutsideGate { get; set; }
    bool CameraAiAccessScanEnabled { get; set; }
    bool CameraAiAllowUnknownVehicles { get; set; }
    bool CameraAiCaptureMissingRfidDescriptionsEnabled { get; set; }
    bool OpenAiUsageEnabled { get; set; }
    string CameraAiApiKey { get; set; }
    string CameraAiLastStatus { get; set; }
    string CameraLcdUnavailableText { get; set; }
    string CameraLcdUnrecognizedText { get; set; }
    string CameraLcdAiUnavailableText { get; set; }
    string CameraLcdUnknownDeniedText { get; set; }
    string CameraLcdAllowedText { get; set; }
    bool BackendSyncEnabled { get; set; }
    string BackendBaseUrl { get; set; }
    event Action? PreferencesChanged;
    bool TryGetParkingSlotPosition(string slotId, out double leftPercent, out double topPercent);
    void SetParkingSlotPosition(string slotId, double leftPercent, double topPercent);
    int GetParkingSlotFloor(string slotId);
    void SetParkingSlotFloor(string slotId, int floor);
}
