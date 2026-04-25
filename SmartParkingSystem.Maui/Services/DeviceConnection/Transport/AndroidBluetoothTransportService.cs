#if ANDROID
using System.Text;
using Android.Bluetooth;
using Android.Content;
using Java.Util;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using Application = Android.App.Application;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

public sealed class AndroidBluetoothTransportService : IDeviceTransportService
{
    private static readonly UUID SerialPortProfileUuid = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")
                                                         ?? throw new InvalidOperationException(
                                                             "The Bluetooth SPP UUID could not be created.");

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private readonly StringBuilder _readBuffer = new StringBuilder();

    private BluetoothSocket? _bluetoothSocket;
    private Stream? _inputStream;
    private Stream? _outputStream;

    public bool IsOpen => _bluetoothSocket?.IsConnected == true;

    public string? ActiveTargetId { get; private set; }

    public Task<IReadOnlyList<ConnectionTarget>> DiscoverTargetsAsync(
        CancellationToken cancellationToken = default)
    {
        var adapter = GetBluetoothAdapter();
        if (adapter is null || !adapter.IsEnabled)
        {
            return Task.FromResult<IReadOnlyList<ConnectionTarget>>([]);
        }

        var targets = adapter.BondedDevices?
            .Select(device =>
            {
                var address = device.Address ?? string.Empty;
                var label = string.IsNullOrWhiteSpace(device.Name)
                    ? address
                    : $"{device.Name} ({address})";

                return new ConnectionTarget(address, label);
            })
            .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .Where(target => !string.IsNullOrWhiteSpace(target.Id))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ConnectionTarget>>(targets ?? []);
    }

    public async Task<bool> OpenAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await CloseAsync(cancellationToken);

        var adapter = GetBluetoothAdapter();
        if (adapter is null || !adapter.IsEnabled)
        {
            return false;
        }

        var device = adapter.BondedDevices?.FirstOrDefault(candidate => string.Equals(
            candidate.Address,
            targetId,
            StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            return false;
        }

        BluetoothSocket? candidateSocket = null;

        try
        {
            adapter.CancelDiscovery();
            candidateSocket = await TryOpenSocketAsync(
                device,
                candidate => candidate.CreateRfcommSocketToServiceRecord(SerialPortProfileUuid),
                cancellationToken);

            if (candidateSocket is null)
            {
                candidateSocket = await TryOpenSocketAsync(
                    device,
                    candidate => candidate.CreateInsecureRfcommSocketToServiceRecord(SerialPortProfileUuid),
                    cancellationToken);
            }

            if (candidateSocket is null)
            {
                return false;
            }

            _bluetoothSocket = candidateSocket;
            _inputStream = candidateSocket.InputStream;
            _outputStream = candidateSocket.OutputStream;
            ActiveTargetId = targetId;
            _readBuffer.Clear();

            await Task.Delay(300, cancellationToken);
            return true;
        }
        catch
        {
            if (candidateSocket is not null)
            {
                try
                {
                    candidateSocket.Close();
                }
                catch
                {
                    // Socket close is best-effort after a failed open attempt.
                }

                candidateSocket.Dispose();
            }

            _bluetoothSocket = null;
            _inputStream = null;
            _outputStream = null;
            ActiveTargetId = null;
            _readBuffer.Clear();
            return false;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _inputStream?.Dispose();
        _outputStream?.Dispose();

        if (_bluetoothSocket is not null)
        {
            try
            {
                _bluetoothSocket.Close();
            }
            catch
            {
                // Android Bluetooth sockets can already be closed by the platform at this point.
            }

            _bluetoothSocket.Dispose();
        }

        _bluetoothSocket = null;
        _inputStream = null;
        _outputStream = null;
        ActiveTargetId = null;
        _readBuffer.Clear();
        return Task.CompletedTask;
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (_outputStream is null || !IsOpen)
        {
            throw new InvalidOperationException("Transport is not open.");
        }

        var framedLine = $"{DeviceTransportFraming.WrapPayload(line)}\n";
        var payload = Encoding.ASCII.GetBytes(framedLine);
        await _outputStream.WriteAsync(payload, cancellationToken);
        await _outputStream.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_inputStream is null || !IsOpen)
        {
            return null;
        }

        var line = TryReadBufferedLine();
        if (line is not null)
        {
            return DeviceTransportFraming.UnwrapPayload(line);
        }

        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var tempBuffer = new byte[256];

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            try
            {
                var bytesRead = await _inputStream
                    .ReadAsync(tempBuffer.AsMemory(0, tempBuffer.Length), cancellationToken)
                    .AsTask()
                    .WaitAsync(remaining, cancellationToken);

                if (bytesRead > 0)
                {
                    _readBuffer.Append(Encoding.ASCII.GetString(tempBuffer, 0, bytesRead));
                    line = TryReadBufferedLine();
                    if (line is not null)
                    {
                        return DeviceTransportFraming.UnwrapPayload(line);
                    }
                }
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        return null;
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

    private static async Task<BluetoothSocket?> TryOpenSocketAsync(
        BluetoothDevice device,
        Func<BluetoothDevice, BluetoothSocket?> socketFactory,
        CancellationToken cancellationToken)
    {
        BluetoothSocket? candidateSocket = null;

        try
        {
            candidateSocket = socketFactory(device);
            if (candidateSocket is null)
            {
                return null;
            }

            var connectTask = Task.Run(() => candidateSocket.Connect(), cancellationToken);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeout, cancellationToken));
            if (completedTask != connectTask)
            {
                CleanupCandidateSocket(candidateSocket);
                return null;
            }

            await connectTask;
            return candidateSocket;
        }
        catch
        {
            if (candidateSocket is not null)
            {
                CleanupCandidateSocket(candidateSocket);
            }

            return null;
        }
    }

    private static void CleanupCandidateSocket(BluetoothSocket candidateSocket)
    {
        try
        {
            candidateSocket.Close();
        }
        catch
        {
            // Socket close is best-effort during fallback cleanup.
        }

        candidateSocket.Dispose();
    }

    private static BluetoothAdapter? GetBluetoothAdapter()
    {
        var manager = Application.Context.GetSystemService(Context.BluetoothService) as BluetoothManager;
        return manager?.Adapter;
    }

    private string? TryReadBufferedLine()
    {
        for (var index = 0; index < _readBuffer.Length; index++)
        {
            if (_readBuffer[index] != '\n')
            {
                continue;
            }

            var line = _readBuffer.ToString(0, index).Trim();
            _readBuffer.Remove(0, index + 1);
            return line;
        }

        return null;
    }
}
#endif