using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Events;
using SmartParkingSystem.Maui.Services.Localization;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceEventsViewBase : ComponentBase, IDisposable
{
    [Inject]
    protected IEventsService? EventsService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected string SearchText { get; set; } = string.Empty;
    protected EventCategory? SelectedCategory { get; private set; }
    protected EventsTexts Texts => RequireLocalizationService().GetEventsTexts();

    protected string FiltersClass => IsExiting
        ? "animate-exit-left rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-left rounded-md bg-brand-100/80 p-6 opacity-0";

    protected string ListClass => IsExiting
        ? "animate-exit-right rounded-md bg-warm-100 p-6"
        : "animate-page-enter-right rounded-md bg-warm-100 p-6 opacity-0";

    protected static string FiltersStyle => "animation-delay: 0ms;";
    protected static string ListStyle => "animation-delay: 120ms;";

    protected IReadOnlyList<(string Label, EventCategory? Category)> Filters =>
    [
        (Texts.AllFilterLabel, null),
        (Texts.ConnectionFilterLabel, EventCategory.Connection),
        (Texts.GateFilterLabel, EventCategory.Gate),
        (Texts.ParkingFilterLabel, EventCategory.Parking),
        (Texts.MonitorFilterLabel, EventCategory.Monitor),
        (Texts.SystemFilterLabel, EventCategory.System)
    ];

    protected IReadOnlyList<EventFeedItem> FilteredEvents =>
        RequireEventsService()
            .GetRecentEvents()
            .Where(item => SelectedCategory is null || item.Category == SelectedCategory)
            .Where(item => string.IsNullOrWhiteSpace(SearchText)
                           || GetEventTitle(item).Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                           || GetEventDescription(item).Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public void Dispose()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
    }

    protected void SelectFilter(EventCategory? category)
    {
        SelectedCategory = category;
    }

    protected string GetFilterClass(EventCategory? category)
    {
        var isSelected = category == SelectedCategory;
        return isSelected
            ? "inline-flex min-h-12 items-center justify-center rounded-md bg-brand-200 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-brand-400"
            : "inline-flex min-h-12 items-center justify-center rounded-md bg-white/85 px-4 py-3 text-sm font-semibold text-calm-700 transition-all duration-500 ease-out hover:bg-calm-100";
    }

    protected string FormatTimestamp(DateTimeOffset timestamp)
    {
        var age = DateTimeOffset.UtcNow - timestamp;
        return age.TotalSeconds < 1
            ? Texts.NowLabel
            : $"{Math.Max(1, (int)Math.Floor(age.TotalSeconds))}{Texts.SecondsSuffix}";
    }

    protected string GetEventTitle(EventFeedItem item)
    {
        return item.Kind switch
        {
            EventKind.ControllerConnected => Texts.ControllerConnectedTitle,
            EventKind.ControllerDisconnected => Texts.ControllerDisconnectedTitle,
            EventKind.GateStateChanged => Texts.GateStateChangedTitle,
            EventKind.GateForceOpenChanged => Texts.GateForceOpenChangedTitle,
            EventKind.GateForceLockChanged => Texts.GateForceLockChangedTitle,
            EventKind.GateOpenAngleChanged => Texts.GateOpenAngleChangedTitle,
            EventKind.GateClosedAngleChanged => Texts.GateClosedAngleChangedTitle,
            EventKind.GateOpenDurationChanged => Texts.GateOpenDurationChangedTitle,
            EventKind.MonitorForceModeChanged => Texts.MonitorForceModeChangedTitle,
            EventKind.MonitorTextChanged => Texts.MonitorTextChangedTitle,
            EventKind.MonitorTemplateChanged => string.Format(
                Texts.MonitorTemplateChangedTitleFormat,
                GetMonitorTemplateLabel(item.Subject)),
            EventKind.ConnectionIntervalChanged => Texts.ConnectionIntervalChangedTitle,
            EventKind.ParkingThresholdChanged => Texts.ParkingThresholdChangedTitle,
            EventKind.ParkingSlotChanged => string.Format(
                Texts.ParkingSlotChangedTitleFormat,
                item.Subject ?? string.Empty),
            EventKind.ParkingSlotAvailabilityChanged => string.Format(
                Texts.ParkingSlotAvailabilityChangedTitleFormat,
                item.Subject ?? string.Empty),
            EventKind.AllowedCardsChanged => Texts.AllowedCardsChangedTitle,
            EventKind.BlockedCardsChanged => Texts.BlockedCardsChangedTitle,
            _ => string.Empty
        };
    }

    protected string GetEventDescription(EventFeedItem item)
    {
        return item.Kind switch
        {
            EventKind.ControllerConnected => item.Subject ?? string.Empty,
            EventKind.ControllerDisconnected => item.Subject ?? string.Empty,
            EventKind.GateStateChanged => FormatTransition(item.PreviousValue, item.CurrentValue),
            EventKind.GateForceOpenChanged => ParseBooleanLabel(item.CurrentValue),
            EventKind.GateForceLockChanged => ParseBooleanLabel(item.CurrentValue),
            EventKind.GateOpenAngleChanged => FormatTransition(
                item.PreviousValue,
                item.CurrentValue,
                Texts.DegreesUnit),
            EventKind.GateClosedAngleChanged => FormatTransition(
                item.PreviousValue,
                item.CurrentValue,
                Texts.DegreesUnit),
            EventKind.GateOpenDurationChanged => FormatTransition(
                item.PreviousValue,
                item.CurrentValue,
                Texts.MillisecondsUnit),
            EventKind.MonitorForceModeChanged => ParseBooleanLabel(item.CurrentValue),
            EventKind.MonitorTextChanged => item.CurrentValue ?? string.Empty,
            EventKind.MonitorTemplateChanged => $"{GetMonitorTemplateLabel(item.Subject)}: {
                FormatTransition(item.PreviousValue, item.CurrentValue)}",
            EventKind.ConnectionIntervalChanged => FormatTransition(
                item.PreviousValue,
                item.CurrentValue,
                Texts.MillisecondsUnit),
            EventKind.ParkingThresholdChanged => FormatTransition(
                item.PreviousValue,
                item.CurrentValue,
                Texts.CentimetersUnit),
            EventKind.ParkingSlotChanged => $"{item.Subject}: {FormatTransition(
                item.PreviousValue,
                item.CurrentValue)}",
            EventKind.ParkingSlotAvailabilityChanged => $"{item.Subject}: {
                ParseBooleanTransition(item.PreviousValue, item.CurrentValue)}",
            EventKind.AllowedCardsChanged => FormatTransition(item.PreviousValue, item.CurrentValue),
            EventKind.BlockedCardsChanged => FormatTransition(item.PreviousValue, item.CurrentValue),
            _ => string.Empty
        };
    }

    private IEventsService RequireEventsService()
    {
        return EventsService ?? throw new InvalidOperationException("Events service is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private IDeviceSessionService RequireDeviceSessionService()
    {
        return DeviceSessionService ?? throw new InvalidOperationException("Device session service is not available.");
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private string ParseBooleanLabel(string? rawValue)
    {
        return bool.TryParse(rawValue, out var parsed) && parsed
            ? Texts.EnabledLabel
            : Texts.DisabledLabel;
    }

    private string ParseBooleanTransition(string? previousValue, string? currentValue)
    {
        return $"{ParseBooleanLabel(previousValue)} -> {ParseBooleanLabel(currentValue)}";
    }

    private string GetMonitorTemplateLabel(string? rawValue)
    {
        return rawValue switch
        {
            "Forced" => Texts.ForcedMonitorTemplateLabel,
            "Default" => Texts.DefaultMonitorTemplateLabel,
            "Allowed" => Texts.AllowedMonitorTemplateLabel,
            "Blocked" => Texts.BlockedMonitorTemplateLabel,
            "Invalid" => Texts.InvalidMonitorTemplateLabel,
            "Locked" => Texts.LockedMonitorTemplateLabel,
            _ => rawValue ?? string.Empty
        };
    }

    private static string FormatTransition(string? previousValue, string? currentValue, string? unit = null)
    {
        var suffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        return $"{previousValue}{suffix} -> {currentValue}{suffix}";
    }
}