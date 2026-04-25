namespace SmartParkingSystem.Domain.Models.Parking;

public sealed record ParkingSlotSnapshot(
    string Id,
    string Label,
    ParkingSlotState State,
    TimeSpan? OccupiedDuration);