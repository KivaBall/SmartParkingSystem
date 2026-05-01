namespace SmartParkingSystem.Domain.Models.Admin;

public sealed class AdminEditableSettings
{
    public AdminEditableSettings()
    {
    }

    public AdminEditableSettings(
        int servoOpenAngle,
        int servoClosedAngle,
        int servoOpenDurationMs,
        bool forceGateOpen,
        bool forceGateLock,
        bool autoExitOpenEnabled,
        bool autoCloseAfterPassEnabled,
        int gatePassageThresholdCm,
        int occupiedThresholdCm,
        IReadOnlyList<bool> parkingSpotEnabledStates,
        int parkingStatusUpdateIntervalMs,
        string allowedCardsText,
        string blockedCardsText)
    {
        ServoOpenAngle = servoOpenAngle;
        ServoClosedAngle = servoClosedAngle;
        ServoOpenDurationMs = servoOpenDurationMs;
        ForceGateOpen = forceGateOpen;
        ForceGateLock = forceGateLock;
        AutoExitOpenEnabled = autoExitOpenEnabled;
        AutoCloseAfterPassEnabled = autoCloseAfterPassEnabled;
        GatePassageThresholdCm = gatePassageThresholdCm;
        OccupiedThresholdCm = occupiedThresholdCm;
        ParkingSpotEnabledStates = [.. parkingSpotEnabledStates];
        ParkingStatusUpdateIntervalMs = parkingStatusUpdateIntervalMs;
        AllowedCardsText = allowedCardsText;
        BlockedCardsText = blockedCardsText;
    }

    public int ServoOpenAngle { get; set; }
    public int ServoClosedAngle { get; set; }
    public int ServoOpenDurationMs { get; set; }
    public bool ForceGateOpen { get; set; }
    public bool ForceGateLock { get; set; }
    public bool AutoExitOpenEnabled { get; set; }
    public bool AutoCloseAfterPassEnabled { get; set; }
    public int GatePassageThresholdCm { get; set; }
    public int OccupiedThresholdCm { get; set; }
    public List<bool> ParkingSpotEnabledStates { get; set; } = [];
    public int ParkingStatusUpdateIntervalMs { get; set; }
    public string AllowedCardsText { get; set; } = string.Empty;
    public string BlockedCardsText { get; set; } = string.Empty;

    public AdminEditableSettings Clone()
    {
        return new AdminEditableSettings(
            ServoOpenAngle,
            ServoClosedAngle,
            ServoOpenDurationMs,
            ForceGateOpen,
            ForceGateLock,
            AutoExitOpenEnabled,
            AutoCloseAfterPassEnabled,
            GatePassageThresholdCm,
            OccupiedThresholdCm,
            ParkingSpotEnabledStates,
            ParkingStatusUpdateIntervalMs,
            AllowedCardsText,
            BlockedCardsText);
    }
}
