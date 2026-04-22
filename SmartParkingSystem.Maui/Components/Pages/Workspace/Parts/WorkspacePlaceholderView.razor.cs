using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspacePlaceholderViewBase : ComponentBase
{
    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;
}