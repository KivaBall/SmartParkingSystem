using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Components.Pages.Connection;

public sealed class ConnectionPageState
{
    public IReadOnlyList<ConnectionTarget> Targets { get; set; } = [];
    public string? SelectedTargetId { get; set; }
    public bool IsBusy { get; set; }
    public ConnectionMode SelectedMode { get; set; } = ConnectionMode.Automatic;
    public ConnectionMode ActiveTabMode { get; set; } = ConnectionMode.Automatic;
    public AppLanguage SelectedLanguage { get; set; } = AppLanguage.English;
    public bool IsModeContentVisible { get; set; } = true;
    public bool IsLeavingPage { get; set; }
    public string AutomaticDescription { get; set; } = string.Empty;
    public string AdvancedDescription { get; set; } = string.Empty;
}