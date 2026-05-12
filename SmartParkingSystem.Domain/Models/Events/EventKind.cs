namespace SmartParkingSystem.Domain.Models.Events;

public enum EventKind
{
    ControllerConnected,
    ControllerDisconnected,
    GateStateChanged,
    GateForceOpenChanged,
    GateForceLockChanged,
    GateOpenAngleChanged,
    GateClosedAngleChanged,
    GateOpenDurationChanged,
    GateAutoExitOpenChanged,
    GateAutoCloseAfterPassChanged,
    GatePassageThresholdChanged,
    MonitorForceModeChanged,
    MonitorTextChanged,
    MonitorTemplateChanged,
    ConnectionIntervalChanged,
    ParkingThresholdChanged,
    ParkingSlotChanged,
    ParkingSlotAvailabilityChanged,
    CameraSnapshotCaptured,
    CameraAccessAttempt,
    AllowedCardsChanged,
    BlockedCardsChanged
}
