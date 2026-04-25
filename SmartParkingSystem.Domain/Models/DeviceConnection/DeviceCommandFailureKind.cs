namespace SmartParkingSystem.Domain.Models.DeviceConnection;

public enum DeviceCommandFailureKind
{
    None,
    TransportClosed,
    DeviceRejected,
    Timeout
}