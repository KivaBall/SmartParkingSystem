using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Gate;
using SmartParkingSystem.Domain.Models.Parking;
using SmartParkingSystem.TelegramBot.Models.Telegram;
using System.Net;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramMenuService
{
    private static readonly TimeZoneInfo KyivTimeZone = ResolveKyivTimeZone();

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildLanguagePicker()
    {
        return (
            "<b>Choose a language / Обери мову</b>",
            new TelegramInlineKeyboardMarkup(
            [
                [new TelegramInlineKeyboardButton("🇺🇦 Українська", "lang:uk")],
                [new TelegramInlineKeyboardButton("🇬🇧 English", "lang:en")]
            ]));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildMainMenu(TelegramChatSettings settings)
    {
        var language = GetLanguageOrDefault(settings);
        var enableButtonText = settings.BotEnabled
            ? Localize(language, "🔴 Вимкнути", "🔴 Disable")
            : Localize(language, "🟢 Увімкнути", "🟢 Enable");
        var notificationsState = settings.NotificationsEnabled ? "🟢" : "🔴";

        var title = Localize(language, "Система Розумного Паркування", "Smart Parking System");
        var notificationsLine = Localize(language, "Сповіщення", "Notifications");
        var text = settings.BotEnabled
            ? $"<b>{title}</b>\n\n{notificationsLine}: {notificationsState}"
            : $"<b>{title}</b>";

        TelegramInlineKeyboardButton[][] rows = settings.BotEnabled
            ? new[]
            {
                new[] { new TelegramInlineKeyboardButton(enableButtonText, "menu:toggle-enabled") },
                new[]
                {
                    new TelegramInlineKeyboardButton(Localize(language, "⌨️ Команди", "⌨️ Commands"), "menu:commands")
                },
                new[]
                {
                    new TelegramInlineKeyboardButton(
                        Localize(language, "⚙️ Налаштування", "⚙️ Settings"),
                        "menu:settings")
                }
            }
            : new[]
            {
                new[] { new TelegramInlineKeyboardButton(enableButtonText, "menu:toggle-enabled") }
            };

        return (
            text,
            new TelegramInlineKeyboardMarkup(rows));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildSettingsMenu(TelegramChatSettings settings)
    {
        var language = GetLanguageOrDefault(settings);
        var notificationsText = settings.NotificationsEnabled
            ? Localize(language, "🔕 Вимкнути сповіщення", "🔕 Disable notifications")
            : Localize(language, "🔔 Увімкнути сповіщення", "🔔 Enable notifications");

        return (
            $"<b>{Localize(language, "Налаштування", "Settings")}</b>",
            new TelegramInlineKeyboardMarkup(
            [
                [new TelegramInlineKeyboardButton(Localize(language, "🌐 Мова", "🌐 Language"), "settings:language")],
                [new TelegramInlineKeyboardButton(notificationsText, "settings:toggle-notifications")],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🔔 Типи сповіщень", "🔔 Notification types"),
                        "settings:notifications")
                ],
                [new TelegramInlineKeyboardButton(Localize(language, "⬅️ Назад", "⬅️ Back"), "menu:main")]
            ]));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildLanguageMenu(TelegramChatSettings settings)
    {
        var language = GetLanguageOrDefault(settings);

        return (
            $"<b>{Localize(language, "Вибір мови", "Language selection")}</b>",
            new TelegramInlineKeyboardMarkup(
            [
                [new TelegramInlineKeyboardButton("🇺🇦 Українська", "lang:uk")],
                [new TelegramInlineKeyboardButton("🇬🇧 English", "lang:en")],
                [new TelegramInlineKeyboardButton(Localize(language, "⬅️ Назад", "⬅️ Back"), "menu:settings")]
            ]));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildNotificationSettingsMenu(
        TelegramChatSettings settings)
    {
        var language = GetLanguageOrDefault(settings);

        return (
            $"<b>{Localize(language, "Типи сповіщень", "Notification types")}</b>",
            new TelegramInlineKeyboardMarkup(
            [
                [
                    BuildToggleButton(
                        settings.GateNotificationsEnabled,
                        Localize(language, "🚪 Ворота", "🚪 Gate"),
                        "settings:toggle:gate")
                ],
                [
                    BuildToggleButton(
                        settings.ParkingNotificationsEnabled,
                        Localize(language, "🅿️ Паркування", "🅿️ Parking"),
                        "settings:toggle:parking")
                ],
                [
                    BuildToggleButton(
                        settings.MonitorNotificationsEnabled,
                        Localize(language, "📟 Монітор", "📟 Monitor"),
                        "settings:toggle:monitor")
                ],
                [
                    BuildToggleButton(
                        settings.AdminNotificationsEnabled,
                        Localize(language, "🛠 Адмін", "🛠 Admin"),
                        "settings:toggle:admin")
                ],
                [
                    BuildToggleButton(
                        settings.ConnectionNotificationsEnabled,
                        Localize(language, "🔌 Підключення", "🔌 Connection"),
                        "settings:toggle:connection")
                ],
                [new TelegramInlineKeyboardButton(Localize(language, "⬅️ Назад", "⬅️ Back"), "menu:settings")]
            ]));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildCommandsMenu(TelegramChatSettings settings)
    {
        var language = GetLanguageOrDefault(settings);

        return (
            $"<b>{Localize(language, "Команди", "Commands")}</b>",
            new TelegramInlineKeyboardMarkup(
            [
                [new TelegramInlineKeyboardButton(Localize(language, "📊 Статус", "📊 Status"), "cmd:status")],
                [new TelegramInlineKeyboardButton(Localize(language, "📋 Події", "📋 Events"), "cmd:events")],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🔄 Оновити стан", "🔄 Refresh state"),
                        "cmd:refresh")
                ],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🅿️ Паркомісця", "🅿️ Parking slots"),
                        "cmd:parking-slots")
                ],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🟢 Примусово відкрити", "🟢 Force open"),
                        "cmd:gate-force-open")
                ],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🚪 Відкрити ворота", "🚪 Open gate"),
                        "cmd:gate-open-temp")
                ],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🚪 Закрити ворота", "🚪 Close gate"),
                        "cmd:gate-close")
                ],
                [
                    new TelegramInlineKeyboardButton(
                        Localize(language, "🔒 Змінити блокування", "🔒 Toggle lock"),
                        "cmd:gate-lock-toggle")
                ],
                [new TelegramInlineKeyboardButton(Localize(language, "⬅️ Назад", "⬅️ Back"), "menu:main")]
            ]));
    }

    public (string Text, TelegramInlineKeyboardMarkup Markup) BuildParkingSlotsMenu(
        IReadOnlyList<ParkingSlotSnapshot> parkingSlots,
        TelegramChatLanguage language)
    {
        if (parkingSlots.Count == 0)
        {
            return (
                $"<b>{Localize(language, "Паркомісця", "Parking slots")}</b>\n\n{
                    Localize(language, "Дані по паркомісцях поки недоступні.", "Parking slot data is not available yet.")}",
                new TelegramInlineKeyboardMarkup(
                [
                    [
                        new TelegramInlineKeyboardButton(
                            Localize(language, "⬅️ До команд", "⬅️ Back to commands"),
                            "menu:commands")
                    ]
                ]));
        }

        var rows = new List<TelegramInlineKeyboardButton[]>();

        foreach (var slot in parkingSlots.OrderBy(static item => item.Label, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(
            [
                new TelegramInlineKeyboardButton(
                    $"{GetParkingSlotEmoji(slot.State)} {slot.Label} · {LocalizeParkingSlotState(slot.State, language)
                    }",
                    $"cmd:parking:toggle:{slot.Id}")
            ]);
        }

        rows.Add(
        [
            new TelegramInlineKeyboardButton(Localize(language, "⬅️ До команд", "⬅️ Back to commands"), "menu:commands")
        ]);

        return (
            $"<b>{Localize(language, "Паркомісця", "Parking slots")}</b>",
            new TelegramInlineKeyboardMarkup([.. rows]));
    }

    public TelegramInlineKeyboardMarkup BuildCommandsBackMarkup(TelegramChatLanguage language)
    {
        return new TelegramInlineKeyboardMarkup(
        [
            [
                new TelegramInlineKeyboardButton(
                    Localize(language, "⬅️ До команд", "⬅️ Back to commands"),
                    "menu:commands")
            ]
        ]);
    }

    public string BuildStatusText(BackendDeviceStatePayload snapshot, TelegramChatLanguage language)
    {
        var dashboard = snapshot.Dashboard;
        var gateLabel = LocalizeGateMode(dashboard.GateMode, language);
        var kyivTime = TimeZoneInfo.ConvertTime(snapshot.CapturedAtUtc, KyivTimeZone);

        return language == TelegramChatLanguage.Ukrainian
            ? string.Join(
                '\n',
                "<b>Статус системи</b>",
                string.Empty,
                $"Підключення: {(dashboard.IsConnected ? "є" : "немає")}",
                $"Ворота: {Html(gateLabel)}",
                $"Місця: вільно {dashboard.FreeCount}, зайнято {dashboard.OccupiedCount}, вимкнено {
                    dashboard.DisabledCount}",
                $"Картки: дозволено {dashboard.AllowedCount}, заблоковано {dashboard.BlockedCount}",
                string.Empty,
                $"Оновлено: {kyivTime:HH:mm:ss}",
                $"День: {kyivTime:yyyy-MM-dd}")
            : string.Join(
                '\n',
                "<b>System status</b>",
                string.Empty,
                $"Connection: {(dashboard.IsConnected ? "connected" : "disconnected")}",
                $"Gate: {Html(gateLabel)}",
                $"Slots: free {dashboard.FreeCount}, occupied {dashboard.OccupiedCount}, disabled {
                    dashboard.DisabledCount}",
                $"Cards: allowed {dashboard.AllowedCount}, blocked {dashboard.BlockedCount}",
                string.Empty,
                $"Updated: {kyivTime:HH:mm:ss}",
                $"Day: {kyivTime:yyyy-MM-dd}");
    }

    public string BuildEventsText(IReadOnlyList<EventFeedItem> events, TelegramChatLanguage language)
    {
        var title = Localize(language, "Останні події", "Recent events");

        if (events.Count == 0)
        {
            return $"<b>{title}</b>\n\n{Localize(language, "Подій поки немає.", "No events yet.")}";
        }

        var lines = new List<string>
        {
            $"<b>{title}</b>",
            string.Empty
        };

        foreach (var item in events.OrderByDescending(static eventItem => eventItem.CreatedAt).Take(5))
        {
            var kyivTime = TimeZoneInfo.ConvertTime(item.CreatedAt, KyivTimeZone);
            lines.Add($"• {kyivTime:HH:mm:ss} — {Html(BuildEventSummary(item, language))}");
        }

        return string.Join('\n', lines);
    }

    public string BuildNotificationText(EventFeedItem item, TelegramChatLanguage language)
    {
        var category = Html(LocalizeEventCategory(item, language));
        var eventLabel = Html(LocalizeNotificationFieldLabel(item.Kind, language));
        var subject = string.IsNullOrWhiteSpace(item.Subject)
            ? null
            : Html(ValueOrFallback(item.Subject, language));
        var previousValue = Html(LocalizeEventValue(item.PreviousValue, language));
        var currentValue = Html(LocalizeEventValue(item.CurrentValue, language));
        var kyivTime = TimeZoneInfo.ConvertTime(item.CreatedAt, KyivTimeZone);
        var timeLabel = Localize(language, "Час", "Time");
        var dayLabel = Localize(language, "День", "Day");

        var lines = new List<string>
        {
            $"<b>🔔 {category}</b>",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(subject))
        {
            lines.Add($"{eventLabel} ({subject}): {FormatChange(previousValue, currentValue)}");
        }
        else
        {
            lines.Add($"{eventLabel}: {FormatChange(previousValue, currentValue)}");
        }

        lines.Add(string.Empty);
        lines.Add($"{timeLabel}: {kyivTime:HH:mm:ss}");
        lines.Add($"{dayLabel}: {kyivTime:yyyy-MM-dd}");

        return string.Join('\n', lines);
    }

    public string BuildNoSnapshotText(TelegramChatLanguage language)
    {
        return Localize(
            language,
            "Система ще не передала стан. Запусти MAUI-додаток і підключи контролер.",
            "The system has not sent its state yet. Start the MAUI app and connect the controller.");
    }

    public string BuildBotEnabledToggledText(bool enabled, TelegramChatLanguage language)
    {
        return enabled
            ? Localize(language, "Бота для цього чату увімкнено.", "Bot enabled for this chat.")
            : Localize(language, "Бота для цього чату вимкнено.", "Bot disabled for this chat.");
    }

    public string BuildBotDisabledText(TelegramChatLanguage language)
    {
        return Localize(
            language,
            "Бот зараз вимкнений для цього чату. Увімкни його в головному меню, щоб використовувати команди та сповіщення.",
            "The bot is disabled for this chat right now. Enable it in the main menu to use commands and notifications.");
    }

    public string BuildNotificationsToggledText(bool enabled, TelegramChatLanguage language)
    {
        return enabled
            ? Localize(language, "Сповіщення увімкнено.", "Notifications enabled.")
            : Localize(language, "Сповіщення вимкнено.", "Notifications disabled.");
    }

    public string BuildLanguageAppliedText(TelegramChatLanguage language)
    {
        return language == TelegramChatLanguage.Ukrainian
            ? "Мову застосовано: Українська."
            : "Language applied: English.";
    }

    public string BuildNotificationKindToggledText(
        TelegramNotificationKind kind,
        bool enabled,
        TelegramChatLanguage language)
    {
        var kindLabel = kind switch
        {
            TelegramNotificationKind.Gate => Localize(language, "Ворота", "Gate"),
            TelegramNotificationKind.Parking => Localize(language, "Паркування", "Parking"),
            TelegramNotificationKind.Monitor => Localize(language, "Монітор", "Monitor"),
            TelegramNotificationKind.Admin => Localize(language, "Адмін", "Admin"),
            TelegramNotificationKind.Connection => Localize(language, "Підключення", "Connection"),
            _ => kind.ToString()
        };

        return enabled
            ? Localize(language, $"Сповіщення \"{kindLabel}\" увімкнено.", $"\"{kindLabel}\" notifications enabled.")
            : Localize(language, $"Сповіщення \"{kindLabel}\" вимкнено.", $"\"{kindLabel}\" notifications disabled.");
    }

    public string BuildBackendCommandResultText(string message, bool succeeded, TelegramChatLanguage language)
    {
        var prefix = succeeded
            ? Localize(language, "✅ Виконано", "✅ Completed")
            : Localize(language, "❌ Помилка", "❌ Failed");
        var userMessage = LocalizeBackendCommandMessage(message, language);

        return $"<b>{prefix}</b>\n\n{Html(userMessage)}";
    }

    private static string LocalizeBackendCommandMessage(string message, TelegramChatLanguage language)
    {
        if (language == TelegramChatLanguage.English)
        {
            return message;
        }

        return message switch
        {
            "System state refreshed." => "Стан системи оновлено.",
            "Gate opened temporarily." => "Ворота тимчасово відкрито.",
            "Gate forced open." => "Ворота примусово відкрито.",
            "Gate closed." => "Ворота закрито.",
            "Gate lock enabled." => "Блокування воріт увімкнено.",
            "Gate lock disabled." => "Блокування воріт вимкнено.",
            "Unsupported command." => "Ця команда поки не підтримується.",
            "No active MAUI device host is connected." => "MAUI-додаток зараз не підключений.",
            "The MAUI host did not return a command result in time." => "MAUI-додаток не відповів вчасно.",
            "The command request was canceled." => "Команду скасовано.",
            "Parking slot id was not provided." => "Не вдалося визначити паркомісце.",
            _ => LocalizeParkingCommandMessage(message) ?? message
        };
    }

    private static string? LocalizeParkingCommandMessage(string message)
    {
        const string prefix = "Parking slot ";

        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var labelAndStatus = message[prefix.Length..];
        return labelAndStatus switch
        {
            var value when value.EndsWith(" disabled.", StringComparison.Ordinal) =>
                $"Паркомісце {value[..^" disabled.".Length]} вимкнено.",
            var value when value.EndsWith(" enabled and free.", StringComparison.Ordinal) =>
                $"Паркомісце {value[..^" enabled and free.".Length]} увімкнено. Стан: вільно.",
            var value when value.EndsWith(" enabled and occupied.", StringComparison.Ordinal) =>
                $"Паркомісце {value[..^" enabled and occupied.".Length]} увімкнено. Стан: зайнято.",
            var value when value.EndsWith(" updated.", StringComparison.Ordinal) =>
                $"Паркомісце {value[..^" updated.".Length]} оновлено.",
            _ => null
        };
    }

    private static TelegramInlineKeyboardButton BuildToggleButton(bool enabled, string label, string callbackData)
    {
        var prefix = enabled ? "✅" : "⬜";
        return new TelegramInlineKeyboardButton($"{prefix} {label}", callbackData);
    }

    private static string Localize(TelegramChatLanguage language, string ukrainian, string english)
    {
        return language == TelegramChatLanguage.Ukrainian ? ukrainian : english;
    }

    private static TelegramChatLanguage GetLanguageOrDefault(TelegramChatSettings settings)
    {
        return settings.Language ?? TelegramChatLanguage.Ukrainian;
    }

    private static string ValueOrFallback(string? value, TelegramChatLanguage language)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Localize(language, "Невідомо", "Unknown")
            : value;
    }

    private static string FormatChange(string? previousValue, string? currentValue)
    {
        if (!string.IsNullOrWhiteSpace(previousValue) && !string.IsNullOrWhiteSpace(currentValue))
        {
            return $"{previousValue} → {currentValue}";
        }

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            return currentValue;
        }

        if (!string.IsNullOrWhiteSpace(previousValue))
        {
            return previousValue;
        }

        return "—";
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static TimeZoneInfo ResolveKyivTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
    }

    private static string LocalizeGateMode(GateMode gateMode, TelegramChatLanguage language)
    {
        return gateMode switch
        {
            GateMode.Closed => Localize(language, "Закриті", "Closed"),
            GateMode.TemporaryOpen => Localize(language, "Тимчасово відкриті", "Temporarily open"),
            GateMode.ForcedOpen => Localize(language, "Примусово відкриті", "Forced open"),
            GateMode.Locked => Localize(language, "Заблоковані", "Locked"),
            _ => gateMode.ToString()
        };
    }

    private static string LocalizeNotificationFieldLabel(EventKind kind, TelegramChatLanguage language)
    {
        return kind switch
        {
            EventKind.GateStateChanged => Localize(language, "Стан", "State"),
            EventKind.GateForceOpenChanged => Localize(language, "Примусове відкриття", "Force open"),
            EventKind.GateForceLockChanged => Localize(language, "Блокування", "Lock"),
            EventKind.GateOpenAngleChanged => Localize(language, "Кут відкриття", "Open angle"),
            EventKind.GateClosedAngleChanged => Localize(language, "Кут закриття", "Closed angle"),
            EventKind.GateOpenDurationChanged => Localize(language, "Тривалість відкриття", "Open duration"),
            EventKind.MonitorForceModeChanged => Localize(language, "Примусовий режим", "Forced mode"),
            EventKind.MonitorTextChanged => Localize(language, "Текст", "Text"),
            EventKind.MonitorTemplateChanged => Localize(language, "Шаблон", "Template"),
            EventKind.ConnectionIntervalChanged => Localize(language, "Інтервал", "Interval"),
            EventKind.ParkingThresholdChanged => Localize(language, "Поріг датчика", "Sensor threshold"),
            EventKind.ParkingSlotChanged => Localize(language, "Стан", "State"),
            EventKind.ParkingSlotAvailabilityChanged => Localize(language, "Доступність", "Availability"),
            EventKind.AllowedCardsChanged => Localize(language, "Дозволені картки", "Allowed cards"),
            EventKind.BlockedCardsChanged => Localize(language, "Заблоковані картки", "Blocked cards"),
            _ => LocalizeEventKind(kind, language)
        };
    }

    private static string BuildEventSummary(EventFeedItem item, TelegramChatLanguage language)
    {
        var eventLabel = LocalizeEventKind(item.Kind, language);
        var subject = string.IsNullOrWhiteSpace(item.Subject)
            ? null
            : ValueOrFallback(item.Subject, language);
        var previousValue = LocalizeEventValue(item.PreviousValue, language);
        var currentValue = LocalizeEventValue(item.CurrentValue, language);

        if (!string.IsNullOrWhiteSpace(previousValue) && !string.IsNullOrWhiteSpace(currentValue))
        {
            return string.IsNullOrWhiteSpace(subject)
                ? $"{eventLabel}: {previousValue} -> {currentValue}"
                : $"{eventLabel} ({subject}): {previousValue} -> {currentValue}";
        }

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            return string.IsNullOrWhiteSpace(subject)
                ? $"{eventLabel}: {currentValue}"
                : $"{eventLabel} ({subject}): {currentValue}";
        }

        return string.IsNullOrWhiteSpace(subject)
            ? eventLabel
            : $"{eventLabel} ({subject})";
    }

    private static string LocalizeEventCategory(EventFeedItem item, TelegramChatLanguage language)
    {
        if (IsAdminConfigurationEvent(item.Kind))
        {
            return Localize(language, "Адмін", "Admin");
        }

        return item.Category switch
        {
            EventCategory.Connection => Localize(language, "Підключення", "Connection"),
            EventCategory.Gate => Localize(language, "Ворота", "Gate"),
            EventCategory.Parking => Localize(language, "Паркування", "Parking"),
            EventCategory.Monitor => Localize(language, "Монітор", "Monitor"),
            EventCategory.System => Localize(language, "Система", "System"),
            _ => item.Category.ToString()
        };
    }

    private static bool IsAdminConfigurationEvent(EventKind kind)
    {
        return kind is EventKind.GateOpenAngleChanged
            or EventKind.GateClosedAngleChanged
            or EventKind.GateOpenDurationChanged
            or EventKind.ConnectionIntervalChanged
            or EventKind.ParkingThresholdChanged
            or EventKind.AllowedCardsChanged
            or EventKind.BlockedCardsChanged;
    }

    private static string LocalizeEventKind(EventKind kind, TelegramChatLanguage language)
    {
        return kind switch
        {
            EventKind.ControllerConnected => Localize(language, "Контролер підключено", "Controller connected"),
            EventKind.ControllerDisconnected => Localize(language, "Контролер відключено", "Controller disconnected"),
            EventKind.GateStateChanged => Localize(language, "Стан воріт", "Gate state"),
            EventKind.GateForceOpenChanged => Localize(language, "Примусове відкриття воріт", "Gate forced open"),
            EventKind.GateForceLockChanged => Localize(language, "Блокування воріт", "Gate lock"),
            EventKind.GateOpenAngleChanged => Localize(language, "Кут відкриття воріт", "Gate open angle"),
            EventKind.GateClosedAngleChanged => Localize(language, "Кут закриття воріт", "Gate closed angle"),
            EventKind.GateOpenDurationChanged => Localize(language, "Тривалість відкриття воріт", "Gate open duration"),
            EventKind.MonitorForceModeChanged => Localize(language, "Режим монітора", "Monitor mode"),
            EventKind.MonitorTextChanged => Localize(language, "Текст монітора", "Monitor text"),
            EventKind.MonitorTemplateChanged => Localize(language, "Шаблон монітора", "Monitor template"),
            EventKind.ConnectionIntervalChanged => Localize(language, "Інтервал підключення", "Connection interval"),
            EventKind.ParkingThresholdChanged => Localize(
                language,
                "Поріг датчика паркування",
                "Parking sensor threshold"),
            EventKind.ParkingSlotChanged => Localize(language, "Стан паркомісця", "Parking slot state"),
            EventKind.ParkingSlotAvailabilityChanged => Localize(
                language,
                "Доступність паркомісця",
                "Parking slot availability"),
            EventKind.AllowedCardsChanged => Localize(language, "Список дозволених карток", "Allowed cards list"),
            EventKind.BlockedCardsChanged => Localize(language, "Список заблокованих карток", "Blocked cards list"),
            _ => kind.ToString()
        };
    }

    private static string? LocalizeEventValue(string? value, TelegramChatLanguage language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value switch
        {
            "True" or "true" => Localize(language, "увімкнено", "enabled"),
            "False" or "false" => Localize(language, "вимкнено", "disabled"),
            "Closed" or "CLOSED" => Localize(language, "закрито", "closed"),
            "TemporaryOpen" or "TEMP_OPEN" => Localize(language, "тимчасово відкрито", "temporarily open"),
            "ForcedOpen" or "FORCED_OPEN" => Localize(language, "примусово відкрито", "forced open"),
            "Locked" or "LOCKED" => Localize(language, "заблоковано", "locked"),
            "Free" or "FREE" => Localize(language, "вільно", "free"),
            "Occupied" or "OCCUPIED" => Localize(language, "зайнято", "occupied"),
            "Disabled" or "DISABLED" => Localize(language, "вимкнено", "disabled"),
            "DISPLAY DEFAULT" => Localize(language, "стандартний текст", "default text"),
            "DISPLAY FORCED" => Localize(language, "примусовий текст", "forced text"),
            "DISPLAY ALLOWED" => Localize(language, "текст для дозволеної картки", "allowed-card text"),
            "DISPLAY BLOCKED" => Localize(language, "текст для заблокованої картки", "blocked-card text"),
            "DISPLAY INVALID" => Localize(language, "текст для невірної картки", "invalid-card text"),
            "DISPLAY LOCKED" => Localize(language, "текст для заблокованих воріт", "locked-gate text"),
            _ => value
        };
    }

    private static string GetParkingSlotEmoji(ParkingSlotState state)
    {
        return state switch
        {
            ParkingSlotState.Free => "🟢",
            ParkingSlotState.Occupied => "🔴",
            ParkingSlotState.Disabled => "⚫",
            _ => "⚪"
        };
    }

    private static string LocalizeParkingSlotState(ParkingSlotState state, TelegramChatLanguage language)
    {
        return state switch
        {
            ParkingSlotState.Free => Localize(language, "вільне", "free"),
            ParkingSlotState.Occupied => Localize(language, "зайняте", "occupied"),
            ParkingSlotState.Disabled => Localize(language, "вимкнене", "disabled"),
            _ => state.ToString()
        };
    }
}
