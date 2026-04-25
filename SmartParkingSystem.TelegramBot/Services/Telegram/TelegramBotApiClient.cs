using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartParkingSystem.TelegramBot.Configuration;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramBotApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramBotOptions> options)
{
    public const string HttpClientName = "TelegramBotApi";

    public async Task<TelegramGetMeResponse?> GetMeAsync(CancellationToken cancellationToken)
    {
        return await SendAsync<TelegramGetMeResponse>("getMe", cancellationToken);
    }

    public async Task<TelegramApiEnvelope<TelegramUpdate[]>?> GetUpdatesAsync(
        long? offset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["offset"] = offset,
            ["timeout"] = timeoutSeconds
        };

        return await SendAsync<TelegramApiEnvelope<TelegramUpdate[]>>(
            "getUpdates",
            cancellationToken,
            payload);
    }

    public async Task<TelegramApiEnvelope<bool>?> DeleteWebhookAsync(CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["drop_pending_updates"] = false
        };

        return await SendAsync<TelegramApiEnvelope<bool>>(
            "deleteWebhook",
            cancellationToken,
            payload);
    }

    public async Task<TelegramApiEnvelope<TelegramSentMessage>?> SendMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken,
        TelegramInlineKeyboardMarkup? replyMarkup = null,
        string? parseMode = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };

        if (!string.IsNullOrWhiteSpace(parseMode))
        {
            payload["parse_mode"] = parseMode;
        }

        if (replyMarkup is not null)
        {
            payload["reply_markup"] = replyMarkup;
        }

        return await SendAsync<TelegramApiEnvelope<TelegramSentMessage>>(
            "sendMessage",
            cancellationToken,
            payload);
    }

    public async Task EditMessageTextAsync(
        long chatId,
        long messageId,
        string text,
        CancellationToken cancellationToken,
        TelegramInlineKeyboardMarkup? replyMarkup = null,
        string? parseMode = null)
    {
        var token = options.Value.BotToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["message_id"] = messageId,
            ["text"] = text
        };

        if (!string.IsNullOrWhiteSpace(parseMode))
        {
            payload["parse_mode"] = parseMode;
        }

        if (replyMarkup is not null)
        {
            payload["reply_markup"] = replyMarkup;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var requestUri = $"https://api.telegram.org/bot{token}/editMessageText";

        using var response = await client.PostAsJsonAsync(requestUri, payload, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<TelegramApiEnvelope<bool>?> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["callback_query_id"] = callbackQueryId
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            payload["text"] = text;
        }

        return await SendAsync<TelegramApiEnvelope<bool>>(
            "answerCallbackQuery",
            cancellationToken,
            payload);
    }

    private async Task<TResponse?> SendAsync<TResponse>(
        string method,
        CancellationToken cancellationToken,
        object? payload = null)
    {
        var token = options.Value.BotToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return default;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var requestUri = $"https://api.telegram.org/bot{token}/{method}";

        if (payload is null)
        {
            return await client.GetFromJsonAsync<TResponse>(requestUri, cancellationToken);
        }

        using var response = await client.PostAsJsonAsync(requestUri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}

public sealed class TelegramGetMeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public TelegramBotIdentity? Result { get; init; }
}

public sealed class TelegramBotIdentity
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = string.Empty;
}

public sealed class TelegramApiEnvelope<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}

public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; init; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; init; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; init; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}

public sealed class TelegramSentMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
}

public sealed class TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramUser From { get; init; } = new TelegramUser();

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}

public sealed class TelegramInlineKeyboardMarkup(TelegramInlineKeyboardButton[][] inlineKeyboard)
{
    [JsonPropertyName("inline_keyboard")]
    public TelegramInlineKeyboardButton[][] InlineKeyboard { get; init; } = inlineKeyboard;
}

public sealed class TelegramInlineKeyboardButton(string text, string callbackData)
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = text;

    [JsonPropertyName("callback_data")]
    public string CallbackData { get; init; } = callbackData;
}
