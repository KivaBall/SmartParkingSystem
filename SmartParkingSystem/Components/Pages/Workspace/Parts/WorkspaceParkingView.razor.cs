using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Models.Localization;
using SmartParkingSystem.Models.Parking;
using SmartParkingSystem.Services.DeviceConnection.Session;
using SmartParkingSystem.Services.Localization;
using SmartParkingSystem.Services.Parking;
using SmartParkingSystem.Services.Settings.Preferences;

namespace SmartParkingSystem.Components.Pages.Workspace.Parts;

public class WorkspaceParkingViewBase : ComponentBase, IDisposable
{
    private const double PositionStep = 2;
    private const double DefaultCenterLeftPercent = 50;
    private const double DefaultCenterTopPercent = 50;
    private const double MinLeftPercent = 2;
    private const double MaxLeftPercent = 98;
    private const double MinTopPercent = 2;
    private const double MaxTopPercent = 92;

    private static readonly IReadOnlyDictionary<int, ParkingLayoutItem> DefaultLayoutItems =
        new Dictionary<int, ParkingLayoutItem>
        {
            [1] = new ParkingLayoutItem(12, 28),
            [2] = new ParkingLayoutItem(12, 56),
            [3] = new ParkingLayoutItem(35, 22),
            [4] = new ParkingLayoutItem(35, 60),
            [5] = new ParkingLayoutItem(67, 30),
            [6] = new ParkingLayoutItem(67, 56)
        };

    [Inject]
    protected IParkingService? ParkingService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected ISettingsPreferencesService? SettingsPreferencesService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected IReadOnlyList<ParkingSlotSnapshot> Slots { get; private set; } = [];
    protected bool IsLoading { get; private set; } = true;
    protected bool IsBusy { get; private set; }
    protected string SelectedSlotId { get; private set; } = "P1";
    protected Dictionary<string, ParkingPositionDraft> DraftPositions { get; } = [];
    private string? LastParkingStateFingerprint { get; set; }
    private bool IsReloading { get; set; }

    protected ParkingTexts Texts => RequireLocalizationService().GetParkingTexts();
    protected bool EditParkingEnabled => RequireSettingsPreferencesService().EditParkingEnabled;

    protected IReadOnlyList<ParkingSlotSnapshot> VisibleSlots => !EditParkingEnabled
        ? [.. Slots.Where(slot => slot.State != ParkingSlotState.Disabled)]
        : Slots;

    protected ParkingSlotSnapshot? SelectedSlot => VisibleSlots.FirstOrDefault(slot => slot.Id == SelectedSlotId);
    protected ParkingPositionDraft? SelectedDraft => DraftPositions.GetValueOrDefault(SelectedSlotId);

    protected string MapClass => IsExiting
        ? "animate-exit-bottom rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-bottom rounded-md bg-brand-100/80 p-6 opacity-0";

    protected string MapStyle => IsExiting ? "animation-delay: 180ms;" : "animation-delay: 0ms;";

    protected string DetailsClass => IsExiting
        ? "animate-exit-bottom rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-bottom rounded-md bg-brand-100/80 p-6 opacity-0";

    protected string DetailsStyle => IsExiting ? "animation-delay: 0ms;" : "animation-delay: 180ms;";

    protected string SelectedSlotStateLabel => SelectedSlot?.State switch
    {
        ParkingSlotState.Free => Texts.FreeStateLabel,
        ParkingSlotState.Occupied => Texts.OccupiedStateLabel,
        ParkingSlotState.Disabled => Texts.DisabledStateLabel,
        _ => string.Empty
    };

    protected string SelectedSlotDurationLabel => SelectedSlot?.OccupiedDuration is { } duration
        ? FormatDuration(duration)
        : SelectedSlot?.State == ParkingSlotState.Disabled
            ? Texts.DisabledDurationLabel
            : Texts.NoOccupiedDurationLabel;

    protected string ToggleButtonLabel => SelectedSlot?.State == ParkingSlotState.Disabled
        ? Texts.EnableSlotButton
        : Texts.DisableSlotButton;

    protected string ToggleButtonClass => SelectedSlot?.State == ParkingSlotState.Disabled
        ? "w-full rounded-md bg-mint-300 px-4 py-4 text-base font-semibold text-calm-900 transition-all duration-[500ms] ease-out hover:bg-mint-200"
        : "w-full rounded-md bg-warm-100 px-4 py-4 text-base font-semibold text-calm-900 transition-all duration-[500ms] ease-out hover:bg-warm-200";

