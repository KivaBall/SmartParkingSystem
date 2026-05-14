using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.Maui.Services.AppMemory;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;

namespace SmartParkingSystem.Maui.Services.Parking;

public sealed class SmartParkingRouteService : IDisposable
{
    private const int PhysicalRouteSlotCount = 3;
    private const int NearestRouteSlotNumber = 1;
    private const int MiddleRouteSlotNumber = 3;
    private const int FarthestRouteSlotNumber = 2;
    private static readonly IReadOnlyDictionary<int, int> PhysicalRouteSlotRanks = new Dictionary<int, int>
    {
        [NearestRouteSlotNumber] = 0,
        [MiddleRouteSlotNumber] = 1,
        [FarthestRouteSlotNumber] = 2
    };
    private static readonly TimeSpan AssignmentWindow = TimeSpan.FromSeconds(30);
    private readonly Dictionary<int, ActiveVisit> _activeVisits = [];

    private readonly IAppMemoryStore _memoryStore;
    private readonly IParkingService _parkingService;
    private readonly Dictionary<string, SmartParkingCardProfile> _profiles;
    private readonly IDeviceSessionService _sessionService;
    private readonly Lock _sync = new Lock();
    private int _lastAccessCounter;
    private PendingAccess? _pendingAccess;
    private DeviceControllerSession? _previousSession;

    public SmartParkingRouteService(
        IDeviceSessionService sessionService,
        IParkingService parkingService,
        IAppMemoryStore memoryStore)
    {
        _sessionService = sessionService;
        _parkingService = parkingService;
        _memoryStore = memoryStore;
        _profiles = memoryStore.GetSmartParkingCardProfiles()
            .ToDictionary(profile => profile.CardUid, StringComparer.OrdinalIgnoreCase);
        _previousSession = sessionService.CurrentSession;
        _lastAccessCounter = _previousSession?.Snapshot.LastAccessCounter ?? 0;
        _sessionService.SessionChanged += OnSessionChanged;
    }

    public void Dispose()
    {
        _sessionService.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        PendingAccess? routeRequest = null;

        lock (_sync)
        {
            TrackCompletedVisits(_previousSession, session);
            routeRequest = TrackNewAccess(session);
            TrackNewOccupancies(_previousSession, session);
            _previousSession = session;
        }

        if (routeRequest is not null)
        {
            _ = RouteAllowedCardAsync(routeRequest);
        }
    }

