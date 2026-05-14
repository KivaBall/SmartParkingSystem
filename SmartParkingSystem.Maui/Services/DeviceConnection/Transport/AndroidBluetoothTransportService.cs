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
    private static readonly TimeSpan OpenWarmupDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _lineSync = new();
    private readonly Queue<string> _completedLines = new();
    private readonly SemaphoreSlim _lineSignal = new(0);
    private readonly StringBuilder _partialLine = new();

    private BluetoothSocket? _bluetoothSocket;
    private Stream? _inputStream;
    private Stream? _outputStream;
    private CancellationTokenSource? _readerCancellationTokenSource;
    private Task? _readerTask;

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

        var targets = (adapter.BondedDevices?.ToArray() ?? [])
            .Select(device =>
            {
                var address = device.Address ?? string.Empty;
                var label = string.IsNullOrWhiteSpace(device.Name)
                    ? address
                    : $"{device.Name} ({address})";

                return new ConnectionTarget(address, label);
            })
            .Where(target => !string.IsNullOrWhiteSpace(target.Id))
            .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ConnectionTarget>>(targets);
    }

    public async Task<bool> OpenAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await CloseAsync(cancellationToken);
        ResetLines();

        var adapter = GetBluetoothAdapter();
        if (adapter is null || !adapter.IsEnabled)
        {
            return false;
        }

        var device = (adapter.BondedDevices?.ToArray() ?? [])
            .FirstOrDefault(candidate => string.Equals(
                candidate.Address,
                targetId,
                StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            return false;
        }

        adapter.CancelDiscovery();

        var socket = await OpenSocketAsync(device, cancellationToken);
        if (socket is null)
        {
            return false;
        }

        _bluetoothSocket = socket;
        _inputStream = socket.InputStream;
        _outputStream = socket.OutputStream;
        ActiveTargetId = targetId;
        StartReader();

        await Task.Delay(OpenWarmupDelay, cancellationToken);
        return true;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _readerCancellationTokenSource?.Cancel();

        CloseSocket();

        if (_readerTask is not null)
        {
            try
            {
                await _readerTask.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch
            {
                // Closing the socket is enough to stop Android's blocking read in normal cases.
            }
        }

        _readerTask = null;
        _readerCancellationTokenSource?.Dispose();
        _readerCancellationTokenSource = null;
        ActiveTargetId = null;
        ResetLines();
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (_outputStream is null || !IsOpen)
        {
            throw new InvalidOperationException("Transport is not open.");
        }

        var payload = Encoding.ASCII.GetBytes($"{DeviceTransportFraming.WrapPayload(line)}\n");
        await _outputStream.WriteAsync(payload, cancellationToken);
        await _outputStream.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsOpen && _lineSignal.CurrentCount <= 0)
        {
            return null;
        }

        if (!await _lineSignal.WaitAsync(timeout, cancellationToken))
        {
            return null;
        }

        return TryDequeueLine(out var line)
            ? DeviceTransportFraming.UnwrapPayload(line)
            : null;
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

    private async Task<BluetoothSocket?> OpenSocketAsync(
        BluetoothDevice device,
        CancellationToken cancellationToken)
    {
        var attempts = new (string Label, Func<BluetoothSocket?> CreateSocket)[]
        {
            ("secure RFCOMM", () => device.CreateRfcommSocketToServiceRecord(SerialPortProfileUuid)),
            ("insecure RFCOMM", () => device.CreateInsecureRfcommSocketToServiceRecord(SerialPortProfileUuid))
        };

        foreach (var attempt in attempts)
        {
            var socket = await TryOpenSocketAsync(attempt.Label, attempt.CreateSocket, cancellationToken);
            if (socket is not null)
            {
                return socket;
            }
        }

        return null;
    }

    private static async Task<BluetoothSocket?> TryOpenSocketAsync(
        string attemptLabel,
        Func<BluetoothSocket?> createSocket,
        CancellationToken cancellationToken)
    {
        BluetoothSocket? socket = null;

        try
        {
            socket = createSocket();
            if (socket is null)
            {
                return null;
            }

            var connectTask = Task.Run(socket.Connect);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeout, cancellationToken));
            if (completedTask != connectTask)
            {
                CleanupSocketInBackground(socket);
                return null;
            }

            await connectTask;
            return socket.IsConnected ? socket : null;
        }
        catch
        {
            if (socket is not null)
            {
                CleanupSocket(socket);
            }

            return null;
        }
    }

    private void StartReader()
    {
        _readerCancellationTokenSource?.Cancel();
        _readerCancellationTokenSource?.Dispose();
        _readerCancellationTokenSource = new CancellationTokenSource();
        _readerTask = Task.Run(() => RunReaderAsync(_readerCancellationTokenSource.Token));
    }

    private async Task RunReaderAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[256];

        while (!cancellationToken.IsCancellationRequested)
        {
            var inputStream = _inputStream;
            if (inputStream is null)
            {
                return;
            }

            int bytesRead;
            try
            {
                bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch
            {
                return;
            }

            if (bytesRead <= 0)
            {
                await Task.Delay(20, cancellationToken);
                continue;
            }

            AppendReceivedText(Encoding.ASCII.GetString(buffer, 0, bytesRead));
        }
    }

    private void AppendReceivedText(string text)
    {
        lock (_lineSync)
        {
            foreach (var character in text)
            {
                if (character == '\r')
                {
                    continue;
                }

                if (character == '\n')
                {
                    EnqueueCurrentLine();
                    continue;
                }

                _partialLine.Append(character);
            }
        }
    }

    private void EnqueueCurrentLine()
    {
        var line = _partialLine.ToString().Trim();
        _partialLine.Clear();

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _completedLines.Enqueue(line);
        _lineSignal.Release();
    }

    private bool TryDequeueLine(out string line)
    {
        lock (_lineSync)
        {
            if (_completedLines.Count > 0)
            {
                line = _completedLines.Dequeue();
                return true;
            }
        }

        line = string.Empty;
        return false;
    }

    private void ResetLines()
    {
        lock (_lineSync)
        {
            _completedLines.Clear();
            _partialLine.Clear();
            while (_lineSignal.CurrentCount > 0)
            {
                _lineSignal.Wait(0);
            }
        }
    }

    private void CloseSocket()
    {
        try
        {
            _inputStream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _outputStream?.Dispose();
        }
        catch
        {
        }

        if (_bluetoothSocket is not null)
        {
            CleanupSocket(_bluetoothSocket);
        }

        _bluetoothSocket = null;
        _inputStream = null;
        _outputStream = null;
    }

    private static void CleanupSocketInBackground(BluetoothSocket socket)
    {
        _ = Task.Run(() => CleanupSocket(socket));
    }

    private static void CleanupSocket(BluetoothSocket socket)
    {
        try
        {
            socket.Close();
        }
        catch
        {
        }

        socket.Dispose();
    }

    private static BluetoothAdapter? GetBluetoothAdapter()
    {
        var manager = Application.Context.GetSystemService(Context.BluetoothService) as BluetoothManager;
        return manager?.Adapter;
    }
}
#endif
