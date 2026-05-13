using System.Security.Cryptography;
using Microsoft.JSInterop;
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
    private readonly IJSRuntime _jsRuntime;
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
        IJSRuntime jsRuntime,
        IVehicleRecognitionAiService vehicleRecognitionAiService,
        ISettingsPreferencesService preferencesService,
        IAppMemoryStore memoryStore,
        IEventsService eventsService)
    {
        _sessionService = sessionService;
        _commandService = commandService;
        _entryCameraService = entryCameraService;
        _jsRuntime = jsRuntime;
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
        string? rfidToEnrich = null;
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
                if (IsAccessResultEnrichmentCandidate(session.Snapshot.LastAccessResult)
                    && _preferencesService.OpenAiUsageEnabled
                    && _preferencesService.CameraAiCaptureMissingRfidDescriptionsEnabled
                    && !string.IsNullOrWhiteSpace(session.Snapshot.LastAccessUid)
                    && !HasVehicleProfile(session.Snapshot.LastAccessUid))
                {
                    rfidToEnrich = session.Snapshot.LastAccessUid;
                }
            }

            _previousSession = session;
        }

        if (shouldRunCameraAccess)
        {
            _ = RunCameraFirstAccessAsync();
        }

        if (rfidToEnrich is not null)
        {
            _ = EnrichRfidAsync(rfidToEnrich);
        }
    }

    private async Task RunCameraFirstAccessAsync()
    {
        await LogAsync("camera-first access requested", new
        {
            openAiUsageEnabled = _preferencesService.OpenAiUsageEnabled,
            cameraAiAccessScanEnabled = _preferencesService.CameraAiAccessScanEnabled,
            allowUnknownVehicles = _preferencesService.CameraAiAllowUnknownVehicles
        });

        if (!await _cameraAttemptGate.WaitAsync(0))
        {
            await LogAsync("camera-first access skipped", new { reason = "Another camera AI attempt is already running." });
            return;
        }

        string? imageDataUrl = null;
        try
        {
            await LogAsync("capture frame starting", new { flow = "camera-first" });
            imageDataUrl = await _entryCameraService.CaptureFrameDataUrlAsync();
            await LogAsync("capture frame completed", new
            {
                hasImage = !string.IsNullOrWhiteSpace(imageDataUrl),
                imageLength = imageDataUrl?.Length ?? 0,
                _entryCameraService.LastFailureReason
            });

            if (string.IsNullOrWhiteSpace(imageDataUrl))
            {
                var reason = string.IsNullOrWhiteSpace(_entryCameraService.LastFailureReason)
                    ? "Camera unavailable"
                    : _entryCameraService.LastFailureReason;
                await LogAsync("camera-first denied", new { reason });
                _eventsService.AddCameraAccessEvent("camera", reason, null, null);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnavailableText);
                return;
            }

            var knownProfiles = GetKnownProfiles();
            await LogAsync("recognition starting", new
            {
                flow = "camera-first",
                knownProfilesCount = knownProfiles.Count,
                knownProfiles
            });

            var result = await _vehicleRecognitionAiService.RecognizeAsync(
                imageDataUrl,
                knownProfiles,
                VehicleAiRecognitionMode.MatchCameraVehicleToKnownProfiles);
            await LogAsync("recognition completed", result);

            if (!result.Succeeded || result.Kind is VehicleAiRecognitionKind.Uncertain)
            {
                await LogAsync("camera-first denied", new
                {
                    reason = result.Reason,
                    result.Kind,
                    result.Succeeded
                });
                _eventsService.AddCameraAccessEvent("AI", $"Denied: {result.Reason}", null, imageDataUrl);
                await ShowCameraLcdTextAsync(
                    result.Succeeded
                        ? _preferencesService.CameraLcdUnrecognizedText
                        : _preferencesService.CameraLcdAiUnavailableText);
                return;
            }

            var allowedCards = GetAllowedCards();
            var blockedCards = GetBlockedCards();
            await LogAsync("configured cards loaded", new
            {
                allowedCards,
                blockedCards
            });

            if (result.Kind is VehicleAiRecognitionKind.NoVehicle)
            {
                await LogAsync("camera-first denied", new { reason = "No vehicle in frame.", result.VehicleDescription });
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
                    await LogAsync("camera-first denied", new { reason = "Matched RFID is missing." });
                    _eventsService.AddCameraAccessEvent("AI", "Denied: matched RFID is missing", null, imageDataUrl);
                    await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                    return;
                }

                if (allowedCards.Contains(result.MatchedCardUid))
                {
                    await LogAsync("camera-first allowed", new
                    {
                        reason = "Known allowed vehicle matched.",
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        result.VehicleNumber
                    });
                    SaveVehicleProfile(
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        result.VehicleNumber,
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
                    await LogAsync("camera-first denied", new
                    {
                        reason = "Matched RFID is blocked.",
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        result.VehicleNumber
                    });
                    SaveVehicleProfile(
                        result.MatchedCardUid,
                        result.VehicleDescription,
                        result.VehicleNumber,
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

                await LogAsync("camera-first denied", new
                {
                    reason = "Matched RFID is not configured.",
                    result.MatchedCardUid,
                    result.VehicleDescription
                });
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
                await LogAsync("camera-first denied", new { reason = result.Reason, result.Kind });
                _eventsService.AddCameraAccessEvent("AI", $"Denied: {result.Reason}", null, imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdUnrecognizedText);
                return;
            }

            if (!_preferencesService.CameraAiAllowUnknownVehicles)
            {
                await LogAsync("camera-first denied", new
                {
                    reason = "Unknown vehicles are not allowed.",
                    result.VehicleDescription
                });
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
            await LogAsync("generated fake RFID", new { fakeUid, existingCards });
            var addResult = await _commandService.AddAllowedCardAsync(fakeUid);
            await LogAsync("add fake RFID command completed", addResult);
            if (!addResult.Succeeded)
            {
                await LogAsync("camera-first failed", new { reason = "Failed to add fake RFID.", fakeUid, addResult });
                _eventsService.AddCameraAccessEvent(
                    fakeUid,
                    "Failed to add fake RFID",
                    result.VehicleDescription,
                    imageDataUrl);
                await ShowCameraLcdTextAsync(_preferencesService.CameraLcdAiUnavailableText);
                return;
            }

            await _commandService.SaveConfigurationAsync();
            await LogAsync("save configuration command completed", new { fakeUid });
            await _sessionService.RefreshConfigurationAsync();
            await LogAsync("configuration refreshed", new { fakeUid });
            SaveVehicleProfile(
                fakeUid,
                result.VehicleDescription,
                result.VehicleNumber,
                "camera-ai-generated-fake-rfid",
                result.Reason);
            await _commandService.OpenTemporarilyAsync();
            await LogAsync("camera-first allowed", new
            {
                reason = "New unknown vehicle allowed with generated fake RFID.",
                fakeUid,
                result.VehicleDescription,
                result.VehicleNumber
            });
            _eventsService.AddCameraAccessEvent(
                fakeUid,
                "Allowed new fake RFID",
                result.VehicleDescription,
                imageDataUrl);
            await ShowCameraLcdTextAsync(_preferencesService.CameraLcdAllowedText);
        }
        finally
        {
            await LogAsync("camera-first access finished", new { hadImage = imageDataUrl is not null });
            _cameraAttemptGate.Release();
        }
    }

    private async Task EnrichRfidAsync(string cardUid)
    {
        try
        {
            await LogAsync("RFID enrichment requested", new { cardUid });
            var imageDataUrl = await _entryCameraService.CaptureFrameDataUrlAsync();
            await LogAsync("RFID enrichment capture completed", new
            {
                cardUid,
                hasImage = !string.IsNullOrWhiteSpace(imageDataUrl),
                imageLength = imageDataUrl?.Length ?? 0,
                _entryCameraService.LastFailureReason
            });

            if (string.IsNullOrWhiteSpace(imageDataUrl))
            {
                return;
            }

            var result = await _vehicleRecognitionAiService.RecognizeAsync(
                imageDataUrl,
                GetKnownProfiles(),
                VehicleAiRecognitionMode.DescribeNewRfidVehicle);
            await LogAsync("RFID enrichment recognition completed", new { cardUid, result });
            if (!result.Succeeded
                || (string.IsNullOrWhiteSpace(result.VehicleDescription)
                    && string.IsNullOrWhiteSpace(result.VehicleNumber)))
            {
                return;
            }

            SaveVehicleProfile(
                cardUid,
                result.VehicleDescription,
                result.VehicleNumber,
                "camera-ai-rfid-enrichment",
                result.Reason);
            _eventsService.AddCameraAccessEvent(
                cardUid,
                "RFID description enriched",
                result.VehicleDescription,
                imageDataUrl);
            await LogAsync("RFID enrichment saved", new
            {
                cardUid,
                result.VehicleDescription,
                result.VehicleNumber,
                result.Reason
            });
        }
        catch (Exception exception)
        {
            await LogAsync("RFID enrichment exception", new
            {
                cardUid,
                exception.GetType().Name,
                exception.Message,
                exception.StackTrace
            });
        }
    }

    private static bool IsAccessResultEnrichmentCandidate(string? accessResult)
    {
        return string.Equals(accessResult, "ALLOWED", StringComparison.OrdinalIgnoreCase)
               || string.Equals(accessResult, "BLOCKED", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogAsync(string eventName, object payload)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "console.log",
                $"[SmartParking AI][Flow] {eventName}",
                payload);
        }
        catch
        {
            // Browser console logging is diagnostic only.
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
            .Where(profile => !string.IsNullOrWhiteSpace(profile.VehicleDescription)
                              || !string.IsNullOrWhiteSpace(profile.VehicleNumber))
            .Select(profile => new VehicleAiKnownProfile(
                NormalizeUid(profile.CardUid) ?? profile.CardUid,
                profile.VehicleDescription ?? string.Empty,
                profile.VehicleNumber ?? string.Empty))
            .ToArray();
    }

    private bool HasVehicleProfile(string cardUid)
    {
        var normalizedUid = NormalizeUid(cardUid);
        return normalizedUid is not null
               && _memoryStore.GetSmartParkingCardProfiles().Any(profile =>
                   string.Equals(NormalizeUid(profile.CardUid), normalizedUid, StringComparison.OrdinalIgnoreCase)
                   && (!string.IsNullOrWhiteSpace(profile.VehicleDescription)
                       || !string.IsNullOrWhiteSpace(profile.VehicleNumber)));
    }

    private void SaveVehicleProfile(
        string cardUid,
        string description,
        string vehicleNumber,
        string source,
        string aiResult)
    {
        var normalizedNumber = NormalizeVehicleNumber(vehicleNumber);
        if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(normalizedNumber))
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
                aiResult,
                normalizedNumber));
        }
        else
        {
            profiles[index] = profiles[index] with
            {
                CardUid = normalizedUid,
                VehicleDescription = string.IsNullOrWhiteSpace(description)
                    ? profiles[index].VehicleDescription
                    : description,
                VehicleNumber = string.IsNullOrWhiteSpace(normalizedNumber)
                    ? profiles[index].VehicleNumber
                    : normalizedNumber,
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
}
