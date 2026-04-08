namespace SmartParkingSystem.Models.DeviceConnection;

public sealed record DeviceControllerConfiguration(
    int OpenAngle,
    int ClosedAngle,
    int OpenDurationMs,
    int ThresholdCm,
    int TelemetryIntervalMs,
    bool ForceOpen,
    bool ForceLock,
    IReadOnlyList<bool> SlotEnabled,
    bool DisplayForceEnabled,
    string DisplayForcedText,
    string DisplayDefaultText,
    string DisplayAllowedText,
    string DisplayBlockedText,
    string DisplayInvalidText,
    string DisplayLockedText,
    IReadOnlyList<string> AllowedCards,
    IReadOnlyList<string> BlockedCards);