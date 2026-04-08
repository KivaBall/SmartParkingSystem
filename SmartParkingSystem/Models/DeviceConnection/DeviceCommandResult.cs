namespace SmartParkingSystem.Models.DeviceConnection;

public sealed record DeviceCommandResult(
    bool Succeeded,
    string Scope,
    string? ResponseLine,
    DeviceCommandFailureKind FailureKind)
{
    public static DeviceCommandResult Success(string scope, string? responseLine)
    {
        return new DeviceCommandResult(true, scope, responseLine, DeviceCommandFailureKind.None);
    }

    public static DeviceCommandResult Failure(
        string scope,
        DeviceCommandFailureKind failureKind,
        string? responseLine = null)
    {
        return new DeviceCommandResult(false, scope, responseLine, failureKind);
    }
}