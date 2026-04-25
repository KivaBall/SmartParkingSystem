namespace SmartParkingSystem.Domain.Models.BackendCommands;

public enum BackendCommandKind
{
    RefreshState,
    ForceOpenGate,
    OpenGateTemporarily,
    CloseGate,
    ToggleGateLock,
    ToggleParkingSlot
}