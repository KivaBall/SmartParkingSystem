using SmartParkingSystem.Models.Events;

namespace SmartParkingSystem.Services.Events;

public interface IEventsService
{
    IReadOnlyList<EventFeedItem> GetRecentEvents();
}