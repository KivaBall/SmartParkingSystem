using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Components.Pages.Connection.Parts;

public class AutomaticConnectionPanelBase : ComponentBase
{
    [Parameter]
    public string ContentClass { get; set; } = string.Empty;

    [Parameter]
    public string PrimaryButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string ButtonText { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnConnect { get; set; }
}