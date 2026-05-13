using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SmartParkingSystem.Domain.Models.Admin;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.Maui.Services.AppMemory;
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

    [Inject]
    protected IAppMemoryStore? AppMemoryStore { get; set; }

    [Inject]
    protected IJSRuntime? JsRuntime { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected AdminSnapshot Snapshot { get; set; } = new AdminSnapshot(
        new AdminEditableSettings(
            0,
            0,
            0,
            false,
            false,
            false,
            false,
            0,
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
        false,
        false,
        0,
        0,
        [],
        0,
        string.Empty,
        string.Empty);

    protected bool IsLoading { get; private set; } = true;
    protected string StatusMessage { get; set; } = string.Empty;
    protected bool IsBusy { get; set; }
    private bool NeedsIconRefresh { get; set; } = true;
    private bool HasLocalCardEditorChanges { get; set; }
    protected AdminTexts Texts => RequireLocalizationService().GetAdminTexts();
    protected SettingsTexts SettingsTexts => RequireLocalizationService().GetSettingsTexts();
    protected bool HasChanges => IsDirty();
    protected List<AdminCardEditorRow> CardRows { get; } = [];

    protected bool EditParkingEnabled
    {
        get => RequireSettingsPreferencesService().EditParkingEnabled;
        set => RequireSettingsPreferencesService().EditParkingEnabled = value;
    }

    protected bool CameraAutoSnapshotEnabled
    {
        get => RequireSettingsPreferencesService().CameraAutoSnapshotEnabled;
        set => RequireSettingsPreferencesService().CameraAutoSnapshotEnabled = value;
    }

    protected int CameraAutoSnapshotDelayMs
    {
        get => RequireSettingsPreferencesService().CameraAutoSnapshotDelayMs;
        set => RequireSettingsPreferencesService().CameraAutoSnapshotDelayMs = value;
    }

    protected bool KeepCameraEnabledOutsideGate
    {
        get => RequireSettingsPreferencesService().KeepCameraEnabledOutsideGate;
        set => RequireSettingsPreferencesService().KeepCameraEnabledOutsideGate = value;
    }

    protected bool CameraAiAccessScanEnabled
    {
        get => RequireSettingsPreferencesService().CameraAiAccessScanEnabled;
        set => RequireSettingsPreferencesService().CameraAiAccessScanEnabled = value;
    }

    protected bool CameraAiAllowUnknownVehicles
    {
        get => RequireSettingsPreferencesService().CameraAiAllowUnknownVehicles;
        set => RequireSettingsPreferencesService().CameraAiAllowUnknownVehicles = value;
    }

    protected bool CameraAiCaptureMissingRfidDescriptionsEnabled
    {
        get => RequireSettingsPreferencesService().CameraAiCaptureMissingRfidDescriptionsEnabled;
        set => RequireSettingsPreferencesService().CameraAiCaptureMissingRfidDescriptionsEnabled = value;
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

    protected string GateSectionStyle => IsExiting ? "animation-delay: 480ms;" : "animation-delay: 180ms;";

    protected string ParkingSectionClass => IsExiting
        ? "rounded-md bg-mint-100 px-4 py-4 animate-exit-right"
        : "rounded-md bg-mint-100 px-4 py-4 animate-page-enter-right opacity-0";

    protected string ParkingSectionStyle => IsExiting ? "animation-delay: 360ms;" : "animation-delay: 320ms;";

    protected string CardsSectionClass => IsExiting
        ? "rounded-md bg-white/85 px-4 py-4 animate-exit-left"
        : "rounded-md bg-white/85 px-4 py-4 animate-page-enter-left opacity-0";

    protected string CardsSectionStyle => IsExiting ? "animation-delay: 240ms;" : "animation-delay: 460ms;";

    protected string CameraSectionClass => IsExiting
        ? "rounded-md bg-brand-100/80 px-4 py-4 animate-exit-right"
        : "rounded-md bg-brand-100/80 px-4 py-4 animate-page-enter-right opacity-0";

    protected string CameraSectionStyle => IsExiting ? "animation-delay: 120ms;" : "animation-delay: 600ms;";

    protected string SystemSectionClass => IsExiting
        ? "rounded-md bg-warm-100 px-4 py-4 animate-exit-left"
        : "rounded-md bg-warm-100 px-4 py-4 animate-page-enter-left opacity-0";

    protected string SystemSectionStyle => IsExiting ? "animation-delay: 360ms;" : "animation-delay: 320ms;";

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

    protected static string GetCardTypeToggleClass(bool isBlocked)
    {
        return isBlocked
            ? "inline-flex min-h-10 items-center justify-center rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200"
            : "inline-flex min-h-10 items-center justify-center rounded-md bg-mint-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-mint-200";
    }

    protected string GetToggleButtonLabel(bool isActive)
    {
        return isActive
            ? Texts.ToggleEnabledLabel
            : Texts.ToggleDisabledLabel;
    }

    protected string GetCardTypeLabel(bool isBlocked)
    {
        if (RequireLocalizationService().CurrentLanguage == AppLanguage.Ukrainian)
        {
            return isBlocked ? "Заблоковано" : "Дозволено";
        }

        return isBlocked ? "Blocked" : "Allowed";
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
        SyncCardRowsFromSettings();
        StatusMessage = Texts.RefreshSuccessMessage;
        IsLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || NeedsIconRefresh)
        {
            NeedsIconRefresh = false;
            await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
        }
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
            SyncCardRowsFromSettings();
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
            SyncCardRowsFromSettings();
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
            SyncCardRowsFromSettings();
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

    protected void ToggleAutoExitOpen()
    {
        EditableSettings.AutoExitOpenEnabled = !EditableSettings.AutoExitOpenEnabled;
    }

    protected void ToggleAutoCloseAfterPass()
    {
        EditableSettings.AutoCloseAfterPassEnabled = !EditableSettings.AutoCloseAfterPassEnabled;
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

    protected void ToggleCameraAutoSnapshot()
    {
        CameraAutoSnapshotEnabled = !CameraAutoSnapshotEnabled;
    }

    protected void ToggleKeepCameraEnabledOutsideGate()
    {
        KeepCameraEnabledOutsideGate = !KeepCameraEnabledOutsideGate;
    }

    protected void ToggleCameraAiAccessScan()
    {
        CameraAiAccessScanEnabled = !CameraAiAccessScanEnabled;
    }

    protected void ToggleCameraAiAllowUnknownVehicles()
    {
        CameraAiAllowUnknownVehicles = !CameraAiAllowUnknownVehicles;
    }

    protected void ToggleCameraAiCaptureMissingRfidDescriptions()
    {
        CameraAiCaptureMissingRfidDescriptionsEnabled = !CameraAiCaptureMissingRfidDescriptionsEnabled;
    }

    protected void ClearVehicleDescription(string cardUid)
    {
        SaveVehicleDescription(cardUid, string.Empty);
    }

    protected void OnVehicleDescriptionChanged(string cardUid, ChangeEventArgs eventArgs)
    {
        SaveVehicleDescription(cardUid, eventArgs.Value?.ToString() ?? string.Empty);
    }

    protected void AddCardRow()
    {
        CardRows.Add(new AdminCardEditorRow(Guid.NewGuid(), string.Empty, string.Empty, string.Empty, false));
        HasLocalCardEditorChanges = true;
        NeedsIconRefresh = true;
    }

    protected void RemoveCardRow(Guid rowId)
    {
        var row = CardRows.FirstOrDefault(item => item.RowId == rowId);
        if (row is null)
        {
            return;
        }

        CardRows.Remove(row);
        HasLocalCardEditorChanges = true;
        RebuildCardsTextFromRows();
        NeedsIconRefresh = true;
    }

    protected void ToggleCardType(Guid rowId)
    {
        var row = CardRows.FirstOrDefault(item => item.RowId == rowId);
        if (row is null)
        {
            return;
        }

        row.IsBlocked = !row.IsBlocked;
        HasLocalCardEditorChanges = true;
        RebuildCardsTextFromRows();
    }

    protected void OnCardUidChanged(Guid rowId, ChangeEventArgs eventArgs)
    {
        var row = CardRows.FirstOrDefault(item => item.RowId == rowId);
        if (row is null)
        {
            return;
        }

        row.CardUid = FormatUidForEditing(eventArgs.Value?.ToString() ?? string.Empty);
        HasLocalCardEditorChanges = true;
        RebuildCardsTextFromRows();
        SaveVehicleProfile(row.CardUid, row.VehicleDescription, row.VehicleNumber);
    }

    protected void OnCardNumberChanged(Guid rowId, ChangeEventArgs eventArgs)
    {
        var row = CardRows.FirstOrDefault(item => item.RowId == rowId);
        if (row is null)
        {
            return;
        }

        row.VehicleNumber = NormalizeVehicleNumber(eventArgs.Value?.ToString());
        HasLocalCardEditorChanges = true;
        SaveVehicleProfile(row.CardUid, row.VehicleDescription, row.VehicleNumber);
    }

    protected void OnCardDescriptionChanged(Guid rowId, ChangeEventArgs eventArgs)
    {
        var row = CardRows.FirstOrDefault(item => item.RowId == rowId);
        if (row is null)
        {
            return;
        }

        row.VehicleDescription = eventArgs.Value?.ToString()?.Trim() ?? string.Empty;
        HasLocalCardEditorChanges = true;
        SaveVehicleProfile(row.CardUid, row.VehicleDescription, row.VehicleNumber);
    }

    private void SaveVehicleDescription(string cardUid, string? description)
    {
        SaveVehicleProfile(cardUid, description, null);
    }

    private void SaveVehicleProfile(string cardUid, string? description, string? vehicleNumber)
    {
        var normalizedUid = NormalizeUid(cardUid);
        if (normalizedUid is null)
        {
            return;
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        var normalizedNumber = NormalizeVehicleNumber(vehicleNumber);
        var profiles = RequireAppMemoryStore().GetSmartParkingCardProfiles().ToList();
        var profileIndex = profiles.FindIndex(profile => string.Equals(
            NormalizeUid(profile.CardUid),
            normalizedUid,
            StringComparison.OrdinalIgnoreCase));

        if (profileIndex >= 0)
        {
            var profile = profiles[profileIndex];
            profiles[profileIndex] = profile with
            {
                VehicleDescription = trimmedDescription,
                VehicleNumber = string.IsNullOrWhiteSpace(normalizedNumber) ? null : normalizedNumber,
                DescriptionCreatedAt = trimmedDescription is null
                                      && string.IsNullOrWhiteSpace(normalizedNumber)
                    ? profile.DescriptionCreatedAt
                    : profile.DescriptionCreatedAt ?? DateTimeOffset.UtcNow,
                DescriptionSource = trimmedDescription is null
                                    && string.IsNullOrWhiteSpace(normalizedNumber)
                    ? profile.DescriptionSource
                    : "admin-manual"
            };
        }
        else if (trimmedDescription is not null || !string.IsNullOrWhiteSpace(normalizedNumber))
        {
            profiles.Add(new SmartParkingCardProfile(
                normalizedUid,
                0,
                0,
                null,
                trimmedDescription,
                DateTimeOffset.UtcNow,
                "admin-manual",
                IsGeneratedFakeUid(normalizedUid),
                null,
                normalizedNumber));
        }

        RequireAppMemoryStore().SetSmartParkingCardProfiles(profiles);
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
               || EditableSettings.AutoExitOpenEnabled != originalSettings.AutoExitOpenEnabled
               || EditableSettings.AutoCloseAfterPassEnabled != originalSettings.AutoCloseAfterPassEnabled
               || EditableSettings.GatePassageThresholdCm != originalSettings.GatePassageThresholdCm
               || EditableSettings.OccupiedThresholdCm != originalSettings.OccupiedThresholdCm
               || !EditableSettings.ParkingSpotEnabledStates.SequenceEqual(originalSettings.ParkingSpotEnabledStates)
               || EditableSettings.ParkingStatusUpdateIntervalMs != originalSettings.ParkingStatusUpdateIntervalMs
               || EditableSettings.AllowedCardsText != originalSettings.AllowedCardsText
               || EditableSettings.BlockedCardsText != originalSettings.BlockedCardsText
               || HasLocalCardEditorChanges;
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

    private IAppMemoryStore RequireAppMemoryStore()
    {
        return AppMemoryStore ?? throw new InvalidOperationException("App memory store is not available.");
    }

    private IJSRuntime RequireJsRuntime()
    {
        return JsRuntime ?? throw new InvalidOperationException("JavaScript runtime is not available.");
    }

    private void SyncCardRowsFromSettings()
    {
        var profiles = RequireAppMemoryStore().GetSmartParkingCardProfiles()
            .Select(profile => new
            {
                Uid = NormalizeUid(profile.CardUid),
                profile.VehicleDescription,
                VehicleNumber = NormalizeVehicleNumber(profile.VehicleNumber)
            })
            .Where(profile => profile.Uid is not null)
            .ToDictionary(
                profile => profile.Uid!,
                profile => profile,
                StringComparer.OrdinalIgnoreCase);

        CardRows.Clear();
        foreach (var uid in ParseCardText(EditableSettings.AllowedCardsText))
        {
            CardRows.Add(new AdminCardEditorRow(
                Guid.NewGuid(),
                uid,
                profiles.GetValueOrDefault(uid)?.VehicleNumber ?? string.Empty,
                profiles.GetValueOrDefault(uid)?.VehicleDescription ?? string.Empty,
                false));
        }

        foreach (var uid in ParseCardText(EditableSettings.BlockedCardsText))
        {
            CardRows.Add(new AdminCardEditorRow(
                Guid.NewGuid(),
                uid,
                profiles.GetValueOrDefault(uid)?.VehicleNumber ?? string.Empty,
                profiles.GetValueOrDefault(uid)?.VehicleDescription ?? string.Empty,
                true));
        }

        HasLocalCardEditorChanges = false;
        NeedsIconRefresh = true;
    }

    private void RebuildCardsTextFromRows()
    {
        var allowedCards = new List<string>();
        var blockedCards = new List<string>();
        var seenCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CardRows)
        {
            var normalizedUid = NormalizeUid(row.CardUid);
            if (normalizedUid is null || !seenCards.Add(normalizedUid))
            {
                continue;
            }

            if (row.IsBlocked)
            {
                blockedCards.Add(normalizedUid);
            }
            else
            {
                allowedCards.Add(normalizedUid);
            }
        }

        EditableSettings.AllowedCardsText = string.Join('\n', allowedCards);
        EditableSettings.BlockedCardsText = string.Join('\n', blockedCards);
    }

    private static IReadOnlyList<string> ParseCardText(string cardsText)
    {
        return cardsText.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeUid)
            .Where(uid => uid is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(uid => uid!)
            .ToArray();
    }

    private static string FormatUidForEditing(string uid)
    {
        var compact = new string((uid ?? string.Empty).Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        return compact.Length <= 8 ? compact : compact[..8];
    }

    private static string NormalizeVehicleNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = new string(value.Trim().ToUpperInvariant().Where(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_').ToArray());
        return compact.Length <= 24 ? compact : compact[..24];
    }

    private static string? NormalizeUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return null;
        }

        var compact = new string(uid.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        return compact.Length == 8 ? compact : null;
    }

    private static bool IsGeneratedFakeUid(string uid)
    {
        return uid.StartsWith("FA", StringComparison.OrdinalIgnoreCase);
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
            SyncCardRowsFromSettings();
            StatusMessage = Texts.RefreshSuccessMessage;
            StateHasChanged();
        });
    }

    private void OnPreferencesChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    protected sealed class AdminCardEditorRow(
        Guid rowId,
        string cardUid,
        string vehicleNumber,
        string vehicleDescription,
        bool isBlocked)
    {
        public Guid RowId { get; } = rowId;
        public string CardUid { get; set; } = cardUid;
        public string VehicleNumber { get; set; } = vehicleNumber;
        public string VehicleDescription { get; set; } = vehicleDescription;
        public bool IsBlocked { get; set; } = isBlocked;
    }
}
