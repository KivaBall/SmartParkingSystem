using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Maui.Services.DeviceConnection.Permissions;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

namespace SmartParkingSystem.Maui.Services.DeviceConnection.Connection;

public sealed class DeviceConnectionService(
    IDevicePermissionService permissionService,
    IDeviceTransportService transportService,
    IDeviceSessionService sessionService) : IDeviceConnectionService
{
    private static readonly TimeSpan ConnectionAttemptTimeout = TimeSpan.FromSeconds(20);
#if WINDOWS
    private static readonly string[] PreferredWindowsTargetOrder = ["COM6", "COM3", "COM4", "COM7"];
#endif

    public async Task<IReadOnlyList<ConnectionTarget>> GetTargetsAsync()
    {
        if (!await HasConnectionPermissionsAsync())
        {
            return [];
        }

        var targets = await transportService.DiscoverTargetsAsync();
        return RankTargets(targets);
    }

    public async Task<ConnectionResult> TryAutoConnectAsync()
    {
        if (!await HasConnectionPermissionsAsync())
        {
            return new ConnectionResult(false);
        }

        var targets = RankTargets(await transportService.DiscoverTargetsAsync());
        return await RunWithTimeoutAsync(token => sessionService.TryAutoOpenSessionAsync(targets, token));
    }

    public async Task<ConnectionResult> TryConnectAsync(string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || !await HasConnectionPermissionsAsync())
        {
            return new ConnectionResult(false);
        }

        return await RunWithTimeoutAsync(token => sessionService.TryOpenSessionAsync(targetId, token));
    }

    public async Task<IReadOnlyList<ConnectionTarget>> RefreshTargetsAsync()
    {
        if (!await HasConnectionPermissionsAsync())
        {
            return [];
        }

        var targets = await transportService.DiscoverTargetsAsync();
        return RankTargets(targets);
    }

    private Task<bool> HasConnectionPermissionsAsync()
    {
        return permissionService.EnsureConnectionPermissionsAsync();
    }

    private static IReadOnlyList<ConnectionTarget> RankTargets(IReadOnlyList<ConnectionTarget> targets)
    {
#if WINDOWS
        return targets
            .OrderBy(target => GetWindowsTargetPriority(target.Id))
            .ThenBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
#else
        return targets;
#endif
    }

#if WINDOWS
    private static int GetWindowsTargetPriority(string targetId)
    {
        for (var index = 0; index < PreferredWindowsTargetOrder.Length; index++)
        {
            if (string.Equals(targetId, PreferredWindowsTargetOrder[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return PreferredWindowsTargetOrder.Length;
    }
#endif

    private async Task<ConnectionResult> RunWithTimeoutAsync(
        Func<CancellationToken, Task<ConnectionResult>> action)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(ConnectionAttemptTimeout);

        try
        {
            return await action(timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            await sessionService.DisconnectAsync();
            return new ConnectionResult(false);
        }
        catch
        {
            await sessionService.DisconnectAsync();
            return new ConnectionResult(false);
        }
    }
}