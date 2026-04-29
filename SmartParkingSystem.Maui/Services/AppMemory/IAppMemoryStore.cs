using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Localization;

namespace SmartParkingSystem.Maui.Services.AppMemory;

public interface IAppMemoryStore
{
    AppLanguage GetLanguage(AppLanguage defaultLanguage);
    void SetLanguage(AppLanguage language);
    IReadOnlyList<EventFeedItem> GetEvents();
    void SetEvents(IReadOnlyList<EventFeedItem> events);
}