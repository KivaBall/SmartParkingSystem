using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Services.Localization;

internal static partial class LocalizationTextCatalog
{
    private static readonly AppHeaderTexts EnglishHeaderTexts = new AppHeaderTexts(
        "Dashboard",
        "Parking",
        "Gate",
        "Monitor",
        "Events",
        "Admin",
        "Settings",
        "Exit");

    private static readonly AppHeaderTexts UkrainianHeaderTexts = new AppHeaderTexts(
        "Дашборд",
        "Паркінг",
        "Ворота",
        "Монітор",
        "Події",
        "Адмін",
        "Налаштування",
        "Вихід");

    private static readonly ConnectionTexts EnglishConnectionTexts = new ConnectionTexts(
        "Smart Parking System",
        "Modern control access for the parking controller. Choose the connection method and continue into the system",
        "Automatic",
        "Advanced",
        "Connect Automatically",
        "Connect",
        "Refresh",
        "Device or port",
        "The app scans available devices automatically and picks the one that matches the parking controller",
        "Searching for the parking controller and trying to connect automatically...",
        "Automatic connection failed. Try again or switch to Advanced mode",
        "Parking controller found. Opening the workspace...",
        "Use refresh to rescan the available targets, then choose the exact device and connect manually",
        "Refreshing the available device list...",
        "Trying to connect to the selected device...",
        "Connection failed. Check the selected target or refresh the list",
        "Device connected. Opening the workspace...");

    private static readonly ConnectionTexts UkrainianConnectionTexts = new ConnectionTexts(
        "Система Смарт Паркінгу",
        "Сучасний інтерфейс доступу до контролера паркування. Обери спосіб підключення і продовж роботу в системі",
        "Авто",
        "Ручний",
        "Підключити автоматично",
        "Підключити",
        "Оновити",
        "Пристрій або порт",
        "Застосунок автоматично шукає доступні пристрої та обирає той, що відповідає контролеру паркування",
        "Триває пошук контролера паркування та автоматична спроба підключення...",
        "Автоматичне підключення не вдалося. Спробуй ще раз або перейди в ручний режим",
        "Контролер знайдено. Відкриваємо робочий простір...",
        "Онови список доступних цілей, вибери потрібний пристрій і підключись вручну",
        "Оновлюємо список доступних пристроїв...",
        "Триває спроба підключення до вибраного пристрою...",
        "Підключення не вдалося. Перевір вибраний пристрій або онови список",
        "Пристрій підключено. Відкриваємо робочий простір...");

    public static AppHeaderTexts GetAppHeaderTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianHeaderTexts : EnglishHeaderTexts;
    }

    public static ConnectionTexts GetConnectionTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianConnectionTexts : EnglishConnectionTexts;
    }
}