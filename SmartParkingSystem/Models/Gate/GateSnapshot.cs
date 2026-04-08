namespace SmartParkingSystem.Models.Gate;

public sealed record GateSnapshot(
    GateMode Mode,
    bool IsOpen,
    bool IsLocked,
    int RemainingSeconds)
{
    public bool CanForceOpen => Mode is not (GateMode.ForcedOpen or GateMode.Locked);

    public bool CanOpenTemporarily => Mode is not (GateMode.TemporaryOpen or GateMode.Locked);

    public bool CanClose => Mode is not (GateMode.Closed or GateMode.Locked);

    public bool CanToggleLock => true;

    public bool IsShowingOpenState => Mode is GateMode.ForcedOpen or GateMode.TemporaryOpen;
}