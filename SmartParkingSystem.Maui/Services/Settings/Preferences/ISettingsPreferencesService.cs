namespace SmartParkingSystem.Maui.Services.Settings.Preferences;

public interface ISettingsPreferencesService
{
    bool EditParkingEnabled { get; set; }
    bool CameraAutoSnapshotEnabled { get; set; }
    int CameraAutoSnapshotDelayMs { get; set; }
    bool KeepCameraEnabledOutsideGate { get; set; }
    event Action? PreferencesChanged;
    bool TryGetParkingSlotPosition(string slotId, out double leftPercent, out double topPercent);
    void SetParkingSlotPosition(string slotId, double leftPercent, double topPercent);
    int GetParkingSlotFloor(string slotId);
    void SetParkingSlotFloor(string slotId, int floor);
}