using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.CameraAi;

public sealed class OpenAiVehicleRecognitionService(
    HttpClient httpClient,
    ISettingsPreferencesService preferencesService) : IVehicleRecognitionAiService
{
    private const string OpenAiModel = "gpt-5-nano";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public async Task<VehicleAiRecognitionResult> RecognizeAsync(
        string imageDataUrl,
        IReadOnlyList<VehicleAiKnownProfile> knownProfiles,
        VehicleAiRecognitionMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!preferencesService.OpenAiUsageEnabled)
        {
            preferencesService.CameraAiLastStatus = "AI usage is disabled.";
            return new VehicleAiRecognitionResult(
                false,
                VehicleAiRecognitionKind.Uncertain,
                null,
                string.Empty,
                "AI usage is disabled.");
        }

        if (string.IsNullOrWhiteSpace(preferencesService.CameraAiApiKey))
        {
            preferencesService.CameraAiLastStatus = "AI key is empty.";
            return new VehicleAiRecognitionResult(false, VehicleAiRecognitionKind.Uncertain, null, string.Empty, "AI key is empty.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preferencesService.CameraAiApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildRequest(imageDataUrl, knownProfiles, mode), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = $"AI request failed: {(int)response.StatusCode}.";
                preferencesService.CameraAiLastStatus = status;
                return new VehicleAiRecognitionResult(false, VehicleAiRecognitionKind.Uncertain, null, string.Empty, status);
            }

            var outputText = ExtractOutputText(responseBody);
            var result = ParseResult(outputText);
            preferencesService.CameraAiLastStatus = result.Succeeded ? "AI request succeeded." : result.Reason;
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            preferencesService.CameraAiLastStatus = $"AI unavailable: {exception.Message}";
            return new VehicleAiRecognitionResult(
                false,
                VehicleAiRecognitionKind.Uncertain,
                null,
                string.Empty,
                "AI service is unavailable.");
        }
    }

    private object BuildRequest(
        string imageDataUrl,
        IReadOnlyList<VehicleAiKnownProfile> knownProfiles,
        VehicleAiRecognitionMode mode)
    {
        var knownText = knownProfiles.Count == 0
            ? "No known vehicles yet."
            : string.Join(
                "\n",
                knownProfiles.Select(profile => $"{profile.CardUid}: {profile.VehicleDescription}"));

        var modeText = mode is VehicleAiRecognitionMode.DescribeNewRfidVehicle
            ? "Mode: DESCRIBE_NEW_RFID_VEHICLE. Describe the visible vehicle for a real RFID card that was already allowed. Do not match it to a known RFID; return NEW_VEHICLE when a vehicle is visible."
            : "Mode: MATCH_CAMERA_VEHICLE_TO_KNOWN_PROFILES. Compare the visible vehicle to known profiles and return MATCH only when confident.";

        var prompt = string.Join(
            "\n",
            "You identify a toy/demo car at a smart parking gate.",
            modeText,
            "Return only JSON with fields: result, matchedCardUid, vehicleDescription, reason.",
            "result must be one of: MATCH, NEW_VEHICLE, NO_VEHICLE, UNCERTAIN.",
            "vehicleDescription should mention distinctive visual features: color, shape, markings, model/toy style, and any visible damage or stickers.",
            "Use NO_VEHICLE when no toy/demo car or vehicle is visible.",
            "Use UNCERTAIN when the image is blurry, dark, partially blocked, or insufficient.",
            "matchedCardUid must be null unless result is MATCH.",
            "Do not invent a known RFID. Use only one of the known RFIDs below.",
            "Known vehicles:",
            knownText);

        return new
        {
            model = OpenAiModel,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "vehicle_ai_recognition",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[]
                        {
                            "result",
                            "matchedCardUid",
                            "vehicleDescription",
                            "reason"
                        },
                        properties = new
                        {
                            result = new
                            {
                                type = "string",
                                @enum = new[] { "MATCH", "NEW_VEHICLE", "NO_VEHICLE", "UNCERTAIN" }
                            },
                            matchedCardUid = new
                            {
                                type = new[] { "string", "null" },
                                description = "Eight hexadecimal RFID characters, or null when there is no confident match."
                            },
                            vehicleDescription = new
                            {
                                type = "string",
                                description = "Distinctive visual vehicle description, or an empty string when no vehicle is visible."
                            },
                            reason = new
                            {
                                type = "string",
                                description = "Short reason for the selected result."
                            }
                        }
                    }
                }
            },
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = prompt },
                        new { type = "input_image", image_url = imageDataUrl }
                    }
                }
            }
        };
    }

    private static VehicleAiRecognitionResult ParseResult(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return new VehicleAiRecognitionResult(
                false,
                VehicleAiRecognitionKind.Uncertain,
                null,
                string.Empty,
                "AI returned empty output.");
        }

        var json = ExtractJsonObject(outputText);
        var payload = JsonSerializer.Deserialize<AiPayload>(json, JsonOptions);
        var description = payload?.VehicleDescription?.Trim() ?? string.Empty;
        var reason = payload?.Reason?.Trim() ?? string.Empty;
        var matchedUid = NormalizeUid(payload?.MatchedCardUid);
        var kind = ParseKind(payload?.Result, matchedUid);
        if (kind is VehicleAiRecognitionKind.NewVehicle && string.IsNullOrWhiteSpace(description))
        {
            kind = VehicleAiRecognitionKind.Uncertain;
            reason = string.IsNullOrWhiteSpace(reason)
                ? "AI marked a new vehicle but did not describe it."
                : reason;
        }

        return new VehicleAiRecognitionResult(
            kind is not VehicleAiRecognitionKind.Uncertain,
            kind,
            matchedUid,
            description,
            string.IsNullOrWhiteSpace(reason) ? "AI response parsed." : reason);
    }

    private static string ExtractOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var builder = new StringBuilder();
        AppendTextNodes(document.RootElement, builder);
        return builder.ToString().Trim();
    }

    private static void AppendTextNodes(JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var type)
                && type.GetString() == "output_text"
                && element.TryGetProperty("text", out var text))
            {
                builder.AppendLine(text.GetString());
                return;
            }

            foreach (var property in element.EnumerateObject())
            {
                AppendTextNodes(property.Value, builder);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AppendTextNodes(item, builder);
            }
        }
    }

    private static string ExtractJsonObject(string value)
    {
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new JsonException("AI output did not contain a JSON object.");
        }

        return value[start..(end + 1)];
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

    private static VehicleAiRecognitionKind ParseKind(string? value, string? matchedUid)
    {
        if (string.Equals(value, "MATCH", StringComparison.OrdinalIgnoreCase) && matchedUid is not null)
        {
            return VehicleAiRecognitionKind.Match;
        }

        if (string.Equals(value, "NEW_VEHICLE", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleAiRecognitionKind.NewVehicle;
        }

        if (string.Equals(value, "NO_VEHICLE", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleAiRecognitionKind.NoVehicle;
        }

        return VehicleAiRecognitionKind.Uncertain;
    }

    private sealed record AiPayload(
        string? Result,
        string? MatchedCardUid,
        string? VehicleDescription,
        string? Reason);
}
