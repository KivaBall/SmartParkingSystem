namespace SmartParkingSystem.Domain.Models.Parking;

public sealed record SmartParkingCardProfile(
    string CardUid,
    int VisitCount,
    double AverageParkingDurationMinutes,
    string? LastKnownSlotId,
    string? VehicleDescription = null,
    DateTimeOffset? DescriptionCreatedAt = null,
    string? DescriptionSource = null,
    bool IsGeneratedFakeUid = false,
    string? LastAiResult = null);
