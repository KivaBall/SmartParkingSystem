using SmartParkingSystem.Domain.Models.Localization;

namespace SmartParkingSystem.Maui.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private const string LanguagePreferenceKey = "app.language";

    private AppLanguage _currentLanguage = LoadLanguage();

    public event Action? LanguageChanged;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
            {
                return;
            }

            _currentLanguage = value;
            Preferences.Default.Set(LanguagePreferenceKey, value.ToString());
            LanguageChanged?.Invoke();
        }
    }

    public AppHeaderTexts GetAppHeaderTexts()
    {
        return LocalizationTextCatalog.GetAppHeaderTexts(CurrentLanguage);
    }

    public ConnectionTexts GetConnectionTexts()
    {
        return LocalizationTextCatalog.GetConnectionTexts(CurrentLanguage);
    }

    public DashboardTexts GetDashboardTexts()
    {
        return LocalizationTextCatalog.GetDashboardTexts(CurrentLanguage);
    }

    public SettingsTexts GetSettingsTexts()
    {
        return LocalizationTextCatalog.GetSettingsTexts(CurrentLanguage);
    }

    public AdminTexts GetAdminTexts()
    {
        return LocalizationTextCatalog.GetAdminTexts(CurrentLanguage);
    }

    public GateTexts GetGateTexts()
    {
        return LocalizationTextCatalog.GetGateTexts(CurrentLanguage);
    }

    public ParkingTexts GetParkingTexts()
    {
        return LocalizationTextCatalog.GetParkingTexts(CurrentLanguage);
    }

    public MonitorTexts GetMonitorTexts()
    {
        return LocalizationTextCatalog.GetMonitorTexts(CurrentLanguage);
    }

    public EventsTexts GetEventsTexts()
    {
        return LocalizationTextCatalog.GetEventsTexts(CurrentLanguage);
    }

    private static AppLanguage LoadLanguage()
    {
        var storedValue = Preferences.Default.Get(LanguagePreferenceKey, nameof(AppLanguage.English));
        return Enum.TryParse<AppLanguage>(storedValue, true, out var parsedValue)
            ? parsedValue
            : AppLanguage.English;
    }
}