using SmartParkingSystem.Domain.Models.BackendSync;

namespace SmartParkingSystem.TelegramBot.Services.Storage;

public interface IDeviceStateStore
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(BackendDeviceStatePayload payload, CancellationToken cancellationToken = default);

    Task<BackendDeviceStatePayload?> GetLatestSnapshotAsync(CancellationToken cancellationToken = default);
}