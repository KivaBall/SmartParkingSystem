using System.Security.Cryptography;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.Maui.Services.AppMemory;
using SmartParkingSystem.Maui.Services.Camera;
using SmartParkingSystem.Maui.Services.DeviceConnection.Commands;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Events;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.CameraAi;

public sealed class CameraAiRfidAccessService : ICameraAiRfidAccessService, IDisposable
{
    private readonly IAppMemoryStore _memoryStore;
    private readonly IDeviceCommandService _commandService;
    private readonly IDeviceSessionService _sessionService;
    private readonly IEntryCameraService _entryCameraService;
    private readonly IEventsService _eventsService;
    private readonly IVehicleRecognitionAiService _vehicleRecognitionAiService;
    private readonly ISettingsPreferencesService _preferencesService;
    private readonly SemaphoreSlim _cameraAttemptGate = new SemaphoreSlim(1, 1);
    private readonly Lock _sync = new Lock();
    private DeviceControllerSession? _previousSession;
    private int _lastFrontCounter;
    private int _lastAccessCounter;

    public CameraAiRfidAccessService(
        IDeviceSessionService sessionService,
        IDeviceCommandService commandService,
        IEntryCameraService entryCameraService,
        IVehicleRecognitionAiService vehicleRecognitionAiService,
        ISettingsPreferencesService preferencesService,
        IAppMemoryStore memoryStore,
        IEventsService eventsService)
    {
        _sessionService = sessionService;
        _commandService = commandService;
        _entryCameraService = entryCameraService;
        _vehicleRecognitionAiService = vehicleRecognitionAiService;
        _preferencesService = preferencesService;
        _memoryStore = memoryStore;
        _eventsService = eventsService;
        _previousSession = sessionService.CurrentSession;
        _lastFrontCounter = _previousSession?.Snapshot.FrontAccessCounter ?? 0;
        _lastAccessCounter = _previousSession?.Snapshot.LastAccessCounter ?? 0;
        _sessionService.SessionChanged += OnSessionChanged;
    }

    public void Dispose()
    {
        _sessionService.SessionChanged -= OnSessionChanged;
        _cameraAttemptGate.Dispose();
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        string? allowedRfidToEnrich = null;
        var shouldRunCameraAccess = false;

        lock (_sync)
        {
            if (session?.Snapshot is not null && session.Snapshot.FrontAccessCounter > _lastFrontCounter)
            {
                _lastFrontCounter = session.Snapshot.FrontAccessCounter;
                shouldRunCameraAccess = _preferencesService.OpenAiUsageEnabled
                                        && _preferencesService.CameraAiAccessScanEnabled;
            }

            if (session?.Snapshot is not null && session.Snapshot.LastAccessCounter > _lastAccessCounter)
            {
                _lastAccessCounter = session.Snapshot.LastAccessCounter;
                if (string.Equals(session.Snapshot.LastAccessResult, "ALLOWED", StringComparison.OrdinalIgnoreCase)
                    && _preferencesService.OpenAiUsageEnabled
                    && _preferencesService.CameraAiCaptureMissingRfidDescriptionsEnabled
                    && !string.IsNullOrWhiteSpace(session.Snapshot.LastAccessUid)
                    && !HasVehicleDescription(session.Snapshot.LastAccessUid))
                {
                    allowedRfidToEnrich = session.Snapshot.LastAccessUid;
                }
            }

            _previousSession = session;
        }

        if (shouldRunCameraAccess)
        {
            _ = RunCameraFirstAccessAsync();
        }

        if (allowedRfidToEnrich is not null)
        {
            _ = EnrichAllowedRfidAsync(allowedRfidToEnrich);
        }
    }

