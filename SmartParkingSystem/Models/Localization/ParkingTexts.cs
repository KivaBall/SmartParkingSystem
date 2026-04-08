namespace SmartParkingSystem.Models.Localization;

public sealed record ParkingTexts(
    string MapTitle,
    string SlotTitle,
    string FreeStateLabel,
    string OccupiedStateLabel,
    string DisabledStateLabel,
    string OccupiedDurationLabel,
    string NoOccupiedDurationLabel,
    string DisabledDurationLabel,
    string EnableSlotButton,
    string DisableSlotButton);