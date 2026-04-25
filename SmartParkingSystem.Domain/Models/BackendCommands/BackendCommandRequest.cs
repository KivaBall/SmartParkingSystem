namespace SmartParkingSystem.Domain.Models.BackendCommands;

public sealed record BackendCommandRequest(
    string CommandId,
    BackendCommandKind Kind,
    DateTimeOffset RequestedAtUtc,
    string? Argument = null);