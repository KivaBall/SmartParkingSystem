using SmartParkingSystem.Domain.Models.Camera;

namespace SmartParkingSystem.Maui.Services.CameraAi;

public interface IVehicleRecognitionAiService
{
    Task<VehicleAiRecognitionResult> RecognizeAsync(
        string imageDataUrl,
        IReadOnlyList<VehicleAiKnownProfile> knownProfiles,
        VehicleAiRecognitionMode mode,
        CancellationToken cancellationToken = default);
}
