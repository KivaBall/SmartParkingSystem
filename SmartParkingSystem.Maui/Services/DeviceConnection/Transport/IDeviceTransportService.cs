using SmartParkingSystem.Domain.Models.DeviceConnection;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

public interface IDeviceTransportService
{
    bool IsOpen { get; }
    string? ActiveTargetId { get; }

    Task<IReadOnlyList<ConnectionTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken = default);
    Task<bool> OpenAsync(string targetId, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task SendLineAsync(string line, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    Task DrainAsync(TimeSpan idleTimeout, CancellationToken cancellationToken = default);
}