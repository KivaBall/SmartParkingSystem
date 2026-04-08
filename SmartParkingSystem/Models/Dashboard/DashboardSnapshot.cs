using SmartParkingSystem.Models.Gate;

namespace SmartParkingSystem.Models.Dashboard;

public sealed record DashboardSnapshot(
    bool IsConnected,
    string TargetLabel,
    string BoardLabel,
    string TransportLabel,
    DateTimeOffset? ConnectedAt,
    int SlotCapacity,
    int FreeCount,
    int OccupiedCount,
    int DisabledCount,
    int AllowedCount,
    int BlockedCount,
    GateMode GateMode,
    int RemainingSeconds,
    bool IsForceOpen,
    bool IsForceLocked,
    int TelemetryIntervalMs,
    int ThresholdCm,
    int OpenDurationMs);