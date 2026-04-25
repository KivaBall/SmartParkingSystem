namespace SmartParkingSystem.TelegramBot.Services.Commands;

public sealed class ConnectedDeviceHostRegistry
{
    private readonly Lock _sync = new Lock();
    private string? _activeConnectionId;

    public void SetActiveConnection(string connectionId)
    {
        lock (_sync)
        {
            _activeConnectionId = connectionId;
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            if (string.Equals(_activeConnectionId, connectionId, StringComparison.Ordinal))
            {
                _activeConnectionId = null;
            }
        }
    }

    public string? GetActiveConnection()
    {
        lock (_sync)
        {
            return _activeConnectionId;
        }
    }
}