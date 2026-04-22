using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Navigation;
using SmartParkingSystem.Maui.Services.Localization;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace;

public class WorkspacePageBase : ComponentBase, IDisposable
{
    [Inject]
    protected NavigationManager? NavigationManager { get; set; }

    [Inject]
    protected IJSRuntime? JsRuntime { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    protected WorkspacePageState State { get; } = new WorkspacePageState();

    protected AppHeaderTexts HeaderTexts => RequireLocalizationService().GetAppHeaderTexts();

    protected IReadOnlyList<AppHeaderNavItem> NavigationItems =>
    [
        new AppHeaderNavItem(
            "dashboard",
            "layout-dashboard",
            HeaderTexts.DashboardLabel,
            State.ActiveSection == WorkspaceSection.Dashboard),
        new AppHeaderNavItem(
            "parking",
            "circle-parking",
            HeaderTexts.ParkingLabel,
            State.ActiveSection == WorkspaceSection.Parking),
        new AppHeaderNavItem("gate", "rows-4", HeaderTexts.GateLabel, State.ActiveSection == WorkspaceSection.Gate),
        new AppHeaderNavItem(
            "monitor",
            "monitor",
            HeaderTexts.MonitorLabel,
            State.ActiveSection == WorkspaceSection.Monitor),
        new AppHeaderNavItem(
            "events",
            "history",
            HeaderTexts.EventsLabel,
            State.ActiveSection == WorkspaceSection.Events),
        new AppHeaderNavItem("admin", "shield", HeaderTexts.AdminLabel, State.ActiveSection == WorkspaceSection.Admin),
        new AppHeaderNavItem(
            "settings",
            "settings-2",
            HeaderTexts.SettingsLabel,
            State.ActiveSection == WorkspaceSection.Settings),
        new AppHeaderNavItem("exit", "log-out", HeaderTexts.ExitLabel, false)
    ];

    protected string HeaderClass => State.IsLeavingWorkspace
        ? "animate-exit-left rounded-md bg-white/85 p-5"
        : "rounded-md bg-white/85 p-5";

    protected string ContentClass => State.IsLeavingWorkspace
        ? "animate-exit-right opacity-100"
        : State.IsContentVisible
            ? "opacity-100 transition-opacity duration-[250ms] ease-out"
            : "opacity-0 transition-opacity duration-[250ms] ease-in";

    public void Dispose()
    {
        RequireLocalizationService().LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        RequireLocalizationService().LanguageChanged += OnLanguageChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || State.NeedsIconRefresh)
        {
            State.NeedsIconRefresh = false;
            await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
        }
    }

    protected async Task HandleNavigationAsync(string target)
    {
        if (State.IsLeavingWorkspace)
        {
            return;
        }

        if (target == "exit")
        {
            await HandleExitAsync();
            return;
        }

        var nextSection = target switch
        {
            "dashboard" => WorkspaceSection.Dashboard,
            "parking" => WorkspaceSection.Parking,
            "gate" => WorkspaceSection.Gate,
            "monitor" => WorkspaceSection.Monitor,
            "events" => WorkspaceSection.Events,
            "admin" => WorkspaceSection.Admin,
            "settings" => WorkspaceSection.Settings,
            _ => State.ActiveSection
        };

        if (nextSection == State.ActiveSection)
        {
            return;
        }

        if (State.IsSectionTransitioning)
        {
            return;
        }

        State.IsSectionTransitioning = true;
        try
        {
            State.ActiveSection = nextSection;
            State.IsContentVisible = false;
            await InvokeAsync(StateHasChanged);
            await Task.Delay(250);
            State.DisplayedSection = nextSection;
            State.NeedsIconRefresh = true;
            await InvokeAsync(StateHasChanged);
            await Task.Delay(250);
            State.IsContentVisible = true;
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            State.IsSectionTransitioning = false;
        }
    }

    protected async Task HandleLanguageChangedAsync()
    {
        State.NeedsIconRefresh = true;
        await InvokeAsync(StateHasChanged);
    }

    protected async Task HandleExitAsync()
    {
        if (State.IsLeavingWorkspace)
        {
            return;
        }

        State.IsLeavingWorkspace = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(500);
        RequireNavigationManager().NavigateTo("/");
    }

    protected async Task CloseApplicationAsync()
    {
        if (State.IsLeavingWorkspace)
        {
            return;
        }

        State.IsLeavingWorkspace = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(500);
        Environment.Exit(0);
    }

    private NavigationManager RequireNavigationManager()
    {
        return NavigationManager ?? throw new InvalidOperationException("Navigation manager is not available.");
    }

    private IJSRuntime RequireJsRuntime()
    {
        return JsRuntime ?? throw new InvalidOperationException("JavaScript runtime is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private void OnLanguageChanged()
    {
        _ = InvokeAsync(async () =>
        {
            State.NeedsIconRefresh = true;
            await HandleLanguageChangedAsync();
        });
    }
}