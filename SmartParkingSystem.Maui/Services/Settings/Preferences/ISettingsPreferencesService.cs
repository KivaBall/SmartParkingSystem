namespace SmartParkingSystem.Maui.Services.Settings.Preferences;

public interface ISettingsPreferencesService
{
    bool EditParkingEnabled { get; set; }
    event Action? PreferencesChanged;
    bool TryGetParkingSlotPosition(string slotId, out double leftPercent, out double topPercent);
    void SetParkingSlotPosition(string slotId, double leftPercent, double topPercent);
}