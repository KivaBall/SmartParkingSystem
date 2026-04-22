namespace SmartParkingSystem.Maui.Services.DeviceConnection.Execution;

public sealed class DeviceProtocolExecutionService : IDeviceProtocolExecutionService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            await action(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}