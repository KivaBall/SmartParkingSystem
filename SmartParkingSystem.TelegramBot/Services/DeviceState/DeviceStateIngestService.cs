using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.TelegramBot.Services.Storage;
using SmartParkingSystem.TelegramBot.Services.Telegram;

namespace SmartParkingSystem.TelegramBot.Services.DeviceState;

public sealed class DeviceStateIngestService(
    IDeviceStateStore store,
    DeviceStateCache cache,
    TelegramNotificationService notificationService)
{
    public async Task IngestAsync(BackendDeviceStatePayload payload, CancellationToken cancellationToken = default)
    {
        await store.SaveSnapshotAsync(payload, cancellationToken);
        cache.SetCurrentSnapshot(payload);
        await notificationService.NotifyAsync(payload, cancellationToken);
    }
}