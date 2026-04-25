namespace SmartParkingSystem.Domain.Models.BackendCommands;

public sealed record BackendCommandExecutionResult(
    string CommandId,
    BackendCommandKind Kind,
    bool Succeeded,
    string Message,
    DateTimeOffset CompletedAtUtc);