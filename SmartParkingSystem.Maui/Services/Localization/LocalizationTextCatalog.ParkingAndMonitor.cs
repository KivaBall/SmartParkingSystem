using SmartParkingSystem.Domain.Models.Localization;

namespace SmartParkingSystem.Maui.Services.Localization;

internal static partial class LocalizationTextCatalog
{
    private static readonly ParkingTexts EnglishParkingTexts = new ParkingTexts(
        "Parking map",
        "Selected parking slot",
        "Free",
        "Occupied",
        "Disabled",
        "Occupied for",
        "No active occupation time",
        "Disabled",
        "Enable slot",
        "Disable slot",
        "Fullscreen",
        "Exit fullscreen",
        "Free spots",
        "Occupied spots",
        "RFID card",
        "Vehicle number",
        "Vehicle description",
        "Entry photo",
        "Undefined");

    private static readonly ParkingTexts UkrainianParkingTexts = new ParkingTexts(
        "Карта паркінгу",
        "Вибране паркомісце",
        "Вільно",
        "Зайнято",
        "Вимкнено",
        "Зайнято вже",
        "Немає",
        "Вимкнено",
        "Увімкнути місце",
        "Вимкнути місце",
        "На весь екран",
        "Вийти з повного екрану",
        "Вільних місць",
        "Зайнятих місць",
        "RFID картка",
        "Номер машини",
        "Опис машини",
        "Знімок при в'їзді",
        "Невизначено");

    private static readonly MonitorTexts EnglishMonitorTexts = new MonitorTexts(
        "Monitor",
        "Current display",
        "Force monitor text",
        "Forced text",
        "Default text",
        "Allowed card text",
        "Blocked card text",
        "Invalid card text",
        "Locked text",
        "Camera unavailable text",
        "Vehicle unrecognized text",
        "AI unavailable text",
        "Unknown vehicle denied text",
        "Camera access allowed text",
        "Save",
        "Refresh",
        "Refreshing monitor values...",
        "Saving monitor values...",
        "Monitor values refreshed",
        "Monitor values saved",
        "Printable ASCII only, up to 16 characters",
        "Enabled",
        "Disabled");

    private static readonly MonitorTexts UkrainianMonitorTexts = new MonitorTexts(
        "Монітор",
        "Поточний надпис",
        "Примусовий режим монітора",
        "Примусовий текст",
        "Текст за замовчуванням",
        "Текст дозволеної картки",
        "Текст заблокованої картки",
        "Текст невірної картки",
        "Текст заблокованих воріт",
        "Камера недоступна",
        "Авто не розпізнано",
        "AI недоступний",
        "Невідоме авто заборонено",
        "Camera-доступ дозволено",
        "Зберегти",
        "Оновити",
        "Оновлюємо значення монітора...",
        "Зберігаємо значення монітора...",
        "Значення монітора оновлено",
        "Значення монітора збережено",
        "Лише printable ASCII, до 16 символів",
        "Увімкнено",
        "Вимкнено");

    public static ParkingTexts GetParkingTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianParkingTexts : EnglishParkingTexts;
    }

    public static MonitorTexts GetMonitorTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianMonitorTexts : EnglishMonitorTexts;
    }
}
