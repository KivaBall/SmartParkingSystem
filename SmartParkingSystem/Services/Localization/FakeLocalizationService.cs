using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Services.Localization;

public sealed class FakeLocalizationService : ILocalizationService
{
    private static readonly ConnectionTexts EnglishTexts = new ConnectionTexts(
        "Smart Parking System",
        "Modern control access for the parking controller. Choose the connection method and continue into the system.",
        "Automatic",
        "Advanced",
        "Connect Automatically",
        "Connect",
        "Refresh",
        "Device or port",
        "The app scans available devices automatically and picks the one that matches the parking controller.",
        "Searching for the parking controller and trying to connect automatically...",
        "Automatic connection failed. Try again or switch to Advanced mode.",
        "Parking controller found. Opening the dashboard...",
        "Use refresh to rescan the available targets, then choose the exact device and connect manually.",
        "Refreshing the available device list...",
        "Trying to connect to the selected device...",
        "Connection failed. Check the selected target or refresh the list.",
        "Device connected. Opening the dashboard...");

    private static readonly ConnectionTexts UkrainianTexts = new ConnectionTexts(
        "Система Смарт Паркінгу",
        "Сучасний інтерфейс доступу до контролера паркування. Обери спосіб підключення і продовж роботу в системі.",
        "Авто",
        "Ручний",
        "Підключити автоматично",
        "Підключити",
        "Оновити",
        "Пристрій або порт",
        "Застосунок автоматично шукає доступні пристрої та обирає той, що відповідає контролеру паркування.",
        "Триває пошук контролера паркування та автоматична спроба підключення...",
        "Автоматичне підключення не вдалося. Спробуй ще раз або перейди в ручний режим.",
        "Контролер знайдено. Відкриваємо головний екран...",
        "Онови список доступних цілей, вибери потрібний пристрій і підключись вручну.",
        "Оновлюємо список доступних пристроїв...",
        "Триває спроба підключення до вибраного пристрою...",
        "Підключення не вдалося. Перевір вибраний пристрій або онови список.",
        "Пристрій підключено. Відкриваємо головний екран...");

    public ConnectionTexts GetConnectionTexts(AppLanguage language)
    {
        return language == AppLanguage.Ukrainian ? UkrainianTexts : EnglishTexts;
    }
}