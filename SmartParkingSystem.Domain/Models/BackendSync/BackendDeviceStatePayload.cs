using SmartParkingSystem.Domain.Models.Admin;
using SmartParkingSystem.Domain.Models.Dashboard;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Gate;
using SmartParkingSystem.Domain.Models.Monitor;
using SmartParkingSystem.Domain.Models.Parking;

namespace SmartParkingSystem.Domain.Models.BackendSync;

public sealed record BackendDeviceStatePayload(
    DateTimeOffset CapturedAtUtc,
    string SourcePlatform,
    string AppVersion,
    DashboardSnapshot Dashboard,
    DeviceControllerSession? Session,
    GateSnapshot Gate,
    MonitorSnapshot Monitor,
    IReadOnlyList<ParkingSlotSnapshot> ParkingSlots,
    AdminSnapshot Admin,
    IReadOnlyList<EventFeedItem> RecentEvents);