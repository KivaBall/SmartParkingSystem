using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Models.DeviceConnection;

namespace SmartParkingSystem.Components.Pages.Connection.Parts;

public class AdvancedConnectionPanelBase : ComponentBase
{
    [Parameter]
    public string ContentClass { get; set; } = string.Empty;

    [Parameter]
    public string DeviceLabel { get; set; } = string.Empty;

    [Parameter]
    public string RefreshButtonText { get; set; } = string.Empty;

    [Parameter]
    public string ConnectButtonText { get; set; } = string.Empty;

    [Parameter]
    public string WarningButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string SecondaryButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public string? SelectedTargetId { get; set; }

    [Parameter]
    public EventCallback<string?> SelectedTargetIdChanged { get; set; }

    [Parameter]
    public IReadOnlyList<ConnectionTarget> Targets { get; set; } = [];

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    [Parameter]
    public EventCallback OnConnect { get; set; }
}