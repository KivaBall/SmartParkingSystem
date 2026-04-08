using SmartParkingSystem.Models.Parking;

namespace SmartParkingSystem.Services.Parking;

public interface IParkingService
{
    Task<IReadOnlyList<ParkingSlotSnapshot>> GetSlotsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkingSlotSnapshot>> ToggleSlotEnabledAsync(
        string slotId,
        CancellationToken cancellationToken = default);
}