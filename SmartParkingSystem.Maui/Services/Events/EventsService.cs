using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Maui.Services.AppMemory;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Events;

public sealed class EventsService : IEventsService, IDisposable
{
    private const int MaxBackendSyncAttachmentCharacters = 768 * 1024;
    private const int MaxBackendSyncEvents = 25;
    private const int MaxStoredEvents = 1000;

    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(1);

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
    private readonly IAppMemoryStore _memoryStore;
    private readonly IDeviceSessionService _sessionService;
    private int _eventsVersion;
    private DeviceControllerSession? _previousSession;

    public EventsService(IDeviceSessionService sessionService, IAppMemoryStore memoryStore)
    {
        _sessionService = sessionService;
        _memoryStore = memoryStore;
        _events.AddRange(memoryStore.GetEvents());
        if (PruneStoredEvents())
        {
            PersistEvents();
        }

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
            if (PruneStoredEvents())
            {
                PersistEvents();
            }

            return _events
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }
    }

    public void AddCameraSnapshotEvent(string filePath)
    {
        var attachmentDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(File.ReadAllBytes(filePath))}";

        lock (_eventsSync)
        {
            AddEvent(
                EventCategory.Camera,
                EventKind.CameraSnapshotCaptured,
                Path.GetFileName(filePath),
                attachmentDataUrl: attachmentDataUrl);
            PruneStoredEvents();
            PersistEvents();
        }
    }

    public IReadOnlyList<EventFeedItem> GetBackendSyncEvents()
    {
        lock (_eventsSync)
        {
            if (PruneStoredEvents())
            {
                PersistEvents();
            }

            var syncEvents = _events
                .OrderByDescending(item => item.CreatedAt)
                .Take(MaxBackendSyncEvents)
                .OrderBy(item => item.CreatedAt)
                .ToArray();

            return LimitAttachmentPayload(syncEvents);
        }
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        lock (_eventsSync)
        {
            var previousVersion = _eventsVersion;
            CaptureEvents(_previousSession, session);
            _previousSession = session;
            var pruned = PruneStoredEvents();
            if (_eventsVersion != previousVersion || pruned)
            {
                PersistEvents();
            }
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
        string? currentValue = null,
        string? attachmentDataUrl = null)
    {
        _events.Add(
            new EventFeedItem(
                Guid.NewGuid().ToString("N"),
                category,
                kind,
                subject,
                previousValue,
                currentValue,
                DateTimeOffset.UtcNow,
                attachmentDataUrl));
        _eventsVersion++;
    }

    private bool PruneStoredEvents()
    {
        var threshold = DateTimeOffset.UtcNow - RetentionWindow;
        var retainedEvents = _events
            .Where(item => item.CreatedAt >= threshold)
            .OrderByDescending(item => item.CreatedAt)
            .Take(MaxStoredEvents)
            .OrderBy(item => item.CreatedAt)
            .ToArray();

        if (_events.SequenceEqual(retainedEvents))
        {
            return false;
        }

        _events.Clear();
        _events.AddRange(retainedEvents);
        return true;
    }

    private void PersistEvents()
    {
        _memoryStore.SetEvents(_events);
    }

    private static IReadOnlyList<EventFeedItem> LimitAttachmentPayload(IReadOnlyList<EventFeedItem> events)
    {
        var attachmentCharacters = 0;
        var trimmedEvents = new EventFeedItem[events.Count];

        for (var index = events.Count - 1; index >= 0; index--)
        {
            var eventItem = events[index];
            var attachmentLength = eventItem.AttachmentDataUrl?.Length ?? 0;
            if (attachmentLength == 0 || attachmentCharacters + attachmentLength <= MaxBackendSyncAttachmentCharacters)
            {
                attachmentCharacters += attachmentLength;
                trimmedEvents[index] = eventItem;
                continue;
            }

            trimmedEvents[index] = eventItem with { AttachmentDataUrl = null };
        }

        return trimmedEvents;
    }
}