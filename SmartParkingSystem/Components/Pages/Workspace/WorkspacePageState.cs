namespace SmartParkingSystem.Components.Pages.Workspace;

public sealed class WorkspacePageState
{
    public WorkspaceSection ActiveSection { get; set; } = WorkspaceSection.Dashboard;
    public WorkspaceSection DisplayedSection { get; set; } = WorkspaceSection.Dashboard;
    public bool IsContentVisible { get; set; } = true;
    public bool IsLeavingWorkspace { get; set; }
    public bool IsSectionTransitioning { get; set; }
    public bool NeedsIconRefresh { get; set; } = true;
}