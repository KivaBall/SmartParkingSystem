using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Admin;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.Admin;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceAdminViewBase : ComponentBase, IDisposable
{
    [Inject]
    protected IAdminService? AdminService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Inject]
    protected ISettingsPreferencesService? SettingsPreferencesService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected AdminSnapshot Snapshot { get; set; } = new AdminSnapshot(
        new AdminEditableSettings(
            0,
            0,
            0,
            false,
            false,
            0,
            [],
            0,
            string.Empty,
            string.Empty));

    protected AdminEditableSettings EditableSettings { get; set; } = new AdminEditableSettings(
        0,
        0,
        0,
        false,
        false,
        0,
        [],
        0,
        string.Empty,
        string.Empty);

    protected bool IsLoading { get; private set; } = true;
    protected string StatusMessage { get; set; } = string.Empty;
    protected bool IsBusy { get; set; }
    protected AdminTexts Texts => RequireLocalizationService().GetAdminTexts();
    protected SettingsTexts SettingsTexts => RequireLocalizationService().GetSettingsTexts();
    protected bool HasChanges => IsDirty();

    protected bool EditParkingEnabled
    {
        get => RequireSettingsPreferencesService().EditParkingEnabled;
        set => RequireSettingsPreferencesService().EditParkingEnabled = value;
    }

    protected static string EditableClass => string.Empty;

    protected string ActionsClass => IsExiting
        ? "workspace-admin-actions animate-exit-bottom rounded-md bg-warm-100 p-6"
        : "workspace-admin-actions animate-page-enter-bottom rounded-md bg-warm-100 p-6 opacity-0";

    protected static string ActionsStyle => "animation-delay: 0ms;";
    protected static string EditableStyle => string.Empty;

    protected string GateSectionClass => IsExiting
        ? "rounded-md bg-brand-100/80 px-4 py-4 animate-exit-left"
        : "rounded-md bg-brand-100/80 px-4 py-4 animate-page-enter-left opacity-0";

    protected string GateSectionStyle => "animation-delay: 180ms;";

    protected string ParkingSectionClass => IsExiting
        ? "rounded-md bg-mint-100 px-4 py-4 animate-exit-right"
        : "rounded-md bg-mint-100 px-4 py-4 animate-page-enter-right opacity-0";

    protected string ParkingSectionStyle => IsExiting ? "animation-delay: 120ms;" : "animation-delay: 320ms;";

    protected string CardsSectionClass => IsExiting
        ? "rounded-md bg-white/85 px-4 py-4 animate-exit-left"
        : "rounded-md bg-white/85 px-4 py-4 animate-page-enter-left opacity-0";

    protected string CardsSectionStyle => IsExiting ? "animation-delay: 120ms;" : "animation-delay: 320ms;";

    protected string SystemSectionClass => IsExiting
        ? "rounded-md bg-warm-100 px-4 py-4 animate-exit-right"
        : "rounded-md bg-warm-100 px-4 py-4 animate-page-enter-right opacity-0";

    protected string SystemSectionStyle => IsExiting ? "animation-delay: 0ms;" : "animation-delay: 460ms;";

    public void Dispose()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        RequireSettingsPreferencesService().PreferencesChanged -= OnPreferencesChanged;
        GC.SuppressFinalize(this);
    }

    protected static string GetToggleButtonClass(bool isActive)
    {
        return isActive
            ? "inline-flex min-h-12 items-center justify-center rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200"
            : "inline-flex min-h-12 items-center justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-calm-700 transition-all duration-500 ease-out hover:bg-calm-50";
    }

    protected static string GetParkingToggleClass(bool isDisabled)
    {
        return isDisabled
            ? "inline-flex min-h-12 flex-col items-center justify-center gap-1 rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200"
            : "inline-flex min-h-12 flex-col items-center justify-center gap-1 rounded-md bg-mint-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-mint-200";
    }

    protected string GetToggleButtonLabel(bool isActive)
    {
        return isActive
            ? Texts.ToggleEnabledLabel
            : Texts.ToggleDisabledLabel;
    }

    protected string GetParkingSpotLabel(int slotNumber)
    {
        return slotNumber switch
        {
            1 => Texts.ParkingSpot1Label,
            2 => Texts.ParkingSpot2Label,
            3 => Texts.ParkingSpot3Label,
            4 => Texts.ParkingSpot4Label,
            5 => Texts.ParkingSpot5Label,
            6 => Texts.ParkingSpot6Label,
            _ when RequireLocalizationService().CurrentLanguage == AppLanguage.Ukrainian => $"Місце P{slotNumber}",
            _ => $"Parking spot {slotNumber}"
        };
    }

    protected override async Task OnInitializedAsync()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
        RequireSettingsPreferencesService().PreferencesChanged += OnPreferencesChanged;
        Snapshot = await RequireAdminService().GetSnapshotAsync();
        EditableSettings = Snapshot.EditableSettings.Clone();
        StatusMessage = Texts.RefreshSuccessMessage;
        IsLoading = false;
    }

    protected async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.RefreshingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireAdminService().GetSnapshotAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.RefreshSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task SaveAsync()
    {
        if (IsBusy || !HasChanges)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.SavingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireAdminService().SaveAsync(EditableSettings);
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.SaveSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task ResetAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.ResettingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireAdminService().ResetAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.ResetSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void ToggleForceGateLock()
    {
        EditableSettings.ForceGateLock = !EditableSettings.ForceGateLock;
        if (EditableSettings.ForceGateLock)
        {
            EditableSettings.ForceGateOpen = false;
        }
    }

    protected void ToggleForceGateOpen()
    {
        EditableSettings.ForceGateOpen = !EditableSettings.ForceGateOpen;
        if (EditableSettings.ForceGateOpen)
        {
            EditableSettings.ForceGateLock = false;
        }
    }

    protected void ToggleParkingSpot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EditableSettings.ParkingSpotEnabledStates.Count)
        {
            return;
        }

        EditableSettings.ParkingSpotEnabledStates[slotIndex] = !EditableSettings.ParkingSpotEnabledStates[slotIndex];
    }

    protected void ToggleEditParking()
    {
        EditParkingEnabled = !EditParkingEnabled;
    }

    private IAdminService RequireAdminService()
    {
        return AdminService ?? throw new InvalidOperationException("Admin service is not available.");
    }

    private bool IsDirty()
    {
        var originalSettings = Snapshot.EditableSettings;

        return EditableSettings.ServoOpenAngle != originalSettings.ServoOpenAngle
               || EditableSettings.ServoClosedAngle != originalSettings.ServoClosedAngle
               || EditableSettings.ServoOpenDurationMs != originalSettings.ServoOpenDurationMs
               || EditableSettings.ForceGateOpen != originalSettings.ForceGateOpen
               || EditableSettings.ForceGateLock != originalSettings.ForceGateLock
               || EditableSettings.OccupiedThresholdCm != originalSettings.OccupiedThresholdCm
               || !EditableSettings.ParkingSpotEnabledStates.SequenceEqual(originalSettings.ParkingSpotEnabledStates)
               || EditableSettings.ParkingStatusUpdateIntervalMs != originalSettings.ParkingStatusUpdateIntervalMs
               || EditableSettings.AllowedCardsText != originalSettings.AllowedCardsText
               || EditableSettings.BlockedCardsText != originalSettings.BlockedCardsText;
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private IDeviceSessionService RequireDeviceSessionService()
    {
        return DeviceSessionService ?? throw new InvalidOperationException("Device session service is not available.");
    }

    private ISettingsPreferencesService RequireSettingsPreferencesService()
    {
        return SettingsPreferencesService ??
               throw new InvalidOperationException("Settings preferences service is not available.");
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        if (IsBusy || HasChanges)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            Snapshot = await RequireAdminService().GetSnapshotAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.RefreshSuccessMessage;
            StateHasChanged();
        });
    }

    private void OnPreferencesChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
