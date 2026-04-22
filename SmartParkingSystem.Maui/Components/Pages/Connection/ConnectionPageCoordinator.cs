using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.DeviceConnection.Connection;

namespace SmartParkingSystem.Maui.Components.Pages.Connection;

public sealed class ConnectionPageCoordinator(IDeviceConnectionService connectionService)
{
    public async Task InitializeAsync(ConnectionPageState state, ConnectionTexts texts)
    {
        SyncDescriptions(state, texts);
        state.Targets = await connectionService.GetTargetsAsync();
        state.SelectedTargetId = state.Targets.Count > 0 ? state.Targets[0].Id : null;
    }

    public static async Task SetModeAsync(ConnectionPageState state, ConnectionMode mode, Func<Task> requestRenderAsync)
    {
        if (mode == state.SelectedMode || state.IsBusy)
        {
            return;
        }

        state.ActiveTabMode = mode;
        state.IsModeContentVisible = false;
        await requestRenderAsync();
        await Task.Delay(ConnectionPageTimings.ModeFadeOutMilliseconds);
        state.SelectedMode = mode;
        await requestRenderAsync();
        await Task.Delay(ConnectionPageTimings.ModeContentSwapDelayMilliseconds);
        state.IsModeContentVisible = true;
    }

    public static void SetLanguage(ConnectionPageState state, AppLanguage language, ConnectionTexts texts)
    {
        if (state.SelectedLanguage == language)
        {
            return;
        }

        state.SelectedLanguage = language;
        SyncDescriptions(state, texts);
    }

    public static void SetAutoConnectBusy(ConnectionPageState state, ConnectionTexts texts)
    {
        state.IsBusy = true;
        state.AutomaticDescription = texts.AutomaticSearchingDescription;
    }

    public static void SetManualConnectBusy(ConnectionPageState state, ConnectionTexts texts)
    {
        state.IsBusy = true;
        state.AdvancedDescription = texts.AdvancedConnectingDescription;
    }

    public static void SetRefreshBusy(ConnectionPageState state, ConnectionTexts texts)
    {
        state.IsBusy = true;
        state.AdvancedDescription = texts.AdvancedRefreshingDescription;
    }

    public static bool ApplyConnectionResult(
        ConnectionPageState state,
        ConnectionResult result,
        ConnectionMode mode,
        ConnectionTexts texts)
    {
        if (!result.IsSuccessful)
        {
            state.IsBusy = false;

            if (mode == ConnectionMode.Automatic)
            {
                state.AutomaticDescription = texts.AutomaticFailedDescription;
            }
            else
            {
                state.AdvancedDescription = texts.AdvancedFailedDescription;
            }

            return false;
        }

        if (mode == ConnectionMode.Automatic)
        {
            state.AutomaticDescription = texts.AutomaticSuccessDescription;
        }
        else
        {
            state.AdvancedDescription = texts.AdvancedSuccessDescription;
        }

        state.IsLeavingPage = true;
        return true;
    }

    private static void SyncDescriptions(ConnectionPageState state, ConnectionTexts texts)
    {
        state.AutomaticDescription = state.SelectedMode == ConnectionMode.Automatic && state.IsBusy
            ? texts.AutomaticSearchingDescription
            : texts.AutomaticIdleDescription;

        state.AdvancedDescription = state.IsBusy
            ? texts.AdvancedConnectingDescription
            : texts.AdvancedIdleDescription;
    }
}