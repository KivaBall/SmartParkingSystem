using SmartParkingSystem.Domain.Models.Camera;

namespace SmartParkingSystem.Maui.Services.Camera;

public interface ICameraSnapshotService
{
    IReadOnlyList<CameraSnapshot> GetSnapshots();
    Task<CameraSnapshot> SaveSnapshotAsync(string imageDataUrl, CancellationToken cancellationToken = default);
}