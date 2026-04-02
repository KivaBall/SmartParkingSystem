using SmartParkingSystem.Models.DeviceConnection;

namespace SmartParkingSystem.Services.DeviceConnection;

public sealed class FakeDeviceConnectionService : IDeviceConnectionService
{
    private static readonly IReadOnlyList<ConnectionTarget> Targets =
    [
        new ConnectionTarget("bt-hc05", "HC-05 Main Controller"),
        new ConnectionTarget("com7", "COM7"),
        new ConnectionTarget("com11", "COM11")
    ];

    public async Task<IReadOnlyList<ConnectionTarget>> GetTargetsAsync()
    {
        await Task.Delay(700);
        return Targets;
    }

    public async Task<ConnectionResult> TryAutoConnectAsync()
    {
        await Task.Delay(2600);
        return new ConnectionResult(true);
    }

    public async Task<ConnectionResult> TryConnectAsync(string? targetId)
    {
        await Task.Delay(2200);

        var target = Targets.FirstOrDefault(item => item.Id == targetId);

        return target is null ? new ConnectionResult(false) : new ConnectionResult(true);
    }

    public async Task<IReadOnlyList<ConnectionTarget>> RefreshTargetsAsync()
    {
        await Task.Delay(1200);
        return Targets;
    }
}