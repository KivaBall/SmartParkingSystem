using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Maui.Components.Pages.Connection.Parts;

public class ConnectionModeTabsBase : ComponentBase
{
    [Parameter]
    public string AutomaticButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string AdvancedButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string AutomaticLabel { get; set; } = string.Empty;

    [Parameter]
    public string AdvancedLabel { get; set; } = string.Empty;

    [Parameter]
    public EventCallback OnAutomaticSelected { get; set; }

    [Parameter]
    public EventCallback OnAdvancedSelected { get; set; }
}