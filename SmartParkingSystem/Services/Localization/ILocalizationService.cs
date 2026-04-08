using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Services.Localization;

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; set; }
    event Action? LanguageChanged;
    AppHeaderTexts GetAppHeaderTexts();
    ConnectionTexts GetConnectionTexts();
    DashboardTexts GetDashboardTexts();
    SettingsTexts GetSettingsTexts();
    AdminTexts GetAdminTexts();
    GateTexts GetGateTexts();
    ParkingTexts GetParkingTexts();
    MonitorTexts GetMonitorTexts();
    EventsTexts GetEventsTexts();
}