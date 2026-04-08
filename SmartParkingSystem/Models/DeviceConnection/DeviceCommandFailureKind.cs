namespace SmartParkingSystem.Models.DeviceConnection;

public enum DeviceCommandFailureKind
{
    None,
    TransportClosed,
    DeviceRejected,
    Timeout
}