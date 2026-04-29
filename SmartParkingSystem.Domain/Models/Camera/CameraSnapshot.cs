namespace SmartParkingSystem.Domain.Models.Camera;

public sealed record CameraSnapshot(
    string Id,
    string FilePath,
    string ImageSource,
    DateTimeOffset CreatedAtUtc);