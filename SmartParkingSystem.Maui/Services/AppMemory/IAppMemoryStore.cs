using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Parking;

namespace SmartParkingSystem.Maui.Services.AppMemory;

public interface IAppMemoryStore
{
    AppLanguage GetLanguage(AppLanguage defaultLanguage);
    void SetLanguage(AppLanguage language);
    IReadOnlyList<EventFeedItem> GetEvents();
    void SetEvents(IReadOnlyList<EventFeedItem> events);
    IReadOnlyList<SmartParkingCardProfile> GetSmartParkingCardProfiles();
    void SetSmartParkingCardProfiles(IReadOnlyList<SmartParkingCardProfile> profiles);
}