using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Maui.Components.Pages.Connection.Parts;

public class ConnectionHeroBase : ComponentBase
{
    [Parameter]
    public string PanelClass { get; set; } = string.Empty;

    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;
}