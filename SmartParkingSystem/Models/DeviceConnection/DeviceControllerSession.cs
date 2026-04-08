namespace SmartParkingSystem.Models.DeviceConnection;

public sealed record DeviceControllerSession(
    ConnectionTarget Target,
    DeviceControllerProfile Profile,
    DeviceControllerConfiguration Configuration,
    DeviceControllerSnapshot Snapshot,
    DateTimeOffset ConnectedAt);