namespace SmartParkingSystem.Domain.Models.DeviceConnection;

public sealed record DeviceControllerSnapshot(
    string Mode,
    int RemainingMs,
    bool Locked,
    bool ForceOpen,
    int OpenAngle,
    int ClosedAngle,
    int OpenDurationMs,
    int ThresholdCm,
    int TelemetryIntervalMs,
    string DisplayText,
    bool DisplayForced,
    IReadOnlyList<DeviceSlotSnapshot> Slots,
    int AllowedCount,
    int BlockedCount);