    protected string SelectedSlotBadgeClass => SelectedSlot?.State switch
    {
        ParkingSlotState.Free => "flex h-12 w-12 items-center justify-center rounded-md bg-mint-300 text-calm-900",
        ParkingSlotState.Occupied => "flex h-12 w-12 items-center justify-center rounded-md bg-brand-300 text-white",
        ParkingSlotState.Disabled => "flex h-12 w-12 items-center justify-center rounded-md bg-warm-100 text-calm-900",
        _ => "flex h-12 w-12 items-center justify-center rounded-md bg-white/85 text-calm-900"
    };

    protected string SelectedSlotIcon => SelectedSlot?.State == ParkingSlotState.Free
        ? "square-parking"
        : "square-parking-off";

    protected string SelectedSlotInfoClass => SelectedSlot?.State switch
    {
        ParkingSlotState.Free => "bg-mint-300 text-calm-900",
        ParkingSlotState.Occupied => "bg-brand-300 text-white",
        ParkingSlotState.Disabled => "bg-warm-100 text-calm-900",
        _ => "bg-white/85 text-calm-900"
    };

    public void Dispose()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        RequireSettingsPreferencesService().PreferencesChanged -= OnPreferencesChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
        RequireSettingsPreferencesService().PreferencesChanged += OnPreferencesChanged;
        await RefreshSlotsAsync();
    }

    protected void SelectSlot(string slotId)
    {
        SelectedSlotId = slotId;
    }

    protected async Task ToggleSelectedSlotAsync()
    {
        if (SelectedSlot is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            Slots = await RequireParkingService().ToggleSlotEnabledAsync(SelectedSlot.Id);
            LastParkingStateFingerprint = BuildParkingStateFingerprint(RequireDeviceSessionService().CurrentSession);
            SyncDrafts();
        }
        finally
        {
            IsBusy = false;
        }

        await InvokeAsync(StateHasChanged);
    }

    protected Task MoveUpAsync()
    {
        return MoveSelectedSlotAsync(0, -PositionStep);
    }

    protected Task MoveDownAsync()
    {
        return MoveSelectedSlotAsync(0, PositionStep);
    }

    protected Task MoveLeftAsync()
    {
        return MoveSelectedSlotAsync(-PositionStep, 0);
    }

    protected Task MoveRightAsync()
    {
        return MoveSelectedSlotAsync(PositionStep, 0);
    }

    protected Task ResetPositionAsync()
    {
        if (SelectedDraft is null)
        {
            return Task.CompletedTask;
        }

        SelectedDraft.LeftPercent = DefaultCenterLeftPercent;
        SelectedDraft.TopPercent = DefaultCenterTopPercent;
        RequireSettingsPreferencesService()
            .SetParkingSlotPosition(
                SelectedSlotId,
                SelectedDraft.LeftPercent,
                SelectedDraft.TopPercent);

        return InvokeAsync(StateHasChanged);
    }

    protected string GetMarkerClass(ParkingSlotSnapshot slot)
    {
        var colorClass = slot.State switch
        {
            ParkingSlotState.Free => "bg-mint-300 text-calm-900 hover:bg-mint-200",
            ParkingSlotState.Occupied => "bg-brand-300 text-white hover:bg-brand-200",
            ParkingSlotState.Disabled => "bg-warm-100 text-calm-900 hover:bg-warm-200",
            _ => "bg-white/85 text-calm-900"
        };

        var selectedClass = slot.Id == SelectedSlotId ? "scale-105" : "scale-100";
        return $"flex h-16 w-16 flex-col items-center justify-center gap-1 rounded-md {colorClass} {selectedClass
        } transition-all duration-[500ms] ease-out";
    }

    protected static string GetMarkerIcon(ParkingSlotSnapshot slot)
    {
        return slot.State == ParkingSlotState.Free
            ? "square-parking"
            : "square-parking-off";
    }

    protected string GetSlotPositionStyle(ParkingSlotSnapshot slot)
    {
        var draft = DraftPositions.GetValueOrDefault(slot.Id) ??
                    BuildInitialDraft(slot.Id);
        return $"position: absolute; left: {draft.LeftPercent:F1}%; top: {draft.TopPercent
            :F1}%; transform: translate(-50%, -50%);";
    }

    private async Task RefreshSlotsAsync()
    {
        if (IsReloading)
        {
            return;
        }

        IsReloading = true;
        try
        {
            Slots = await RequireParkingService().GetSlotsAsync();
            LastParkingStateFingerprint = BuildParkingStateFingerprint(RequireDeviceSessionService().CurrentSession);
            IsLoading = false;
            SyncDrafts();

            if (VisibleSlots.All(slot => slot.Id != SelectedSlotId))
            {
                SelectedSlotId = VisibleSlots.FirstOrDefault()?.Id ?? "P1";
            }
        }
        finally
        {
            IsReloading = false;
        }
    }

    private Task MoveSelectedSlotAsync(double leftOffset, double topOffset)
    {
        if (SelectedDraft is null)
        {
            return Task.CompletedTask;
        }

        SelectedDraft.LeftPercent = Math.Clamp(SelectedDraft.LeftPercent + leftOffset, MinLeftPercent, MaxLeftPercent);
        SelectedDraft.TopPercent = Math.Clamp(SelectedDraft.TopPercent + topOffset, MinTopPercent, MaxTopPercent);
        RequireSettingsPreferencesService()
            .SetParkingSlotPosition(
                SelectedSlotId,
                SelectedDraft.LeftPercent,
                SelectedDraft.TopPercent);

        return InvokeAsync(StateHasChanged);
    }

    private void SyncDrafts()
    {
        foreach (var slot in Slots)
        {
            if (DraftPositions.ContainsKey(slot.Id))
            {
                continue;
            }

            if (RequireSettingsPreferencesService()
                .TryGetParkingSlotPosition(
                    slot.Id,
                    out var leftPercent,
                    out var topPercent))
            {
                DraftPositions[slot.Id] = new ParkingPositionDraft(leftPercent, topPercent);
                continue;
            }

            DraftPositions[slot.Id] = BuildInitialDraft(slot.Id);
        }

        foreach (var draftSlotId in DraftPositions.Keys.Except(Slots.Select(slot => slot.Id)).ToArray())
        {
            DraftPositions.Remove(draftSlotId);
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)Math.Floor(duration.TotalHours);
        return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private static ParkingPositionDraft BuildInitialDraft(string slotId)
    {
        if (TryGetDefaultLayoutItem(slotId, out var layoutItem))
        {
            return new ParkingPositionDraft(layoutItem.LeftPercent, layoutItem.TopPercent);
        }

        return BuildFallbackDraft(slotId);
    }

    private static bool TryGetDefaultLayoutItem(string slotId, out ParkingLayoutItem layoutItem)
    {
        layoutItem = default;

        if (!TryParseSlotNumber(slotId, out var slotNumber))
        {
            return false;
        }

        return DefaultLayoutItems.TryGetValue(slotNumber, out layoutItem);
    }

    private static ParkingPositionDraft BuildFallbackDraft(string slotId)
    {
        const int columns = 3;
        const double leftStart = 20;
        const double leftStep = 24;
        const double topStart = 22;
        const double topStep = 18;

        if (!TryParseSlotNumber(slotId, out var slotNumber))
        {
            return new ParkingPositionDraft(DefaultCenterLeftPercent, DefaultCenterTopPercent);
        }

        var extraIndex = Math.Max(slotNumber - (DefaultLayoutItems.Count + 1), 0);
        var column = extraIndex % columns;
        var row = extraIndex / columns;

        return new ParkingPositionDraft(
            leftStart + column * leftStep,
            topStart + row * topStep);
    }

    private static bool TryParseSlotNumber(string slotId, out int slotNumber)
    {
        slotNumber = 0;
        return slotId.Length >= 2
               && slotId[0] == 'P'
               && int.TryParse(slotId[1..], out slotNumber);
    }

    private static string BuildParkingStateFingerprint(DeviceControllerSession? session)
    {
        if (session is null)
        {
            return "null";
        }

        var configurationPart = string.Join(
            ",",
            session.Configuration.SlotEnabled.Select(enabled => enabled ? '1' : '0'));

        var slotsPart = string.Join(
            ";",
            session.Snapshot.Slots
                .OrderBy(slot => slot.SlotNumber)
                .Select(slot => $"{slot.SlotNumber}:{slot.State}:{slot.Enabled}:{slot.OccupiedMs}:{slot.DistanceCm}"));

        return $"{session.Profile.SlotCount}|{configurationPart}|{slotsPart}";
    }

    private IParkingService RequireParkingService()
    {
        return ParkingService ?? throw new InvalidOperationException("Parking service is not available.");
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
        if (IsBusy || IsReloading)
        {
            return;
        }

        var fingerprint = BuildParkingStateFingerprint(session);
        if (string.Equals(LastParkingStateFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await RefreshSlotsAsync();
            StateHasChanged();
        });
    }

    private void OnPreferencesChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    protected sealed class ParkingPositionDraft
    {
        public ParkingPositionDraft(double leftPercent, double topPercent)
        {
            LeftPercent = leftPercent;
            TopPercent = topPercent;
        }

        public double LeftPercent { get; set; }
        public double TopPercent { get; set; }
    }

    private readonly record struct ParkingLayoutItem(double LeftPercent, double TopPercent);
}