using SmartParkingSystem.TelegramBot.Models.Telegram;
using SmartParkingSystem.TelegramBot.Services.Storage;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramChatSettingsService(ITelegramChatSettingsStore store)
{
    public async Task<IReadOnlyList<TelegramChatSettings>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await store.GetAllChatSettingsAsync(cancellationToken);
    }

    public async Task<TelegramChatSettings> GetOrCreateAsync(long chatId, CancellationToken cancellationToken)
    {
        var existingSettings = await store.GetChatSettingsAsync(chatId, cancellationToken);
        var settings = existingSettings ?? TelegramChatSettings.CreateDefault(chatId);

        if (existingSettings is null)
        {
            await store.SaveChatSettingsAsync(settings, cancellationToken);
        }

        return settings;
    }

    public async Task<TelegramChatSettings> SetLanguageAsync(
        long chatId,
        TelegramChatLanguage language,
        CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(chatId, cancellationToken);
        var updated = current with { Language = language };
        await store.SaveChatSettingsAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TelegramChatSettings> ToggleBotEnabledAsync(long chatId, CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(chatId, cancellationToken);
        var updated = current with { BotEnabled = !current.BotEnabled };
        await store.SaveChatSettingsAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TelegramChatSettings> ToggleNotificationsEnabledAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(chatId, cancellationToken);
        var updated = current with { NotificationsEnabled = !current.NotificationsEnabled };
        await store.SaveChatSettingsAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TelegramChatSettings> ToggleNotificationKindAsync(
        long chatId,
        TelegramNotificationKind kind,
        CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(chatId, cancellationToken);
        var updated = kind switch
        {
            TelegramNotificationKind.Gate => current with
            {
                GateNotificationsEnabled = !current.GateNotificationsEnabled
            },
            TelegramNotificationKind.Parking => current with
            {
                ParkingNotificationsEnabled = !current.ParkingNotificationsEnabled
            },
            TelegramNotificationKind.Monitor => current with
            {
                MonitorNotificationsEnabled = !current.MonitorNotificationsEnabled
            },
            TelegramNotificationKind.Admin => current with
            {
                AdminNotificationsEnabled = !current.AdminNotificationsEnabled
            },
            TelegramNotificationKind.Connection => current with
            {
                ConnectionNotificationsEnabled = !current.ConnectionNotificationsEnabled
            },
            _ => current
        };

        await store.SaveChatSettingsAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TelegramChatSettings> SetLastDeliveredEventIdAsync(
        long chatId,
        string? lastDeliveredEventId,
        CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(chatId, cancellationToken);
        var updated = current with { LastDeliveredEventId = lastDeliveredEventId };
        await store.SaveChatSettingsAsync(updated, cancellationToken);
        return updated;
    }
}
