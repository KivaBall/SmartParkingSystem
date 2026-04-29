using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Gate;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.Camera;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Gate;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceGateViewBase : ComponentBase, IAsyncDisposable
{
    private const string CameraPreviewElementId = "entry-camera-preview";
    private static readonly TimeSpan CameraAlertDuration = TimeSpan.FromSeconds(2);
    private bool _isCameraControlsAlertActive;
    private bool _isReloading;

    [Inject]
    protected IGateService? GateService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Inject]
    protected IEntryCameraService? EntryCameraService { get; set; }

    [Inject]
    protected ISettingsPreferencesService? SettingsPreferencesService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected GateSnapshot Snapshot { get; set; } = new GateSnapshot(GateMode.Closed, false, false, 0);
    protected bool IsLoading { get; private set; } = true;
    protected bool IsBusy { get; private set; }
    protected bool IsCameraActive => RequireEntryCameraService().IsActive;
    protected IReadOnlyList<CameraDeviceOption> CameraDevices => RequireEntryCameraService().Devices;

    protected string? SelectedCameraDeviceId
    {
        get => RequireEntryCameraService().SelectedDeviceId;
        set => RequireEntryCameraService().SelectedDeviceId = value;
    }

    protected GateTexts Texts => RequireLocalizationService().GetGateTexts();

    protected string GateVisualClass => IsExiting
        ? $"animate-exit-left rounded-md p-6 {VisualContainerClass}"
        : $"animate-page-enter-left rounded-md p-6 opacity-0 {VisualContainerClass}";

    protected string GateActionsClass => IsExiting
        ? "animate-exit-right rounded-md bg-warm-100 p-6"
        : "animate-page-enter-right rounded-md bg-warm-100 p-6 opacity-0";

    protected static string GateVisualStyle => "animation-delay: 0ms;";
    protected static string GateActionsStyle => "animation-delay: 120ms;";
    protected static string CameraPanelStyle => "animation-delay: 240ms;";

    protected string CameraPreviewStyle =>
        $"width: 100%; height: 100%; object-fit: cover; display: {(IsCameraActive ? "block" : "none")};";

    protected string CameraControlsStyle
    {
        get
        {
            var background = _isCameraControlsAlertActive
                ? "rgba(255, 92, 92, 0.92)"
                : IsCameraActive
                    ? "rgba(255, 255, 255, 0.5)"
                    : "rgba(255, 255, 255, 0.9)";

            var opacity = IsCameraActive && !_isCameraControlsAlertActive ? "0.5" : "1";

            return $"position: absolute; right: 1rem; bottom: 1rem; max-width: calc(100% - 2rem); background: {
                background}; opacity: {opacity};";
        }
    }

    protected string CountdownLabel => $"{Snapshot.RemainingSeconds} {Texts.SecondsUnit}";

    protected bool IsForceOpenDisabled => IsBusy || !Snapshot.CanForceOpen;
    protected bool IsTemporaryOpenDisabled => IsBusy || !Snapshot.CanOpenTemporarily;
    protected bool IsCloseDisabled => IsBusy || !Snapshot.CanClose;
    protected bool IsLockDisabled => IsBusy;
    protected bool IsCameraToggleDisabled => IsBusy || RequireEntryCameraService().IsCapturing;
    protected bool HasCameraDevices => CameraDevices.Count > 0;

    protected string VisualContainerClass => Snapshot.Mode switch
    {
        GateMode.ForcedOpen => "bg-brand-100/80",
        GateMode.TemporaryOpen => "bg-mint-100",
        GateMode.Locked => "bg-warm-100",
        _ => "bg-white/85"
    };

    protected string VisualAccentClass => Snapshot.Mode switch
    {
        GateMode.ForcedOpen => "text-calm-700",
        GateMode.TemporaryOpen => "text-mint-700",
        GateMode.Locked => "text-warm-700",
        _ => "text-calm-700"
    };

    protected string LockButtonLabel => Snapshot.IsLocked
        ? Texts.UnlockButton
        : Texts.LockButton;

    protected static string PrimaryActionButtonClass =>
        "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-brand-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-brand-400 disabled:cursor-default disabled:opacity-50";

    protected static string MintActionButtonClass =>
        "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-mint-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-mint-200 disabled:cursor-default disabled:opacity-50";

    protected static string NeutralActionButtonClass =>
        "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-white/85 px-4 py-3 text-sm font-semibold text-calm-700 transition-all duration-500 ease-out hover:bg-calm-100 disabled:cursor-default disabled:opacity-50";

    protected static string WarmActionButtonClass =>
        "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200 disabled:cursor-default disabled:opacity-50";

    protected string CameraPanelClass => IsExiting
        ? "animate-exit-bottom rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-bottom rounded-md bg-brand-100/80 p-6 opacity-0";

    protected static string CameraIconButtonClass =>
        "inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-white text-calm-700 transition-all duration-500 ease-out hover:bg-calm-100 disabled:cursor-default disabled:opacity-50";

    protected string CameraToggleIcon => IsCameraActive ? "video-off" : "video";

    public async ValueTask DisposeAsync()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        RequireEntryCameraService().StateChanged -= OnCameraStateChanged;
        if (RequireSettingsPreferencesService().KeepCameraEnabledOutsideGate)
        {
            await RequireEntryCameraService().DetachPreviewAsync(CameraPreviewElementId);
        }
        else
        {
            await RequireEntryCameraService().StopAsync(CameraPreviewElementId);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
        RequireEntryCameraService().StateChanged += OnCameraStateChanged;
        await ReloadSnapshotAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await RequireEntryCameraService().InitializeAsync();
        await RequireEntryCameraService().AttachPreviewAsync(CameraPreviewElementId);
    }

    protected async Task ForceOpenAsync()
    {
        await ExecuteGateActionAsync(cancellationToken => RequireGateService().ForceOpenAsync(cancellationToken));
    }

    protected async Task OpenTemporarilyAsync()
    {
        await ExecuteGateActionAsync(cancellationToken => RequireGateService().OpenTemporarilyAsync(cancellationToken));
    }

    protected async Task CloseAsync()
    {
        await ExecuteGateActionAsync(cancellationToken => RequireGateService().CloseAsync(cancellationToken));
    }

    protected async Task ToggleLockAsync()
    {
        await ExecuteGateActionAsync(cancellationToken => RequireGateService().ToggleLockAsync(cancellationToken));
    }

    protected async Task ToggleCameraAsync()
    {
        if (IsCameraToggleDisabled)
        {
            return;
        }

        if (IsCameraActive)
        {
            await RequireEntryCameraService().StopAsync(CameraPreviewElementId);
            await InvokeAsync(StateHasChanged);
            return;
        }

        await RequireEntryCameraService().StartAsync(CameraPreviewElementId);
        if (!IsCameraActive)
        {
            await FlashCameraControlsAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadSnapshotAsync()
    {
        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        try
        {
            Snapshot = await RequireGateService().GetSnapshotAsync();
            IsLoading = false;
        }
        finally
        {
            _isReloading = false;
        }
    }

    private async Task ExecuteGateActionAsync(
        Func<CancellationToken, Task<GateSnapshot>> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Snapshot = await action(CancellationToken.None);
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task RefreshCameraDevicesAsync()
    {
        await RequireEntryCameraService().RefreshDevicesAsync();
    }

    protected static string TruncateCameraDeviceLabel(string label)
    {
        const int maxLabelLength = 36;

        if (label.Length <= maxLabelLength)
        {
            return label;
        }

        return $"{label[..(maxLabelLength - 3)]}...";
    }

    private async Task FlashCameraControlsAsync()
    {
        _isCameraControlsAlertActive = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(CameraAlertDuration);
        _isCameraControlsAlertActive = false;
    }

    private IGateService RequireGateService()
    {
        return GateService ?? throw new InvalidOperationException("Gate service is not available.");
    }

    private IDeviceSessionService RequireDeviceSessionService()
    {
        return DeviceSessionService ?? throw new InvalidOperationException("Device session service is not available.");
    }

    private IEntryCameraService RequireEntryCameraService()
    {
        return EntryCameraService ?? throw new InvalidOperationException("Entry camera service is not available.");
    }

    private ISettingsPreferencesService RequireSettingsPreferencesService()
    {
        return SettingsPreferencesService ??
               throw new InvalidOperationException("Settings preferences service is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        if (IsBusy || _isReloading)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await ReloadSnapshotAsync();
            StateHasChanged();
        });
    }

    private void OnCameraStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }
}