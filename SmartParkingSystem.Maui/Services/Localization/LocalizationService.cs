using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.AppMemory;

namespace SmartParkingSystem.Maui.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly IAppMemoryStore _memoryStore;
    private AppLanguage _currentLanguage;

    public LocalizationService(IAppMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
        _currentLanguage = memoryStore.GetLanguage(AppLanguage.English);
    }

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
            _memoryStore.SetLanguage(value);
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
}