namespace SmartParkingSystem.Models.DeviceConnection;

public sealed record DeviceSlotSnapshot(
    int SlotNumber,
    string State,
    bool Enabled,
    int DistanceCm,
    long OccupiedMs);