namespace SmartParkingSystem.Domain.Models.Camera;

public sealed record VehicleAiRecognitionResult(
    bool Succeeded,
    VehicleAiRecognitionKind Kind,
    string? MatchedCardUid,
    string VehicleDescription,
    string Reason);
