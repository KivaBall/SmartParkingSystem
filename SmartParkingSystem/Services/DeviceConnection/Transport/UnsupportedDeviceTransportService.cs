using SmartParkingSystem.Models.DeviceConnection;

namespace SmartParkingSystem.Services.DeviceConnection.Transport;

public sealed class UnsupportedDeviceTransportService : IDeviceTransportService
{
    public bool IsOpen => false;

    public string? ActiveTargetId => null;

    public Task<IReadOnlyList<ConnectionTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ConnectionTarget>>([]);
    }

    public Task<bool> OpenAsync(string targetId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException("Device transport is not implemented on this platform.");
    }

    public Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task DrainAsync(TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}