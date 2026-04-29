using Microsoft.JSInterop;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Events;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.Camera;

public sealed class EntryCameraService : IEntryCameraService, IDisposable
{
    private const string OpenMode = "TEMP_OPEN";
    private const string ForcedOpenMode = "FORCED_OPEN";
    private readonly ICameraSnapshotService _cameraSnapshotService;
    private readonly IEventsService _eventsService;
    private readonly IJSRuntime _jsRuntime;
    private readonly ISettingsPreferencesService _preferencesService;
    private readonly IDeviceSessionService _sessionService;
    private readonly Lock _sync = new Lock();
    private bool _isDisposed;
    private bool _keepCameraEnabledOutsideGate;
    private DeviceControllerSession? _previousSession;

    public EntryCameraService(
        IJSRuntime jsRuntime,
        IDeviceSessionService sessionService,
        ISettingsPreferencesService preferencesService,
        ICameraSnapshotService cameraSnapshotService,
        IEventsService eventsService)
    {
        _jsRuntime = jsRuntime;
        _sessionService = sessionService;
        _preferencesService = preferencesService;
        _cameraSnapshotService = cameraSnapshotService;
        _eventsService = eventsService;
        _previousSession = sessionService.CurrentSession;
        _keepCameraEnabledOutsideGate = preferencesService.KeepCameraEnabledOutsideGate;
        _sessionService.SessionChanged += OnSessionChanged;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _sessionService.SessionChanged -= OnSessionChanged;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        _isDisposed = true;
    }

    public bool IsActive { get; private set; }
    public bool IsCapturing { get; private set; }
    public IReadOnlyList<CameraDeviceOption> Devices { get; private set; } = [];
    public string? SelectedDeviceId { get; set; }
    public event Action? StateChanged;

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await _jsRuntime.InvokeAsync<CameraDeviceOption[]>("smartParkingCamera.listDevices");
            Devices = devices;

            if (Devices.Count > 0 &&
                (string.IsNullOrWhiteSpace(SelectedDeviceId) ||
                 Devices.All(device => device.Id != SelectedDeviceId)))
            {
                SelectedDeviceId = Devices[0].Id;
            }
        }
        catch
        {
            // Some platforms hide camera devices until permission is granted.
            Devices = [];
            SelectedDeviceId = null;
        }

        NotifyStateChanged();
    }

    public async Task StartAsync(string previewElementId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "smartParkingCamera.startWithDevice",
                previewElementId,
                string.IsNullOrWhiteSpace(SelectedDeviceId) ? null : SelectedDeviceId);
            IsActive = true;
            await RefreshDevicesAsync();
        }
        catch
        {
            // The gate view flashes the camera controls when start fails.
            IsActive = false;
            NotifyStateChanged();
        }
    }

    public async Task StopAsync(string previewElementId)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("smartParkingCamera.stop", previewElementId);
        }
        catch
        {
            // The WebView can already be unavailable while the app is closing.
        }

        IsActive = false;
        NotifyStateChanged();
    }

    public async Task AttachPreviewAsync(string previewElementId)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("smartParkingCamera.attach", previewElementId);
        }
        catch
        {
            // If the page remounted, the previous preview element can be gone already.
            IsActive = false;
        }

        NotifyStateChanged();
    }

    public async Task DetachPreviewAsync(string previewElementId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("smartParkingCamera.detach", previewElementId);
        }
        catch
        {
            // Preview detach is best-effort because the element can already be gone.
        }
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        var previous = _previousSession;
        _previousSession = session;
        var shouldCapture = ShouldCaptureAfterGateAction(previous, session);

        if (!shouldCapture)
        {
            return;
        }

        _ = CaptureAfterDelayAsync();
    }

    private void OnPreferencesChanged()
    {
        var keepCameraEnabledOutsideGate = _preferencesService.KeepCameraEnabledOutsideGate;
        var wasDisabled = _keepCameraEnabledOutsideGate && !keepCameraEnabledOutsideGate;
        _keepCameraEnabledOutsideGate = keepCameraEnabledOutsideGate;

        if (wasDisabled && IsActive)
        {
            _ = StopAsync(string.Empty);
        }
    }

    private async Task CaptureAfterDelayAsync()
    {
        lock (_sync)
        {
            if (IsCapturing)
            {
                return;
            }

            IsCapturing = true;
        }

        NotifyStateChanged();

        try
        {
            var delayMs = _preferencesService.CameraAutoSnapshotDelayMs;
            await Task.Delay(delayMs);
            if (!_preferencesService.CameraAutoSnapshotEnabled || !IsActive)
            {
                return;
            }

            var imageDataUrl = await _jsRuntime.InvokeAsync<string>("smartParkingCamera.capture", string.Empty);
            var snapshot = await _cameraSnapshotService.SaveSnapshotAsync(imageDataUrl);
            _eventsService.AddCameraSnapshotEvent(snapshot.FilePath);
        }
        catch
        {
            // Snapshot capture is opportunistic; the next gate opening can try again.
        }
        finally
        {
            IsCapturing = false;
            NotifyStateChanged();
        }
    }

    private bool ShouldCaptureAfterGateAction(
        DeviceControllerSession? previous,
        DeviceControllerSession? current)
    {
        return _preferencesService.CameraAutoSnapshotEnabled
               && IsActive
               && previous is not null
               && current is not null
               && !IsOpenMode(previous.Snapshot.Mode)
               && IsOpenMode(current.Snapshot.Mode);
    }

    private static bool IsOpenMode(string mode)
    {
        return string.Equals(mode, OpenMode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mode, ForcedOpenMode, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}