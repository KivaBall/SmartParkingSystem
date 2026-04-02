using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SmartParkingSystem.Components.Pages;

public class DashboardBase : ComponentBase
{
    protected static readonly IReadOnlyList<DashboardNavItem> NavigationItems =
    [
        new DashboardNavItem("/", "plug-zap", "Connection", "Device connection stage"),
        new DashboardNavItem("/dashboard", "layout-dashboard", "Dashboard", "System overview"),
        new DashboardNavItem("/dashboard", "circle-parking", "Parking", "Parking status section"),
        new DashboardNavItem("/dashboard", "waypoints", "Gate", "Gate control area"),
        new DashboardNavItem("/dashboard", "shield", "Admin", "Administrative tools")
    ];

    [Inject]
    protected IJSRuntime? JsRuntime { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RequireJsRuntime().InvokeVoidAsync("initializeLucideIcons");
        }
    }

    private IJSRuntime RequireJsRuntime()
    {
        return JsRuntime ?? throw new InvalidOperationException("JavaScript runtime is not available.");
    }

    protected sealed record DashboardNavItem(string Href, string Icon, string Label, string Description);
}