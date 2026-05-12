using SmartParkingSystem.Domain.Models.Camera;

namespace SmartParkingSystem.Maui.Services.Camera;

public interface IEntryCameraService
{
    bool IsActive { get; }
    bool IsCapturing { get; }
    string LastFailureReason { get; }
    IReadOnlyList<CameraDeviceOption> Devices { get; }
    string? SelectedDeviceId { get; set; }
    event Action? StateChanged;

    Task InitializeAsync();
    Task RefreshDevicesAsync();
    Task StartAsync(string previewElementId);
    Task StopAsync(string previewElementId);
    Task AttachPreviewAsync(string previewElementId);
    Task DetachPreviewAsync(string previewElementId);
    Task<string?> CaptureFrameDataUrlAsync(int maxSide = 768, CancellationToken cancellationToken = default);
}
