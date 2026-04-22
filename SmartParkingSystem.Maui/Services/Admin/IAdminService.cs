using SmartParkingSystem.Domain.Models.Admin;

namespace SmartParkingSystem.Maui.Services.Admin;

public interface IAdminService
{
    Task<AdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<AdminSnapshot> SaveAsync(AdminEditableSettings settings, CancellationToken cancellationToken = default);
    Task<AdminSnapshot> ResetAsync(CancellationToken cancellationToken = default);
}