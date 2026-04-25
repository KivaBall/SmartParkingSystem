using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.TelegramBot.Models.Telegram;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramNotificationService(
    TelegramBotApiClient apiClient,
    TelegramChatAuthorizationService authorizationService,
    TelegramChatSettingsService chatSettingsService,
    TelegramMenuService menuService)
{
    public async Task NotifyAsync(BackendDeviceStatePayload payload, CancellationToken cancellationToken)
    {
        if (payload.RecentEvents.Count == 0)
        {
            return;
        }

        var orderedEvents = payload.RecentEvents
            .OrderBy(static item => item.CreatedAt)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .ToArray();

        var latestEventId = orderedEvents[^1].Id;
        var chats = await chatSettingsService.GetAllAsync(cancellationToken);

        foreach (var chat in chats.Where(static settings => settings.BotEnabled && settings.NotificationsEnabled))
        {
            if (!authorizationService.IsAllowed(chat.ChatId))
            {
                continue;
            }

            var language = chat.Language ?? TelegramChatLanguage.Ukrainian;

            if (string.IsNullOrWhiteSpace(chat.LastDeliveredEventId))
            {
                await chatSettingsService.SetLastDeliveredEventIdAsync(chat.ChatId, latestEventId, cancellationToken);
                continue;
            }

            var pendingEvents = GetPendingEvents(orderedEvents, chat.LastDeliveredEventId);
            if (pendingEvents.Length == 0)
            {
                if (!string.Equals(chat.LastDeliveredEventId, latestEventId, StringComparison.Ordinal))
                {
                    await chatSettingsService.SetLastDeliveredEventIdAsync(
                        chat.ChatId,
                        latestEventId,
                        cancellationToken);
                }

                continue;
            }

            var filteredEvents = pendingEvents
                .Where(eventItem => IsEnabledForChat(chat, eventItem))
                .ToArray();

            try
            {
                foreach (var eventItem in filteredEvents)
                {
                    await apiClient.SendMessageAsync(
                        chat.ChatId,
                        menuService.BuildNotificationText(eventItem, language),
                        cancellationToken,
                        parseMode: "HTML");
                }

                await chatSettingsService.SetLastDeliveredEventIdAsync(chat.ChatId, latestEventId, cancellationToken);
            }
            catch
            {
                // Keep the cursor unchanged so the next sync can retry delivery.
            }
        }
    }

    private static EventFeedItem[] GetPendingEvents(
        EventFeedItem[] orderedEvents,
        string? lastDeliveredEventId)
    {
        if (string.IsNullOrWhiteSpace(lastDeliveredEventId))
        {
            return orderedEvents;
        }

        var lastDeliveredIndex = -1;
        for (var index = 0; index < orderedEvents.Length; index++)
        {
            if (string.Equals(orderedEvents[index].Id, lastDeliveredEventId, StringComparison.Ordinal))
            {
                lastDeliveredIndex = index;
                break;
            }
        }

        if (lastDeliveredIndex < 0)
        {
            return orderedEvents;
        }

        if (lastDeliveredIndex >= orderedEvents.Length - 1)
        {
            return [];
        }

        return orderedEvents.Skip(lastDeliveredIndex + 1).ToArray();
    }

    private static bool IsEnabledForChat(TelegramChatSettings settings, EventFeedItem eventItem)
    {
        var kind = eventItem.Kind switch
        {
            EventKind.GateOpenAngleChanged
                or EventKind.GateClosedAngleChanged
                or EventKind.GateOpenDurationChanged
                or EventKind.ConnectionIntervalChanged
                or EventKind.ParkingThresholdChanged
                or EventKind.AllowedCardsChanged
                or EventKind.BlockedCardsChanged => TelegramNotificationKind.Admin,
            EventKind.ControllerConnected or EventKind.ControllerDisconnected => TelegramNotificationKind.Connection,
            _ => eventItem.Category switch
            {
                EventCategory.Gate => TelegramNotificationKind.Gate,
                EventCategory.Parking => TelegramNotificationKind.Parking,
                EventCategory.Monitor => TelegramNotificationKind.Monitor,
                EventCategory.Connection => TelegramNotificationKind.Connection,
                EventCategory.System => TelegramNotificationKind.Admin,
                _ => TelegramNotificationKind.Admin
            }
        };

        return kind switch
        {
            TelegramNotificationKind.Gate => settings.GateNotificationsEnabled,
            TelegramNotificationKind.Parking => settings.ParkingNotificationsEnabled,
            TelegramNotificationKind.Monitor => settings.MonitorNotificationsEnabled,
            TelegramNotificationKind.Admin => settings.AdminNotificationsEnabled,
            TelegramNotificationKind.Connection => settings.ConnectionNotificationsEnabled,
            _ => false
        };
    }
}
