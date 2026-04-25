namespace SmartParkingSystem.Domain.Models.Events;

public sealed record EventFeedItem(
    string Id,
    EventCategory Category,
    EventKind Kind,
    string? Subject,
    string? PreviousValue,
    string? CurrentValue,
    DateTimeOffset CreatedAt);