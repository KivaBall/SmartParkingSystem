namespace SmartParkingSystem.Domain.Models.DeviceConnection;

public sealed record DeviceControllerProfile(
    string Board,
    string Rfid,
    string Lcd,
    string Gate,
    string Transport,
    int SlotCount);