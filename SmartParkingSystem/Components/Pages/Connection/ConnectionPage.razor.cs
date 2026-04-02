using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Models.Localization;
using SmartParkingSystem.Services.DeviceConnection;
using SmartParkingSystem.Services.Localization;

namespace SmartParkingSystem.Components.Pages.Connection;

public class ConnectionPageBase : ComponentBase
{
    [Inject]
    protected IDeviceConnectionService? ConnectionService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected NavigationManager? NavigationManager { get; set; }

    [Inject]
    protected IJSRuntime? JsRuntime { get; set; }

    protected ConnectionPageState State { get; } = new ConnectionPageState();
    protected ConnectionTexts Texts => RequireLocalizationService().GetConnectionTexts(State.SelectedLanguage);

    private ConnectionPageCoordinator Coordinator =>
        field ??= new ConnectionPageCoordinator(RequireConnectionService());

    protected static string PrimaryButtonClass => ConnectionPageStyles.PrimaryButtonClass;
    protected static string SecondaryButtonClass => ConnectionPageStyles.SecondaryButtonClass;
    protected static string WarningButtonClass => ConnectionPageStyles.WarningButtonClass;

    protected string LeftPanelClass => ConnectionPageStyles.GetLeftPanelClass(State.IsLeavingPage);
    protected string RightPanelClass => ConnectionPageStyles.GetRightPanelClass(State.IsLeavingPage);

    protected override async Task OnInitializedAsync()
    {
        await Coordinator.InitializeAsync(State, Texts);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
        }
    }

    protected async Task SetMode(ConnectionMode mode)
    {
        await ConnectionPageCoordinator.SetModeAsync(State, mode, () => InvokeAsync(StateHasChanged));
    }

    protected void SetLanguage(AppLanguage language)
    {
        ConnectionPageCoordinator.SetLanguage(
            State,
            language,
            RequireLocalizationService().GetConnectionTexts(language));
    }

    protected string GetModeButtonClass(ConnectionMode mode)
    {
        return ConnectionPageStyles.GetModeButtonClass(mode, State.ActiveTabMode);
    }

    protected string GetLanguageButtonClass(AppLanguage language)
    {
        return ConnectionPageStyles.GetLanguageButtonClass(language, State.SelectedLanguage);
    }

    protected string GetModeContentClass()
    {
        return ConnectionPageStyles.GetModeContentClass(State.IsModeContentVisible);
    }

    protected async Task TryAutoConnectAsync()
    {
        var result = await Coordinator.TryAutoConnectAsync(State, Texts);
        await HandleConnectionResultAsync(result, ConnectionMode.Automatic);
    }

    protected async Task TryManualConnectAsync()
    {
        var result = await Coordinator.TryManualConnectAsync(State, Texts);
        await HandleConnectionResultAsync(result, ConnectionMode.Advanced);
    }

    protected async Task RefreshTargetsAsync()
    {
        await Coordinator.RefreshTargetsAsync(State, Texts);
        await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
    }

    protected Task OnSelectedTargetChanged(string? targetId)
    {
        State.SelectedTargetId = targetId;
        return Task.CompletedTask;
    }

    private async Task HandleConnectionResultAsync(ConnectionResult result, ConnectionMode mode)
    {
        await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
        if (!ConnectionPageCoordinator.ApplyConnectionResult(State, result, mode, Texts))
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
        await Task.Delay(ConnectionPageTimings.PageExitMilliseconds);
        State.IsBusy = false;
        RequireNavigationManager().NavigateTo("/dashboard");
    }

    private IDeviceConnectionService RequireConnectionService()
    {
        return ConnectionService ?? throw new InvalidOperationException("Device connection service is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private NavigationManager RequireNavigationManager()
    {
        return NavigationManager ?? throw new InvalidOperationException("Navigation manager is not available.");
    }

    private IJSRuntime RequireJsRuntime()
    {
        return JsRuntime ?? throw new InvalidOperationException("JavaScript runtime is not available.");
    }
}