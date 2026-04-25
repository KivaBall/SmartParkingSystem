using SmartParkingSystem.Domain.Models.BackendSync;

namespace SmartParkingSystem.TelegramBot.Services.DeviceState;

public sealed class DeviceStateCache
{
    private readonly Lock _sync = new Lock();

    public BackendDeviceStatePayload? CurrentSnapshot { get; private set; }

    public void SetCurrentSnapshot(BackendDeviceStatePayload payload)
    {
        lock (_sync)
        {
            CurrentSnapshot = payload;
        }
    }
}