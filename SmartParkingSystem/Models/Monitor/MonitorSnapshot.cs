namespace SmartParkingSystem.Models.Monitor;

public sealed record MonitorSnapshot(
    string CurrentText,
    bool ForceEnabled,
    MonitorEditableSettings EditableSettings);