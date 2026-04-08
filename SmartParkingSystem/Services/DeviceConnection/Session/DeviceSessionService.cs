using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Services.DeviceConnection.Protocol;
using SmartParkingSystem.Services.DeviceConnection.Telemetry;
using SmartParkingSystem.Services.DeviceConnection.Transport;

namespace SmartParkingSystem.Services.DeviceConnection.Session;

public sealed class DeviceSessionService(
    IDeviceTransportService transportService,
    IDeviceTelemetryService telemetryService) : IDeviceSessionService
{
    private const int HelloAttempts = 3;
    private const int HelloAttemptWindowMs = 1800;
    private const int AutoConnectionPasses = 3;
    private static readonly TimeSpan InitialDrainWindow = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan HelloReadTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan AutoRetryDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DefaultSnapshotRefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumSnapshotRefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly object _sessionSync = new object();
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private Task? _refreshLoopTask;

    public event Action<DeviceControllerSession?>? SessionChanged;

    public DeviceControllerSession? CurrentSession { get; private set; }

    public bool IsValidated => CurrentSession is not null;

    public async Task<ConnectionResult> TryOpenSessionAsync(
        string targetId,
        CancellationToken cancellationToken = default)
    {
        return await TryOpenSessionAsync(
            new ConnectionTarget(targetId, targetId),
            cancellationToken);
    }

    public async Task<ConnectionResult> TryAutoOpenSessionAsync(
        IReadOnlyList<ConnectionTarget> targets,
        CancellationToken cancellationToken = default)
    {
        for (var pass = 0; pass < AutoConnectionPasses; pass++)
        {
            foreach (var target in targets)
            {
                var result = await TryOpenSessionAsync(target, cancellationToken);
                if (result.IsSuccessful)
                {
                    return result;
                }

                if (pass < AutoConnectionPasses - 1)
                {
                    await Task.Delay(AutoRetryDelay, cancellationToken);
                }
            }
        }

        return new ConnectionResult(false);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await StopBackgroundRefreshLoopAsync();
        SetCurrentSession(null);
        await transportService.CloseAsync(cancellationToken);
    }

    public async Task<DeviceControllerConfiguration?> RefreshConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        var session = CurrentSession;
        if (session is null)
        {
            return null;
        }

        var configuration = await telemetryService.GetConfigurationAsync(
            session.Profile.SlotCount,
            cancellationToken);

        if (configuration is null)
        {
            return null;
        }

        var updatedSession = UpdateCurrentSession(current => current is null
            ? null
            : current with { Configuration = configuration });

        return updatedSession?.Configuration;
    }

    public async Task<DeviceControllerSnapshot?> RefreshSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var session = CurrentSession;
        if (session is null)
        {
            return null;
        }

        var snapshot = await telemetryService.GetSnapshotAsync(
            session.Profile.SlotCount,
            cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var updatedSession = UpdateCurrentSession(current => current is null
            ? null
            : current with { Snapshot = snapshot });

        return updatedSession?.Snapshot;
    }

    public async Task<DeviceControllerSession?> RefreshSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var session = CurrentSession;
        if (session is null)
        {
            return null;
        }

        var configuration = await telemetryService.GetConfigurationAsync(
            session.Profile.SlotCount,
            cancellationToken);
        var snapshot = await telemetryService.GetSnapshotAsync(
            session.Profile.SlotCount,
            cancellationToken);
        if (configuration is null || snapshot is null)
        {
            return CurrentSession;
        }

        return UpdateCurrentSession(current => current is null
            ? null
            : current with
            {
                Configuration = configuration,
                Snapshot = snapshot
            });
    }

    private async Task<ConnectionResult> TryOpenSessionAsync(
        ConnectionTarget target,
        CancellationToken cancellationToken)
    {
        await DisconnectAsync(cancellationToken);

        if (!await transportService.OpenAsync(target.Id, cancellationToken))
        {
            return new ConnectionResult(false);
        }

        if (!await WaitForHelloAsync(cancellationToken))
        {
            await DisconnectAsync(cancellationToken);
            return new ConnectionResult(false);
        }

        var profile = await telemetryService.GetProfileAsync(cancellationToken);
        if (profile is null)
        {
            await DisconnectAsync(cancellationToken);
            return new ConnectionResult(false);
        }

        var configuration = await telemetryService.GetConfigurationAsync(profile.SlotCount, cancellationToken);
        if (configuration is null)
        {
            await DisconnectAsync(cancellationToken);
            return new ConnectionResult(false);
        }

        var snapshot = await telemetryService.GetSnapshotAsync(profile.SlotCount, cancellationToken);
        if (snapshot is null)
        {
            await DisconnectAsync(cancellationToken);
            return new ConnectionResult(false);
        }

        SetCurrentSession(
            new DeviceControllerSession(
                target,
                profile,
                configuration,
                snapshot,
                DateTimeOffset.UtcNow));

        StartBackgroundRefreshLoop();

        return new ConnectionResult(true);
    }

    private async Task<bool> WaitForHelloAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < HelloAttempts; attempt++)
        {
            await transportService.DrainAsync(InitialDrainWindow, cancellationToken);
            await transportService.SendLineAsync("HELLO SPS", cancellationToken);

            var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(HelloAttemptWindowMs);
            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                var line = await transportService.ReadLineAsync(HelloReadTimeout, cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (DeviceProtocolParser.IsHelloOk(line))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void StartBackgroundRefreshLoop()
    {
        _refreshCancellationTokenSource?.Cancel();
        _refreshCancellationTokenSource?.Dispose();

        _refreshCancellationTokenSource = new CancellationTokenSource();
        _refreshLoopTask = RunBackgroundRefreshLoopAsync(_refreshCancellationTokenSource.Token);
    }

    private async Task StopBackgroundRefreshLoopAsync()
    {
        if (_refreshCancellationTokenSource is null)
        {
            return;
        }

        _refreshCancellationTokenSource.Cancel();

        if (_refreshLoopTask is not null)
        {
            try
            {
                await _refreshLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _refreshLoopTask = null;
        _refreshCancellationTokenSource.Dispose();
        _refreshCancellationTokenSource = null;
    }

    private async Task RunBackgroundRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(GetSnapshotRefreshInterval(), cancellationToken);

                if (CurrentSession is null || !transportService.IsOpen)
                {
                    continue;
                }

                try
                {
                    await RefreshSnapshotAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Keep the last good session alive; the next ticks may recover.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private TimeSpan GetSnapshotRefreshInterval()
    {
        var configuredMs = CurrentSession?.Configuration.TelemetryIntervalMs ?? 0;
        if (configuredMs <= 0)
        {
            return DefaultSnapshotRefreshInterval;
        }

        return TimeSpan.FromMilliseconds(Math.Max(configuredMs, (int)MinimumSnapshotRefreshInterval.TotalMilliseconds));
    }

    private void SetCurrentSession(DeviceControllerSession? session)
    {
        UpdateCurrentSession(_ => session);
    }

    private DeviceControllerSession? UpdateCurrentSession(
        Func<DeviceControllerSession?, DeviceControllerSession?> update)
    {
        DeviceControllerSession? session;
        lock (_sessionSync)
        {
            session = update(CurrentSession);
            CurrentSession = session;
        }

        SessionChanged?.Invoke(session);
        return session;
    }
}