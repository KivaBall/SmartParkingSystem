namespace SmartParkingSystem.Domain.Models.Monitor;

public sealed record MonitorSnapshot(
    string CurrentText,
    string CurrentDetailText,
    bool ForceEnabled,
    MonitorEditableSettings EditableSettings);