    private async Task RunCameraFirstAccessAsync()
    {
        if (!await _cameraAttemptGate.WaitAsync(0))
        {
            return;
        }

        string? imageDataUrl = null;
        try
        {
            imageDataUrl = await _entryCameraService.CaptureFrameDataUrlAsync();
            if (string.IsNullOrWhiteSpace(imageDataUrl))
            {
                var reason = string.IsNullOrWhiteSpace(_entryCameraService.LastFailureReason)
                    ? "Camera unavailable"
                    : _entryCameraService.LastFailureReason;
                _eventsService.AddCameraAccessEvent("camera", reason, null, null);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnavailableText);
                return;
            }

            var knownProfiles = GetKnownProfiles();
            var result = await _vehicleRecognitionAiService.RecognizeAsync(
                imageDataUrl,
                knownProfiles,
                VehicleAiRecognitionMode.MatchCameraVehicleToKnownProfiles);
            if (!result.Succeeded || result.Kind is VehicleAiRecognitionKind.Uncertain)
            {
                _eventsService.AddCameraAccessEvent("AI", $"Denied: {result.Reason}", null, imageDataUrl);
                await ShowCameraLcdTextAsync(
                    result.Succeeded
                        ? _preferencesService.CameraLcdUnrecognizedText
                        : _preferencesService.CameraLcdAiUnavailableText);
                return;
            }

            var allowedCards = GetAllowedCards();
            var blockedCards = GetBlockedCards();
            if (result.Kind is VehicleAiRecognitionKind.NoVehicle)
            {
                _eventsService.AddCameraAccessEvent(
                    "camera",
                    "Denied: no vehicle in frame",
                    result.VehicleDescription,
                    imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                return;
            }

            if (result.Kind is VehicleAiRecognitionKind.Match)
            {
                if (result.MatchedCardUid is null)
                {
                    _eventsService.AddCameraAccessEvent("AI", "Denied: matched RFID is missing", null, imageDataUrl);
                    await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                    return;
                }

                if (allowedCards.Contains(result.MatchedCardUid))
                {
                    SaveVehicleDescription(
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        "camera-ai-match",
                        result.Reason);
                    await _commandService.OpenTemporarilyAsync();
                    _eventsService.AddCameraAccessEvent(
                        result.MatchedCardUid,
                        "Allowed known vehicle",
                        result.VehicleDescription,
                        imageDataUrl);
                    await ShowCameraLcdTextAsync(_preferencesService.CameraLcdAllowedText);
                    return;
                }

                if (blockedCards.Contains(result.MatchedCardUid))
                {
                    SaveVehicleDescription(
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        "camera-ai-blocked-match",
                        result.Reason);
                    _eventsService.AddCameraAccessEvent(
                        result.MatchedCardUid,
                        "Denied blocked RFID",
                        result.VehicleDescription,
                        imageDataUrl);
                    await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnknownDeniedText);
                    return;
                }

                _eventsService.AddCameraAccessEvent(
                    result.MatchedCardUid,
                    "Denied matched RFID is not configured",
                    result.VehicleDescription,
                    imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                return;
            }

            if (result.Kind is not VehicleAiRecognitionKind.NewVehicle)
            {
                _eventsService.AddCameraAccessEvent("AI", $"Denied: {result.Reason}", null, imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                return;
            }

            if (!_preferencesService.CameraAiAllowUnknownVehicles)
            {
                _eventsService.AddCameraAccessEvent(
                    "unknown",
                    "Denied unknown vehicle",
                    result.VehicleDescription,
                    imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnknownDeniedText);
                return;
            }

            var existingCards = GetAllConfiguredCards(allowedCards, blockedCards);
            var fakeUid = GenerateFakeRfid(existingCards);
            var addResult = await _commandService.AddAllowedCardAsync(fakeUid);
            if (!addResult.Succeeded)
            {
                _eventsService.AddCameraAccessEvent(
                    fakeUid,
                    "Failed to add fake RFID",
                    result.VehicleDescription,
                    imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdAiUnavailableText);
                return;
            }

            await _commandService.SaveConfigurationAsync();
            await _sessionService.RefreshConfigurationAsync();
            SaveVehicleDescription(
                fakeUid,
                result.VehicleDescription,
                "camera-ai-generated-fake-rfid",
                result.Reason);
            await _commandService.OpenTemporarilyAsync();
            _eventsService.AddCameraAccessEvent(
                fakeUid,
                "Allowed new fake RFID",
                result.VehicleDescription,
                imageDataUrl);
            await ShowCameraLcdTextAsync(_preferencesService.CameraLcdAllowedText);
        }
        finally
        {
            _cameraAttemptGate.Release();
        }
    }

    private async Task EnrichAllowedRfidAsync(string cardUid)
    {
        try
        {
            var imageDataUrl = await _entryCameraService.CaptureFrameDataUrlAsync();
            if (string.IsNullOrWhiteSpace(imageDataUrl))
            {
                return;
            }

            var result = await _vehicleRecognitionAiService.RecognizeAsync(
                imageDataUrl,
                GetKnownProfiles(),
                VehicleAiRecognitionMode.DescribeNewRfidVehicle);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.VehicleDescription))
            {
                return;
            }

            SaveVehicleDescription(
                cardUid,
                result.VehicleDescription,
                "camera-ai-rfid-enrichment",
                result.Reason);
            _eventsService.AddCameraAccessEvent(
                cardUid,
                "RFID description enriched",
                result.VehicleDescription,
                imageDataUrl);
        }
        catch
        {
            // RFID-first enrichment must never block an already allowed card.
        }
    }

    private HashSet<string> GetAllowedCards()
    {
        return (_sessionService.CurrentSession?.Configuration.AllowedCards ?? [])
            .Select(NormalizeUid)
            .Where(uid => uid is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private HashSet<string> GetBlockedCards()
    {
        return (_sessionService.CurrentSession?.Configuration.BlockedCards ?? [])
            .Select(NormalizeUid)
            .Where(uid => uid is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private static HashSet<string> GetAllConfiguredCards(HashSet<string> allowedCards, HashSet<string> blockedCards)
    {
        var allCards = new HashSet<string>(allowedCards, StringComparer.OrdinalIgnoreCase);
        allCards.UnionWith(blockedCards);
        return allCards;
    }

    private IReadOnlyList<VehicleAiKnownProfile> GetKnownProfiles()
    {
        var allowedCards = GetAllowedCards();
        var blockedCards = GetBlockedCards();
        var configuredCards = GetAllConfiguredCards(allowedCards, blockedCards);
        return _memoryStore.GetSmartParkingCardProfiles()
            .Where(profile => configuredCards.Contains(NormalizeUid(profile.CardUid) ?? string.Empty))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.VehicleDescription))
            .Select(profile => new VehicleAiKnownProfile(
                NormalizeUid(profile.CardUid) ?? profile.CardUid,
                profile.VehicleDescription!))
            .ToArray();
    }

    private bool HasVehicleDescription(string cardUid)
    {
        var normalizedUid = NormalizeUid(cardUid);
        return normalizedUid is not null
               && _memoryStore.GetSmartParkingCardProfiles().Any(profile =>
                   string.Equals(NormalizeUid(profile.CardUid), normalizedUid, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(profile.VehicleDescription));
    }

    private void SaveVehicleDescription(
        string cardUid,
        string description,
        string source,
        string aiResult)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        var normalizedUid = NormalizeUid(cardUid) ?? cardUid;
        var profiles = _memoryStore.GetSmartParkingCardProfiles().ToList();
        var index = profiles.FindIndex(profile =>
            string.Equals(NormalizeUid(profile.CardUid), normalizedUid, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            profiles.Add(new SmartParkingCardProfile(
                normalizedUid,
                0,
                0,
                null,
                description,
                DateTimeOffset.UtcNow,
                source,
                IsGeneratedFakeUid(normalizedUid),
                aiResult));
        }
        else
        {
            profiles[index] = profiles[index] with
            {
                CardUid = normalizedUid,
                VehicleDescription = description,
                DescriptionCreatedAt = profiles[index].DescriptionCreatedAt ?? DateTimeOffset.UtcNow,
                DescriptionSource = source,
                IsGeneratedFakeUid = profiles[index].IsGeneratedFakeUid || IsGeneratedFakeUid(normalizedUid),
                LastAiResult = aiResult
            };
        }

        _memoryStore.SetSmartParkingCardProfiles(profiles);
    }

    private async Task ShowCameraLcdTextAsync(string text)
    {
        try
        {
            await _commandService.ShowDisplayMessageAsync(text);
        }
        catch
        {
            // LCD feedback is helpful but must not change the gate decision.
        }
    }

    private static string GenerateFakeRfid(HashSet<string> existingCards)
    {
        Span<byte> randomBytes = stackalloc byte[3];
        for (var attempt = 0; attempt < 32; attempt++)
        {
            RandomNumberGenerator.Fill(randomBytes);
            var uid = $"FA{randomBytes[0]:X2}{randomBytes[1]:X2}{randomBytes[2]:X2}";
            if (!existingCards.Contains(uid))
            {
                return uid;
            }
        }

        return $"FA{DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFFFF:X6}";
    }

    private static bool IsGeneratedFakeUid(string uid)
    {
        return uid.StartsWith("FA", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeUid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = new string(value.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        return compact.Length == 8 ? compact : null;
    }
}
