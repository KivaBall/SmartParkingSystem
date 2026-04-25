namespace SmartParkingSystem.Domain.Models.DeviceConnection;

public sealed record DeviceControllerSession(
    ConnectionTarget Target,
    DeviceControllerProfile Profile,
    DeviceControllerConfiguration Configuration,
    DeviceControllerSnapshot Snapshot,
    DateTimeOffset ConnectedAt);