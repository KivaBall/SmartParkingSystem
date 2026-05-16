namespace SmartParkingSystem.Domain.Models.Parking;

public sealed record SlotOccupant(
    string CardUid,
    string? VehicleNumber,
    string? VehicleDescription,
    DateTimeOffset StartedAt,
    string? EntrySnapshotImageSource);
