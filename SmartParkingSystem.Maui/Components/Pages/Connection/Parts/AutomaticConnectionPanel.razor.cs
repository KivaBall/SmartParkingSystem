using Microsoft.AspNetCore.Components;

namespace SmartParkingSystem.Maui.Components.Pages.Connection.Parts;

public class AutomaticConnectionPanelBase : ComponentBase
{
    [Parameter]
    public string ContentClass { get; set; } = string.Empty;

    [Parameter]
    public string PrimaryButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string BluetoothButtonText { get; set; } = string.Empty;

    [Parameter]
    public string UsbButtonText { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnConnectBluetooth { get; set; }

    [Parameter]
    public EventCallback OnConnectUsb { get; set; }
}
