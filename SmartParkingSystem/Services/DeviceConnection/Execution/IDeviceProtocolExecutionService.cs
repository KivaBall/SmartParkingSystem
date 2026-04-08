namespace SmartParkingSystem.Services.DeviceConnection.Execution;

public interface IDeviceProtocolExecutionService
{
    Task RunExclusiveAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}