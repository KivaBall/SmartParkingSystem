using SmartParkingSystem.Domain.Models.Events;

namespace SmartParkingSystem.Maui.Services.Events;

public interface IEventsService
{
    IReadOnlyList<EventFeedItem> GetRecentEvents();
}