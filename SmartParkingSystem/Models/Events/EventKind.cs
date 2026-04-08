namespace SmartParkingSystem.Models.Events;

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
    MonitorForceModeChanged,
    MonitorTextChanged,
    MonitorTemplateChanged,
    ConnectionIntervalChanged,
    ParkingThresholdChanged,
    ParkingSlotChanged,
    ParkingSlotAvailabilityChanged,
    AllowedCardsChanged,
    BlockedCardsChanged
}