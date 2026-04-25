namespace SmartParkingSystem.TelegramBot.Configuration;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string BotToken { get; set; } = string.Empty;

    public bool UseWebhook { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;

    public int PollingTimeoutSeconds { get; set; } = 25;

    public string[] AllowedChatIds { get; set; } = [];
}