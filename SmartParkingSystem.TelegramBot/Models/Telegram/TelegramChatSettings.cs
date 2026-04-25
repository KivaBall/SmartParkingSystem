namespace SmartParkingSystem.TelegramBot.Models.Telegram;

public sealed record TelegramChatSettings(
    long ChatId,
    TelegramChatLanguage? Language,
    bool BotEnabled,
    bool NotificationsEnabled,
    bool GateNotificationsEnabled,
    bool ParkingNotificationsEnabled,
    bool MonitorNotificationsEnabled,
    bool AdminNotificationsEnabled,
    bool ConnectionNotificationsEnabled,
    string? LastDeliveredEventId)
{
    public static TelegramChatSettings CreateDefault(long chatId)
    {
        return new TelegramChatSettings(
            chatId,
            null,
            false,
            true,
            true,
            true,
            false,
            false,
            false,
            null);
    }
}
