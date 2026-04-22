using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Maui.Services.DeviceConnection.Connection;
using SmartParkingSystem.Maui.Services.Localization;

namespace SmartParkingSystem.Maui.Components.Pages.Connection;

public class ConnectionPageBase : ComponentBase, IDisposable
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
    protected ConnectionTexts Texts => RequireLocalizationService().GetConnectionTexts();

    private ConnectionPageCoordinator Coordinator =>
        field ??= new ConnectionPageCoordinator(RequireConnectionService());

    protected static string PrimaryButtonClass => ConnectionPageStyles.PrimaryButtonClass;
    protected static string SecondaryButtonClass => ConnectionPageStyles.SecondaryButtonClass;
    protected static string WarningButtonClass => ConnectionPageStyles.WarningButtonClass;

    protected string LeftPanelClass => ConnectionPageStyles.GetLeftPanelClass(State.IsLeavingPage);
    protected string RightPanelClass => ConnectionPageStyles.GetRightPanelClass(State.IsLeavingPage);

    public void Dispose()
    {
        RequireLocalizationService().LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        RequireLocalizationService().LanguageChanged += OnLanguageChanged;
        State.SelectedLanguage = RequireLocalizationService().CurrentLanguage;
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
        RequireLocalizationService().CurrentLanguage = language;
        ConnectionPageCoordinator.SetLanguage(
            State,
            language,
            RequireLocalizationService().GetConnectionTexts());
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
        ConnectionPageCoordinator.SetAutoConnectBusy(State, Texts);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        var result = await RequireConnectionService().TryAutoConnectAsync();
        await HandleConnectionResultAsync(result, ConnectionMode.Automatic);
    }

    protected async Task TryManualConnectAsync()
    {
        ConnectionPageCoordinator.SetManualConnectBusy(State, Texts);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        var result = await RequireConnectionService().TryConnectAsync(State.SelectedTargetId);
        await HandleConnectionResultAsync(result, ConnectionMode.Advanced);
    }

    protected async Task RefreshTargetsAsync()
    {
        ConnectionPageCoordinator.SetRefreshBusy(State, Texts);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            State.Targets = await RequireConnectionService().RefreshTargetsAsync();
            State.SelectedTargetId = State.Targets.Count > 0 ? State.Targets[0].Id : null;
            State.AdvancedDescription = Texts.AdvancedIdleDescription;
        }
        catch
        {
            State.AdvancedDescription = Texts.AdvancedFailedDescription;
            throw;
        }
        finally
        {
            State.IsBusy = false;
        }

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
        RequireNavigationManager().NavigateTo("/workspace");
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

    private void OnLanguageChanged()
    {
        _ = InvokeAsync(() =>
        {
            ConnectionPageCoordinator.SetLanguage(
                State,
                RequireLocalizationService().CurrentLanguage,
                RequireLocalizationService().GetConnectionTexts());
            StateHasChanged();
        });
    }
}