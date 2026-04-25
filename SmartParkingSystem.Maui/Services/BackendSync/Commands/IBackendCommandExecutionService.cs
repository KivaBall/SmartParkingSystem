using SmartParkingSystem.Domain.Models.BackendCommands;

namespace SmartParkingSystem.Maui.Services.BackendSync.Commands;

public interface IBackendCommandExecutionService
{
    Task<BackendCommandExecutionResult> ExecuteAsync(
        BackendCommandRequest request,
        CancellationToken cancellationToken = default);
}