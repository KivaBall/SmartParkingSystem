using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class DashboardMetricCardBase : ComponentBase
{
    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;

    [Parameter]
    public string SurfaceClass { get; set; } = string.Empty;
}