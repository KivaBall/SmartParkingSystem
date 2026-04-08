using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Services.Localization;

internal static partial class LocalizationTextCatalog
{
    private static readonly AdminTexts EnglishAdminTexts = new AdminTexts(
        "Gate",
        "Parking",
        "Cards",
        "System",
        "Refresh",
        "Save",
        "Reset",
        "Open angle (deg)",
        "Closed angle (deg)",
        "Open time (ms)",
        "Forced open",
        "Forced lock",
        "Scan threshold (cm)",
        "Disabled parking slots",
        "Parking spot 1",
        "Parking spot 2",
        "Parking spot 3",
        "Parking spot 4",
        "Parking spot 5",
        "Parking spot 6",
        "Connection interval (ms)",
        "Allowed cards",
        "Blocked cards",
        "Enabled",
        "Disabled",
        "Refreshing current admin values...",
        "Saving current admin values...",
        "Resetting admin values to defaults...",
        "Current values refreshed",
        "Current values saved",
        "Default values restored");

    private static readonly AdminTexts UkrainianAdminTexts = new AdminTexts(
        "Ворота",
        "Паркінг",
        "Картки",
        "Система",
        "Оновити",
        "Зберегти",
        "Скинути",
        "Кут відкриття (градуси)",
        "Кут закриття (градуси)",
        "Час відкритих воріт (мс)",
        "Примусово відкрити",
        "Примусове блокування",
        "Поріг сканування (см)",
        "Вимкнені паркомісця",
        "Місце P1",
        "Місце P2",
        "Місце P3",
        "Місце P4",
        "Місце P5",
        "Місце P6",
        "Інтервал зв'язку (мс)",
        "Дозволені картки",
        "Заблоковані картки",
        "Увімкнено",
        "Вимкнено",
        "Оновлюємо поточні адмін-значення...",
        "Зберігаємо поточні адмін-значення...",
        "Скидаємо адмін-значення до стандартних...",
        "Поточні значення оновлено",
        "Поточні значення збережено",
        "Стандартні значення відновлено");

    private static readonly GateTexts EnglishGateTexts = new GateTexts(
        "Gate",
        "Gate control actions",
        "Time remaining",
        "Open",
        "Closed",
        "Locked",
        "Force open",
        "Open temporarily",
        "Close",
        "Lock",
        "Unlock",
        "s");

    private static readonly GateTexts UkrainianGateTexts = new GateTexts(
        "Ворота",
        "Керування воротами",
        "Залишилось до закриття",
        "Відкрито",
        "Закрито",
        "Заблоковано",
        "Примусово відкрити",
        "Відкрити тимчасово",
        "Закрити",
        "Заблокувати",
        "Розблокувати",
        "с");

    public static AdminTexts GetAdminTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianAdminTexts : EnglishAdminTexts;
    }

    public static GateTexts GetGateTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianGateTexts : EnglishGateTexts;
    }
}