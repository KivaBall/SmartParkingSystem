using SmartParkingSystem.Models.DeviceConnection;

namespace SmartParkingSystem.Services.DeviceConnection.Transport;

public sealed class SerialDeviceTransportService : IDeviceTransportService
{
#if WINDOWS
    private static readonly TimeSpan OpenWarmupDelay = TimeSpan.FromMilliseconds(1500);
    private System.IO.Ports.SerialPort? serialPort;
#endif

    public bool IsOpen
    {
        get
        {
#if WINDOWS
            return serialPort?.IsOpen == true;
#else
            return false;
#endif
        }
    }

    public string? ActiveTargetId { get; private set; }

    public Task<IReadOnlyList<ConnectionTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        var targets = System.IO.Ports.SerialPort
            .GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new ConnectionTarget(name, name))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ConnectionTarget>>(targets);
#else
        return Task.FromResult<IReadOnlyList<ConnectionTarget>>([]);
#endif
    }

    public async Task<bool> OpenAsync(string targetId, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        await CloseAsync(cancellationToken);

        var candidate = new System.IO.Ports.SerialPort(targetId, 9600)
        {
            NewLine = "\n",
            ReadTimeout = 500,
            WriteTimeout = 2000,
            DtrEnable = false,
            RtsEnable = false
        };

        try
        {
            await Task.Run(candidate.Open, cancellationToken);
            serialPort = candidate;
            ActiveTargetId = targetId;
            await Task.Delay(OpenWarmupDelay, cancellationToken);
            await Task.Run(() =>
            {
                candidate.DiscardInBuffer();
                candidate.DiscardOutBuffer();
            }, cancellationToken);
            return true;
        }
        catch
        {
            candidate.Dispose();
            ActiveTargetId = null;
            return false;
        }
#else
        await Task.CompletedTask;
        ActiveTargetId = null;
        return false;
#endif
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        if (serialPort is not null)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            finally
            {
                serialPort.Dispose();
                serialPort = null;
            }
        }
#endif

        ActiveTargetId = null;
        return Task.CompletedTask;
    }

    public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        if (serialPort is null || !serialPort.IsOpen)
        {
            throw new InvalidOperationException("Transport is not open.");
        }

        return Task.Run(() =>
        {
            serialPort.Write($"{line}\n");
        }, cancellationToken);
#else
        throw new PlatformNotSupportedException("Serial transport is not implemented on this platform.");
#endif
    }

    public Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        if (serialPort is null || !serialPort.IsOpen)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.Run(() =>
        {
            try
            {
                serialPort.ReadTimeout = (int)Math.Max(timeout.TotalMilliseconds, 1);
                return serialPort.ReadLine()?.Trim();
            }
            catch (TimeoutException)
            {
                return null;
            }
        }, cancellationToken);
#else
        return Task.FromResult<string?>(null);
#endif
    }

    public async Task DrainAsync(TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var line = await ReadLineAsync(idleTimeout, cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }
        }
    }
}