    private PendingAccess? TrackNewAccess(DeviceControllerSession? session)
    {
        var snapshot = session?.Snapshot;
        if (snapshot is null || snapshot.LastAccessCounter <= _lastAccessCounter)
        {
            return null;
        }

        _lastAccessCounter = snapshot.LastAccessCounter;
        if (!string.Equals(snapshot.LastAccessResult, "ALLOWED", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(snapshot.LastAccessUid))
        {
            return null;
        }

        _pendingAccess = new PendingAccess(snapshot.LastAccessUid, DateTimeOffset.UtcNow);
        return _pendingAccess;
    }

    private void TrackNewOccupancies(DeviceControllerSession? previous, DeviceControllerSession? current)
    {
        if (_pendingAccess is null || current is null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - _pendingAccess.CreatedAt > AssignmentWindow)
        {
            _pendingAccess = null;
            return;
        }

        foreach (var currentSlot in current.Snapshot.Slots.Where(slot => slot.SlotNumber <= PhysicalRouteSlotCount))
        {
            var wasOccupied = previous?.Snapshot.Slots
                .FirstOrDefault(slot => slot.SlotNumber == currentSlot.SlotNumber)
                ?.State.Equals("OCCUPIED", StringComparison.OrdinalIgnoreCase) == true;

            var isOccupied = currentSlot.State.Equals("OCCUPIED", StringComparison.OrdinalIgnoreCase);
            if (wasOccupied || !isOccupied || _activeVisits.ContainsKey(currentSlot.SlotNumber))
            {
                continue;
            }

            _activeVisits[currentSlot.SlotNumber] = new ActiveVisit(
                _pendingAccess.CardUid,
                $"P{currentSlot.SlotNumber}",
                DateTimeOffset.UtcNow);
            _pendingAccess = null;
            return;
        }
    }

    private void TrackCompletedVisits(DeviceControllerSession? previous, DeviceControllerSession? current)
    {
        if (previous is null || current is null)
        {
            return;
        }

        foreach (var previousSlot in previous.Snapshot.Slots.Where(slot => slot.SlotNumber <= PhysicalRouteSlotCount))
        {
            var currentSlot = current.Snapshot.Slots.FirstOrDefault(slot => slot.SlotNumber == previousSlot.SlotNumber);
            if (currentSlot is null)
            {
                continue;
            }

            var wasOccupied = previousSlot.State.Equals("OCCUPIED", StringComparison.OrdinalIgnoreCase);
            var isOccupied = currentSlot.State.Equals("OCCUPIED", StringComparison.OrdinalIgnoreCase);
            if (!wasOccupied || isOccupied || !_activeVisits.Remove(previousSlot.SlotNumber, out var visit))
            {
                continue;
            }

            UpdateProfile(visit, DateTimeOffset.UtcNow - visit.StartedAt);
        }
    }

    private void UpdateProfile(ActiveVisit visit, TimeSpan duration)
    {
        var durationMinutes = Math.Max(0.01, duration.TotalMinutes);
        if (!_profiles.TryGetValue(visit.CardUid, out var current))
        {
            _profiles[visit.CardUid] = new SmartParkingCardProfile(
                visit.CardUid,
                1,
                durationMinutes,
                visit.SlotId);
        }
        else
        {
            var visitCount = current.VisitCount + 1;
            var average = (current.AverageParkingDurationMinutes * current.VisitCount + durationMinutes) / visitCount;
            _profiles[visit.CardUid] = current with
            {
                VisitCount = visitCount,
                AverageParkingDurationMinutes = average,
                LastKnownSlotId = visit.SlotId
            };
        }

        _memoryStore.SetSmartParkingCardProfiles(_profiles.Values.ToArray());
    }

    private async Task RouteAllowedCardAsync(PendingAccess access)
    {
        var slots = await _parkingService.GetSlotsAsync();
        var recommendedSlot = RecommendSlot(access.CardUid, slots);
        if (recommendedSlot is null)
        {
            return;
        }

        await _parkingService.ShowRouteToSlotAsync(recommendedSlot.Id);
    }

    private ParkingSlotSnapshot? RecommendSlot(string cardUid, IReadOnlyList<ParkingSlotSnapshot> slots)
    {
        var candidates = slots
            .Where(slot => slot.State == ParkingSlotState.Free)
            .Select(slot => new RouteSlotCandidate(slot, ParseSlotNumber(slot.Id)))
            .Where(item => item.Number is >= 1 and <= PhysicalRouteSlotCount)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        if (!_profiles.TryGetValue(cardUid, out var profile) || profile.VisitCount <= 0)
        {
            return PickClosestTo(candidates, NearestRouteSlotNumber);
        }

        // TODO: Keep this hardcoded until the physical parking layout becomes configurable.
        // Physical distance order from entrance: P1 nearest, then P3, then P2 farthest.
        var durationBand = ClassifyDurationBand(profile);
        return durationBand switch
        {
            ParkingDurationBand.Long => PickClosestTo(candidates, FarthestRouteSlotNumber),
            ParkingDurationBand.Medium => PickClosestTo(candidates, MiddleRouteSlotNumber),
            _ => PickClosestTo(candidates, NearestRouteSlotNumber)
        };
    }

    private ParkingDurationBand ClassifyDurationBand(SmartParkingCardProfile profile)
    {
        var knownAverages = _profiles.Values
            .Where(item => item.VisitCount > 0)
            .Select(item => item.AverageParkingDurationMinutes)
            .Order()
            .ToArray();

        if (knownAverages.Length <= 1)
        {
            return ParkingDurationBand.Short;
        }

        var minAverage = knownAverages[0];
        var maxAverage = knownAverages[^1];
        if (Math.Abs(maxAverage - minAverage) < 0.001)
        {
            return ParkingDurationBand.Short;
        }

        var normalizedRank = (profile.AverageParkingDurationMinutes - minAverage) / (maxAverage - minAverage);

        return normalizedRank switch
        {
            <= 1d / 3d => ParkingDurationBand.Short,
            >= 2d / 3d => ParkingDurationBand.Long,
            _ => ParkingDurationBand.Medium
        };
    }

    private static ParkingSlotSnapshot PickClosestTo(
        IEnumerable<RouteSlotCandidate> candidates,
        int targetSlotNumber)
    {
        var targetRank = GetPhysicalRouteRank(targetSlotNumber);
        return candidates
            .OrderBy(item => Math.Abs(GetPhysicalRouteRank(item.Number.GetValueOrDefault()) - targetRank))
            .ThenBy(item => item.Number)
            .First()
            .Slot;
    }

    private static int GetPhysicalRouteRank(int slotNumber)
    {
        return PhysicalRouteSlotRanks.GetValueOrDefault(slotNumber, slotNumber);
    }

    private static int? ParseSlotNumber(string slotId)
    {
        return slotId.Length >= 2 && slotId[0] == 'P' && int.TryParse(slotId[1..], out var number)
            ? number
            : null;
    }

    private sealed record PendingAccess(string CardUid, DateTimeOffset CreatedAt);

    private sealed record ActiveVisit(string CardUid, string SlotId, DateTimeOffset StartedAt);

    private sealed record RouteSlotCandidate(ParkingSlotSnapshot Slot, int? Number);

    private enum ParkingDurationBand
    {
        Short,
        Medium,
        Long
    }
}
