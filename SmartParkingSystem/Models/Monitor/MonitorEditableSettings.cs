namespace SmartParkingSystem.Models.Monitor;

public sealed class MonitorEditableSettings
{
    public MonitorEditableSettings()
    {
    }

    public MonitorEditableSettings(
        bool forceEnabled,
        string forcedText,
        string defaultText,
        string allowedText,
        string blockedText,
        string invalidText,
        string lockedText)
    {
        ForceEnabled = forceEnabled;
        ForcedText = forcedText;
        DefaultText = defaultText;
        AllowedText = allowedText;
        BlockedText = blockedText;
        InvalidText = invalidText;
        LockedText = lockedText;
    }

    public bool ForceEnabled { get; set; }
    public string ForcedText { get; set; } = string.Empty;
    public string DefaultText { get; set; } = string.Empty;
    public string AllowedText { get; set; } = string.Empty;
    public string BlockedText { get; set; } = string.Empty;
    public string InvalidText { get; set; } = string.Empty;
    public string LockedText { get; set; } = string.Empty;

    public MonitorEditableSettings Clone()
    {
        return new MonitorEditableSettings(
            ForceEnabled,
            ForcedText,
            DefaultText,
            AllowedText,
            BlockedText,
            InvalidText,
            LockedText);
    }
}