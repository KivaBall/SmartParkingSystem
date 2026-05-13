using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Services.CameraAi;

public sealed class OpenAiVehicleRecognitionService(
    HttpClient httpClient,
    IJSRuntime jsRuntime,
    ISettingsPreferencesService preferencesService) : IVehicleRecognitionAiService
{
    private const string OpenAiModel = "gpt-5-mini";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public async Task<VehicleAiRecognitionResult> RecognizeAsync(
        string imageDataUrl,
        IReadOnlyList<VehicleAiKnownProfile> knownProfiles,
        VehicleAiRecognitionMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!preferencesService.OpenAiUsageEnabled)
        {
            await LogAsync("recognition skipped", new { reason = "AI usage is disabled." });
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
            await LogAsync("recognition skipped", new { reason = "AI key is empty." });
            preferencesService.CameraAiLastStatus = "AI key is empty.";
            return new VehicleAiRecognitionResult(false, VehicleAiRecognitionKind.Uncertain, null, string.Empty, "AI key is empty.");
        }

        if (!IsSupportedImageDataUrl(imageDataUrl))
        {
            await LogAsync("recognition skipped", new
            {
                reason = "AI image is invalid before request.",
                image = DescribeImage(imageDataUrl)
            });
            preferencesService.CameraAiLastStatus = "AI image is invalid before request.";
            return new VehicleAiRecognitionResult(
                false,
                VehicleAiRecognitionKind.Uncertain,
                null,
                string.Empty,
                "AI image is invalid before request.");
        }

        try
        {
            var requestPayload = BuildRequest(imageDataUrl, knownProfiles, mode);
            var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);
            await LogAsync("request prepared", new
            {
                endpoint = "https://api.openai.com/v1/responses",
                model = OpenAiModel,
                mode = mode.ToString(),
                knownProfiles,
                image = DescribeImage(imageDataUrl),
                apiKey = MaskApiKey(preferencesService.CameraAiApiKey),
                requestJsonPreview = RedactImageDataUrl(requestJson)
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preferencesService.CameraAiApiKey);
            request.Content = new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");

            await LogAsync("request sending", new { mode = mode.ToString() });
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            await LogAsync("response received", new
            {
                statusCode = (int)response.StatusCode,
                response.ReasonPhrase,
                responseBody
            });

            if (!response.IsSuccessStatusCode)
            {
                var status = $"AI request failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
                await LogAsync("request failed", new { status, responseBody });
                preferencesService.CameraAiLastStatus = status;
                return new VehicleAiRecognitionResult(false, VehicleAiRecognitionKind.Uncertain, null, string.Empty, status);
            }

            var outputText = ExtractOutputText(responseBody);
            await LogAsync("output text extracted", new { outputText });
            var result = ParseResult(outputText);
            await LogAsync("result parsed", result);
            preferencesService.CameraAiLastStatus = result.Succeeded ? "AI request succeeded." : result.Reason;
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            await LogAsync("exception", new
            {
                exception.GetType().Name,
                exception.Message,
                exception.StackTrace
            });
            preferencesService.CameraAiLastStatus = $"AI unavailable: {exception.Message}";
            return new VehicleAiRecognitionResult(
                false,
                VehicleAiRecognitionKind.Uncertain,
                null,
                string.Empty,
                "AI service is unavailable.");
        }
    }

    private async Task LogAsync(string eventName, object payload)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "console.log",
                $"[SmartParking AI][OpenAI] {eventName}",
                payload);
        }
        catch
        {
            // Browser console logging is diagnostic only.
        }
    }

    private static bool IsSupportedImageDataUrl(string imageDataUrl)
    {
        const string Base64Marker = ";base64,";
        if (string.IsNullOrWhiteSpace(imageDataUrl))
        {
            return false;
        }

        var markerIndex = imageDataUrl.IndexOf(Base64Marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return false;
        }

        var mimeType = imageDataUrl[..markerIndex].Trim();
        if (!string.Equals(mimeType, "data:image/jpeg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mimeType, "data:image/png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mimeType, "data:image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = imageDataUrl[(markerIndex + Base64Marker.Length)..].Trim();
        if (payload.Length < 64)
        {
            return false;
        }

        try
        {
            _ = Convert.FromBase64String(payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
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
                knownProfiles.Select(profile =>
                    $"{profile.CardUid}: number={FormatKnownValue(profile.VehicleNumber)}; description={FormatKnownValue(profile.VehicleDescription)}"));

        var modeText = mode is VehicleAiRecognitionMode.DescribeNewRfidVehicle
            ? "Mode: DESCRIBE_NEW_RFID_VEHICLE. Read the visible vehicle number and/or describe the visible vehicle for a real RFID card that was already processed. Do not match it to a known RFID; return NEW_VEHICLE when a vehicle number or vehicle is visible."
            : "Mode: MATCH_CAMERA_VEHICLE_TO_KNOWN_PROFILES. Compare the visible vehicle number and visible vehicle to known profiles. Return MATCH when the visible number confidently matches a known number, even if the vehicle body is unclear. Also return MATCH when the visible vehicle confidently matches a known description.";

        var prompt = string.Join(
            "\n",
            "You identify a toy/demo car at a smart parking gate.",
            modeText,
            "Return only JSON with fields: result, matchedCardUid, vehicleNumber, vehicleDescription, reason.",
            "result must be one of: MATCH, NEW_VEHICLE, NO_VEHICLE, UNCERTAIN.",
            "vehicleNumber is the visible car number/license/label text from the image, normalized to a short uppercase string, or an empty string when no number is readable.",
            "vehicleDescription should mention distinctive visual features: color, shape, markings, model/toy style, and any visible damage or stickers.",
            "It is valid to return only vehicleNumber with an empty vehicleDescription when only the number is visible.",
            "It is valid to return only vehicleDescription with an empty vehicleNumber when the car is visible but no number is readable.",
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
                            "vehicleNumber",
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
                            vehicleNumber = new
                            {
                                type = "string",
                                description = "Visible car number/license/label text, or an empty string when no readable number is visible."
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

    private static object DescribeImage(string imageDataUrl)
    {
        const string Base64Marker = ";base64,";
        var markerIndex = imageDataUrl.IndexOf(Base64Marker, StringComparison.OrdinalIgnoreCase);
        var mimeType = markerIndex > 0 ? imageDataUrl[..markerIndex] : string.Empty;
        var payloadLength = markerIndex > 0
            ? imageDataUrl.Length - markerIndex - Base64Marker.Length
            : 0;

        return new
        {
            mimeType,
            totalLength = imageDataUrl.Length,
            payloadLength,
            prefix = imageDataUrl[..Math.Min(imageDataUrl.Length, 80)]
        };
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "(empty)";
        }

        var trimmed = apiKey.Trim();
        return trimmed.Length <= 4
            ? "****"
            : $"****{trimmed[^4..]}";
    }

    private static string RedactImageDataUrl(string requestJson)
    {
        const string ImageUrlProperty = "\"image_url\":\"";
        var propertyIndex = requestJson.IndexOf(ImageUrlProperty, StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            return requestJson;
        }

        var valueStart = propertyIndex + ImageUrlProperty.Length;
        var valueEnd = requestJson.IndexOf('"', valueStart);
        if (valueEnd <= valueStart)
        {
            return requestJson;
        }

        var original = requestJson[valueStart..valueEnd];
        var replacement = $"{original[..Math.Min(original.Length, 80)]}... [redacted {original.Length} chars]";
        return requestJson[..valueStart] + replacement + requestJson[valueEnd..];
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
        var vehicleNumber = NormalizeVehicleNumber(payload?.VehicleNumber);
        var reason = payload?.Reason?.Trim() ?? string.Empty;
        var matchedUid = NormalizeUid(payload?.MatchedCardUid);
        var kind = ParseKind(payload?.Result, matchedUid);
        if (kind is VehicleAiRecognitionKind.NewVehicle
            && string.IsNullOrWhiteSpace(description)
            && string.IsNullOrWhiteSpace(vehicleNumber))
        {
            kind = VehicleAiRecognitionKind.Uncertain;
            reason = string.IsNullOrWhiteSpace(reason)
                ? "AI marked a new vehicle but did not return a number or description."
                : reason;
        }

        return new VehicleAiRecognitionResult(
            kind is not VehicleAiRecognitionKind.Uncertain,
            kind,
            matchedUid,
            description,
            string.IsNullOrWhiteSpace(reason) ? "AI response parsed." : reason,
            vehicleNumber);
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
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "output_text"
                && element.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
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

    private static string FormatKnownValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();
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
        string? VehicleNumber,
        string? VehicleDescription,
        string? Reason);
}
