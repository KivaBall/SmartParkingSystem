using SmartParkingSystem.TelegramBot.Services.DeviceState;

namespace SmartParkingSystem.TelegramBot.Services.Storage;

public sealed class DeviceStateStorageHostedService(
    IDeviceStateStore store,
    ITelegramChatSettingsStore chatSettingsStore,
    DeviceStateCache cache) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.EnsureInitializedAsync(cancellationToken);
        await chatSettingsStore.EnsureInitializedAsync(cancellationToken);

        var latestSnapshot = await store.GetLatestSnapshotAsync(cancellationToken);
        if (latestSnapshot is null)
        {
            return;
        }

        cache.SetCurrentSnapshot(latestSnapshot);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}