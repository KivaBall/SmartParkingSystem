using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Events;

public sealed class EventsService : IEventsService, IDisposable
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromMinutes(2);

    private static readonly IReadOnlyList<(string Label, Func<DeviceControllerConfiguration, string> Selector)>
        MonitorTemplateSelectors =
        [
            ("Forced", configuration => configuration.DisplayForcedText),
            ("Default", configuration => configuration.DisplayDefaultText),
            ("Allowed", configuration => configuration.DisplayAllowedText),
            ("Blocked", configuration => configuration.DisplayBlockedText),
            ("Invalid", configuration => configuration.DisplayInvalidText),
            ("Locked", configuration => configuration.DisplayLockedText)
        ];

    private readonly List<EventFeedItem> _events = [];
    private readonly Lock _eventsSync = new Lock();
    private readonly IDeviceSessionService _sessionService;
    private DeviceControllerSession? _previousSession;

    public EventsService(IDeviceSessionService sessionService)
    {
        _sessionService = sessionService;
        _previousSession = sessionService.CurrentSession;
        _sessionService.SessionChanged += OnSessionChanged;
    }

    public void Dispose()
    {
        _sessionService.SessionChanged -= OnSessionChanged;
    }

    public IReadOnlyList<EventFeedItem> GetRecentEvents()
    {
        lock (_eventsSync)
        {
            PruneExpiredEvents();
            return _events
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        lock (_eventsSync)
        {
            CaptureEvents(_previousSession, session);
            _previousSession = session;
            PruneExpiredEvents();
        }
    }

    private void CaptureEvents(DeviceControllerSession? previous, DeviceControllerSession? current)
    {
        if (previous is null && current is not null)
        {
            AddEvent(EventCategory.Connection, EventKind.ControllerConnected, current.Target.Label);
            return;
        }

        if (previous is not null && current is null)
        {
            AddEvent(EventCategory.Connection, EventKind.ControllerDisconnected, previous.Target.Label);
            return;
        }

        if (previous is null || current is null)
        {
            return;
        }

        if (!string.Equals(previous.Snapshot.Mode, current.Snapshot.Mode, StringComparison.OrdinalIgnoreCase))
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateStateChanged,
                previousValue: previous.Snapshot.Mode,
                currentValue: current.Snapshot.Mode);
        }

        if (previous.Snapshot.DisplayForced != current.Snapshot.DisplayForced)
        {
            AddEvent(
                EventCategory.Monitor,
                EventKind.MonitorForceModeChanged,
                currentValue: current.Snapshot.DisplayForced ? bool.TrueString : bool.FalseString);
        }

        if (!string.Equals(previous.Snapshot.DisplayText, current.Snapshot.DisplayText, StringComparison.Ordinal))
        {
            AddEvent(
                EventCategory.Monitor,
                EventKind.MonitorTextChanged,
                currentValue: current.Snapshot.DisplayText);
        }

        CaptureConfigurationEvents(previous.Configuration, current.Configuration);
        CaptureParkingEvents(previous, current);
    }

    private void CaptureConfigurationEvents(
        DeviceControllerConfiguration previous,
        DeviceControllerConfiguration current)
    {
        if (previous.ForceOpen != current.ForceOpen)
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateForceOpenChanged,
                currentValue: current.ForceOpen ? bool.TrueString : bool.FalseString);
        }

        if (previous.ForceLock != current.ForceLock)
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateForceLockChanged,
                currentValue: current.ForceLock ? bool.TrueString : bool.FalseString);
        }

        if (previous.OpenAngle != current.OpenAngle)
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateOpenAngleChanged,
                previousValue: previous.OpenAngle.ToString(),
                currentValue: current.OpenAngle.ToString());
        }

        if (previous.ClosedAngle != current.ClosedAngle)
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateClosedAngleChanged,
                previousValue: previous.ClosedAngle.ToString(),
                currentValue: current.ClosedAngle.ToString());
        }

        if (previous.OpenDurationMs != current.OpenDurationMs)
        {
            AddEvent(
                EventCategory.Gate,
                EventKind.GateOpenDurationChanged,
                previousValue: previous.OpenDurationMs.ToString(),
                currentValue: current.OpenDurationMs.ToString());
        }

        if (previous.TelemetryIntervalMs != current.TelemetryIntervalMs)
        {
            AddEvent(
                EventCategory.System,
                EventKind.ConnectionIntervalChanged,
                previousValue: previous.TelemetryIntervalMs.ToString(),
                currentValue: current.TelemetryIntervalMs.ToString());
        }

        if (previous.ThresholdCm != current.ThresholdCm)
        {
            AddEvent(
                EventCategory.System,
                EventKind.ParkingThresholdChanged,
                previousValue: previous.ThresholdCm.ToString(),
                currentValue: current.ThresholdCm.ToString());
        }

        for (var index = 0; index < Math.Min(previous.SlotEnabled.Count, current.SlotEnabled.Count); index++)
        {
            if (previous.SlotEnabled[index] == current.SlotEnabled[index])
            {
                continue;
            }

            AddEvent(
                EventCategory.Parking,
                EventKind.ParkingSlotAvailabilityChanged,
                $"P{index + 1}",
                previous.SlotEnabled[index] ? bool.TrueString : bool.FalseString,
                current.SlotEnabled[index] ? bool.TrueString : bool.FalseString);
        }

        if (!previous.AllowedCards.SequenceEqual(current.AllowedCards, StringComparer.OrdinalIgnoreCase))
        {
            AddEvent(
                EventCategory.System,
                EventKind.AllowedCardsChanged,
                previousValue: FormatCardList(previous.AllowedCards),
                currentValue: FormatCardList(current.AllowedCards));
        }

        if (!previous.BlockedCards.SequenceEqual(current.BlockedCards, StringComparer.OrdinalIgnoreCase))
        {
            AddEvent(
                EventCategory.System,
                EventKind.BlockedCardsChanged,
                previousValue: FormatCardList(previous.BlockedCards),
                currentValue: FormatCardList(current.BlockedCards));
        }

        foreach (var (label, selector) in MonitorTemplateSelectors)
        {
            var previousValue = selector(previous);
            var currentValue = selector(current);

            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                AddEvent(
                    EventCategory.Monitor,
                    EventKind.MonitorTemplateChanged,
                    label,
                    previousValue,
                    currentValue);
            }
        }
    }

    private void CaptureParkingEvents(DeviceControllerSession previous, DeviceControllerSession current)
    {
        for (var index = 0; index < Math.Min(previous.Snapshot.Slots.Count, current.Snapshot.Slots.Count); index++)
        {
            var previousSlot = previous.Snapshot.Slots[index];
            var currentSlot = current.Snapshot.Slots[index];

            if (!string.Equals(previousSlot.State, currentSlot.State, StringComparison.OrdinalIgnoreCase))
            {
                AddEvent(
                    EventCategory.Parking,
                    EventKind.ParkingSlotChanged,
                    $"P{currentSlot.SlotNumber}",
                    previousSlot.State,
                    currentSlot.State);
            }
        }
    }

    private static string FormatCardList(IReadOnlyList<string> cards)
    {
        return cards.Count == 0 ? "-" : string.Join(", ", cards);
    }

    private void AddEvent(
        EventCategory category,
        EventKind kind,
        string? subject = null,
        string? previousValue = null,
        string? currentValue = null)
    {
        _events.Add(
            new EventFeedItem(
                Guid.NewGuid().ToString("N"),
                category,
                kind,
                subject,
                previousValue,
                currentValue,
                DateTimeOffset.UtcNow));
    }

    private void PruneExpiredEvents()
    {
        var threshold = DateTimeOffset.UtcNow - RetentionWindow;
        _events.RemoveAll(item => item.CreatedAt < threshold);
    }
}