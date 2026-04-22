using SmartParkingSystem.Domain.Models.Monitor;

namespace SmartParkingSystem.Maui.Services.Monitor;

public interface IMonitorService
{
    Task<MonitorSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<MonitorSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
    Task<MonitorSnapshot> SaveAsync(MonitorEditableSettings settings, CancellationToken cancellationToken = default);
    Task<MonitorSnapshot> ResetAsync(CancellationToken cancellationToken = default);
}