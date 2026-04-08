using SmartParkingSystem.Models.Admin;

namespace SmartParkingSystem.Services.Admin;

public interface IAdminService
{
    Task<AdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<AdminSnapshot> SaveAsync(AdminEditableSettings settings, CancellationToken cancellationToken = default);
    Task<AdminSnapshot> ResetAsync(CancellationToken cancellationToken = default);
}