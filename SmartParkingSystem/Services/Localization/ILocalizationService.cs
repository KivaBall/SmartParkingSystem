using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Services.Localization;

public interface ILocalizationService
{
    ConnectionTexts GetConnectionTexts(AppLanguage language);
}