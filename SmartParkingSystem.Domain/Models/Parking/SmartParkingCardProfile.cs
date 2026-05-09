namespace SmartParkingSystem.Domain.Models.Parking;

public sealed record SmartParkingCardProfile(
    string CardUid,
    int VisitCount,
    double AverageParkingDurationMinutes,
    string? LastKnownSlotId);