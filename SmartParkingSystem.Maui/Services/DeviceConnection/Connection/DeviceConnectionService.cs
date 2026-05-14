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
    private static readonly TimeSpan ConnectionAttemptTimeout = TimeSpan.FromSeconds(45);

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

        var targets = FilterAutoTargets(RankTargets(await transportService.DiscoverTargetsAsync()));
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
        return targets
            .OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ConnectionTarget> FilterAutoTargets(IReadOnlyList<ConnectionTarget> targets)
    {
        var likelyControllerTargets = targets
            .Where(IsLikelyControllerTarget)
            .ToArray();

        return likelyControllerTargets.Length > 0 ? likelyControllerTargets : targets;
    }

    private static bool IsLikelyControllerTarget(ConnectionTarget target)
    {
        var value = $"{target.Label} {target.Id}";
        return value.Contains("HC-05", StringComparison.OrdinalIgnoreCase)
               || value.Contains("HC-06", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Arduino", StringComparison.OrdinalIgnoreCase)
               || value.Contains("SPS", StringComparison.OrdinalIgnoreCase)
               || value.Contains("SmartParking", StringComparison.OrdinalIgnoreCase);
    }

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
