namespace SmartParkingSystem.Domain.Models.Monitor;

public sealed record MonitorSnapshot(
    string CurrentText,
    bool ForceEnabled,
    MonitorEditableSettings EditableSettings);