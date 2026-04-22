using SmartParkingSystem.Domain.Models.Parking;

namespace SmartParkingSystem.Maui.Services.Parking;

public interface IParkingService
{
    Task<IReadOnlyList<ParkingSlotSnapshot>> GetSlotsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkingSlotSnapshot>> ToggleSlotEnabledAsync(
        string slotId,
        CancellationToken cancellationToken = default);
}