using SmartParkingSystem.Domain.Models.BackendCommands;
using SmartParkingSystem.TelegramBot.Models.Telegram;
using SmartParkingSystem.TelegramBot.Services.Commands;
using SmartParkingSystem.TelegramBot.Services.DeviceState;
using SmartParkingSystem.TelegramBot.Services.Storage;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramBotUpdateHandler(
    TelegramBotApiClient apiClient,
    TelegramChatAuthorizationService authorizationService,
    TelegramChatSettingsService chatSettingsService,
    TelegramMenuService menuService,
    BackendCommandDispatchService commandDispatchService,
    DeviceStateCache deviceStateCache,
    IDeviceStateStore deviceStateStore)
{
    public async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!TryResolveChatId(update, out var chatId) || !authorizationService.IsAllowed(chatId))
        {
            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackAsync(update.CallbackQuery, cancellationToken);
            return;
        }

        if (update.Message is not null)
        {
            await HandleMessageAsync(update.Message, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var chat = message.Chat;
        var text = message.Text?.Trim();

        if (chat is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var settings = await chatSettingsService.GetOrCreateAsync(chat.Id, cancellationToken);
        if (settings.Language is null)
        {
            var languagePicker = menuService.BuildLanguagePicker();
            await apiClient.SendMessageAsync(
                chat.Id,
                languagePicker.Text,
                cancellationToken,
                languagePicker.Markup,
                "HTML");
            return;
        }

        var command = NormalizeCommand(text);

        switch (command)
        {
            case "/start":
                await ShowMainMenuAsync(chat.Id, null, settings, cancellationToken);
                break;
            default:
                await ShowMainMenuAsync(chat.Id, null, settings, cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackAsync(TelegramCallbackQuery callback, CancellationToken cancellationToken)
    {
        var chatId = callback.Message?.Chat?.Id ?? callback.From.Id;
        var messageId = callback.Message?.MessageId;
        var data = callback.Data?.Trim();
        if (string.IsNullOrWhiteSpace(data))
        {
            await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
            return;
        }

        var settings = await chatSettingsService.GetOrCreateAsync(chatId, cancellationToken);

        switch (data)
        {
            case "lang:uk":
                settings = await chatSettingsService.SetLanguageAsync(
                    chatId,
                    TelegramChatLanguage.Ukrainian,
                    cancellationToken);
                await apiClient.AnswerCallbackQueryAsync(
                    callback.Id,
                    menuService.BuildLanguageAppliedText(TelegramChatLanguage.Ukrainian),
                    cancellationToken);
                await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                break;
            case "lang:en":
                settings = await chatSettingsService.SetLanguageAsync(
                    chatId,
                    TelegramChatLanguage.English,
                    cancellationToken);
                await apiClient.AnswerCallbackQueryAsync(
                    callback.Id,
                    menuService.BuildLanguageAppliedText(TelegramChatLanguage.English),
                    cancellationToken);
                await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                break;
            case "menu:toggle-enabled":
                settings = await chatSettingsService.ToggleBotEnabledAsync(chatId, cancellationToken);
                await apiClient.AnswerCallbackQueryAsync(
                    callback.Id,
                    menuService.BuildBotEnabledToggledText(
                        settings.BotEnabled,
                        settings.Language ?? TelegramChatLanguage.Ukrainian),
                    cancellationToken);
                await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                break;
            case "menu:main":
                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "menu:settings":
                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowSettingsMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "settings:language":
                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowLanguageMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "settings:toggle-notifications":
                settings = await chatSettingsService.ToggleNotificationsEnabledAsync(chatId, cancellationToken);
                await apiClient.AnswerCallbackQueryAsync(
                    callback.Id,
                    menuService.BuildNotificationsToggledText(
                        settings.NotificationsEnabled,
                        settings.Language ?? TelegramChatLanguage.Ukrainian),
                    cancellationToken);
                await ShowSettingsMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "settings:notifications":
                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowNotificationSettingsMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "menu:commands":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowCommandsMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            case "cmd:status":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowTextAsync(
                    chatId,
                    messageId,
                    await BuildStatusTextAsync(settings.Language ?? TelegramChatLanguage.Ukrainian, cancellationToken),
                    menuService.BuildCommandsBackMarkup(settings.Language ?? TelegramChatLanguage.Ukrainian),
                    cancellationToken,
                    "HTML");
                return;
            case "cmd:events":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowTextAsync(
                    chatId,
                    messageId,
                    await BuildEventsTextAsync(settings.Language ?? TelegramChatLanguage.Ukrainian, cancellationToken),
                    menuService.BuildCommandsBackMarkup(settings.Language ?? TelegramChatLanguage.Ukrainian),
                    cancellationToken,
                    "HTML");
                return;
            case "cmd:parking-slots":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
                await ShowParkingSlotsMenuAsync(
                    chatId,
                    messageId,
                    settings.Language ?? TelegramChatLanguage.Ukrainian,
                    cancellationToken);
                return;
            case "cmd:refresh":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await HandleBackendCommandAsync(
                    callback.Id,
                    chatId,
                    messageId,
                    BackendCommandKind.RefreshState,
                    settings,
                    cancellationToken);
                return;
            case "cmd:gate-force-open":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await HandleBackendCommandAsync(
                    callback.Id,
                    chatId,
                    messageId,
                    BackendCommandKind.ForceOpenGate,
                    settings,
                    cancellationToken);
                return;
            case "cmd:gate-open-temp":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await HandleBackendCommandAsync(
                    callback.Id,
                    chatId,
                    messageId,
                    BackendCommandKind.OpenGateTemporarily,
                    settings,
                    cancellationToken);
                return;
            case "cmd:gate-close":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await HandleBackendCommandAsync(
                    callback.Id,
                    chatId,
                    messageId,
                    BackendCommandKind.CloseGate,
                    settings,
                    cancellationToken);
                return;
            case "cmd:gate-lock-toggle":
                if (!settings.BotEnabled)
                {
                    await apiClient.AnswerCallbackQueryAsync(
                        callback.Id,
                        menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                        cancellationToken);
                    await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                    return;
                }

                await HandleBackendCommandAsync(
                    callback.Id,
                    chatId,
                    messageId,
                    BackendCommandKind.ToggleGateLock,
                    settings,
                    cancellationToken);
                return;
        }

        if (TryParseNotificationToggle(data, out var kind))
        {
            settings = await chatSettingsService.ToggleNotificationKindAsync(chatId, kind, cancellationToken);
            var language = settings.Language ?? TelegramChatLanguage.Ukrainian;
            var enabled = IsNotificationEnabled(settings, kind);
            await apiClient.AnswerCallbackQueryAsync(
                callback.Id,
                menuService.BuildNotificationKindToggledText(kind, enabled, language),
                cancellationToken);
            await ShowNotificationSettingsMenuAsync(chatId, messageId, settings, cancellationToken);
            return;
        }

        if (TryParseParkingSlotToggle(data, out var slotId))
        {
            if (!settings.BotEnabled)
            {
                await apiClient.AnswerCallbackQueryAsync(
                    callback.Id,
                    menuService.BuildBotDisabledText(settings.Language ?? TelegramChatLanguage.Ukrainian),
                    cancellationToken);
                await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
                return;
            }

            await HandleBackendCommandAsync(
                callback.Id,
                chatId,
                messageId,
                BackendCommandKind.ToggleParkingSlot,
                settings,
                cancellationToken,
                slotId);
            return;
        }

        await apiClient.AnswerCallbackQueryAsync(callback.Id, null, cancellationToken);
        await ShowMainMenuAsync(chatId, messageId, settings, cancellationToken);
    }

    private async Task HandleBackendCommandAsync(
        string callbackId,
        long chatId,
        long? messageId,
        BackendCommandKind kind,
        TelegramChatSettings settings,
        CancellationToken cancellationToken,
        string? argument = null)
    {
        await apiClient.AnswerCallbackQueryAsync(callbackId, null, cancellationToken);

        var result = await commandDispatchService.DispatchAsync(kind, argument, cancellationToken);
        var language = settings.Language ?? TelegramChatLanguage.Ukrainian;

        await ShowTextAsync(
            chatId,
            messageId,
            menuService.BuildBackendCommandResultText(result.Message, result.Succeeded, language),
            menuService.BuildCommandsBackMarkup(language),
            cancellationToken,
            "HTML");
    }

    private async Task ShowMainMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatSettings settings,
        CancellationToken cancellationToken)
    {
        var menu = menuService.BuildMainMenu(settings);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowSettingsMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatSettings settings,
        CancellationToken cancellationToken)
    {
        var menu = menuService.BuildSettingsMenu(settings);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowLanguageMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatSettings settings,
        CancellationToken cancellationToken)
    {
        var menu = menuService.BuildLanguageMenu(settings);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowNotificationSettingsMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatSettings settings,
        CancellationToken cancellationToken)
    {
        var menu = menuService.BuildNotificationSettingsMenu(settings);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowCommandsMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatSettings settings,
        CancellationToken cancellationToken)
    {
        var menu = menuService.BuildCommandsMenu(settings);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowParkingSlotsMenuAsync(
        long chatId,
        long? messageId,
        TelegramChatLanguage language,
        CancellationToken cancellationToken)
    {
        var snapshot = deviceStateCache.CurrentSnapshot
                       ?? await deviceStateStore.GetLatestSnapshotAsync(cancellationToken);

        var menu = menuService.BuildParkingSlotsMenu(snapshot?.ParkingSlots ?? [], language);
        await ShowMenuAsync(chatId, messageId, menu.Text, menu.Markup, cancellationToken);
    }

    private async Task ShowMenuAsync(
        long chatId,
        long? messageId,
        string text,
        TelegramInlineKeyboardMarkup markup,
        CancellationToken cancellationToken)
    {
        const string menuParseMode = "HTML";

        if (messageId.HasValue)
        {
            await apiClient.EditMessageTextAsync(
                chatId,
                messageId.Value,
                text,
                cancellationToken,
                markup,
                menuParseMode);
            return;
        }

        await apiClient.SendMessageAsync(chatId, text, cancellationToken, markup, menuParseMode);
    }

    private async Task ShowTextAsync(
        long chatId,
        long? messageId,
        string text,
        TelegramInlineKeyboardMarkup? markup,
        CancellationToken cancellationToken,
        string? parseMode = null)
    {
        if (messageId.HasValue)
        {
            await apiClient.EditMessageTextAsync(
                chatId,
                messageId.Value,
                text,
                cancellationToken,
                markup,
                parseMode);
            return;
        }

        await apiClient.SendMessageAsync(chatId, text, cancellationToken, markup, parseMode);
    }

    private async Task<string> BuildStatusTextAsync(TelegramChatLanguage language, CancellationToken cancellationToken)
    {
        var snapshot = deviceStateCache.CurrentSnapshot
                       ?? await deviceStateStore.GetLatestSnapshotAsync(cancellationToken);

        return snapshot is null
            ? menuService.BuildNoSnapshotText(language)
            : menuService.BuildStatusText(snapshot, language);
    }

    private async Task<string> BuildEventsTextAsync(TelegramChatLanguage language, CancellationToken cancellationToken)
    {
        var snapshot = deviceStateCache.CurrentSnapshot
                       ?? await deviceStateStore.GetLatestSnapshotAsync(cancellationToken);

        return snapshot is null
            ? menuService.BuildNoSnapshotText(language)
            : menuService.BuildEventsText(snapshot.RecentEvents, language);
    }

    private static string NormalizeCommand(string rawText)
    {
        var firstToken = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var atIndex = firstToken.IndexOf('@');
        return atIndex > 0 ? firstToken[..atIndex].ToLowerInvariant() : firstToken.ToLowerInvariant();
    }

    private static bool TryParseNotificationToggle(string data, out TelegramNotificationKind kind)
    {
        switch (data)
        {
            case "settings:toggle:gate":
                kind = TelegramNotificationKind.Gate;
                return true;
            case "settings:toggle:parking":
                kind = TelegramNotificationKind.Parking;
                return true;
            case "settings:toggle:monitor":
                kind = TelegramNotificationKind.Monitor;
                return true;
            case "settings:toggle:admin":
                kind = TelegramNotificationKind.Admin;
                return true;
            case "settings:toggle:connection":
                kind = TelegramNotificationKind.Connection;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryParseParkingSlotToggle(string data, out string? slotId)
    {
        slotId = null;
        const string prefix = "cmd:parking:toggle:";
        if (!data.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parsedSlotId = data[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(parsedSlotId))
        {
            return false;
        }

        slotId = parsedSlotId;
        return true;
    }

    private static bool IsNotificationEnabled(TelegramChatSettings settings, TelegramNotificationKind kind)
    {
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

    private static bool TryResolveChatId(TelegramUpdate update, out long chatId)
    {
        chatId = update.CallbackQuery?.Message?.Chat?.Id
                 ?? update.CallbackQuery?.From.Id
                 ?? update.Message?.Chat?.Id
                 ?? update.Message?.From?.Id
                 ?? 0;

        return chatId != 0;
    }
}
