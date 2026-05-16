using SmartParkingSystem.Domain.Models.Parking;

namespace SmartParkingSystem.Maui.Services.Parking;

public interface ISmartParkingRouteService
{
    SlotOccupant? GetSlotOccupant(int slotNumber);
}
