namespace SmartParkingSystem.Domain.Models.Localization;

public sealed record GateTexts(
    string GateTitle,
    string GateSubtitle,
    string TimeRemainingLabel,
    string OpenStateLabel,
    string ClosedStateLabel,
    string LockedStateLabel,
    string ForceOpenButton,
    string OpenTemporarilyButton,
    string CloseButton,
    string LockButton,
    string UnlockButton,
    string SecondsUnit);