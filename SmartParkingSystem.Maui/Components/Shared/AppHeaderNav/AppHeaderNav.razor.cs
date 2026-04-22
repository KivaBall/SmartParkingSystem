using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Navigation;

namespace SmartParkingSystem.Maui.Components.Shared.AppHeaderNav;

public class AppHeaderNavBase : ComponentBase
{
    private string? _pendingTarget;

    [Parameter]
    public IReadOnlyList<AppHeaderNavItem> Items { get; set; } = [];

    [Parameter]
    public string ContainerClass { get; set; } = "rounded-md bg-white/85 p-5";

    [Parameter]
    public EventCallback<string> OnNavigateRequested { get; set; }

    protected string GetItemClass(AppHeaderNavItem item)
    {
        var isActive = _pendingTarget == item.Target || item.IsActive;
        var stateClass = isActive
            ? "bg-brand-200 hover:bg-brand-400"
            : "bg-brand-100/70 hover:bg-calm-100";
        var isExit = IsExitItem(item);
        var sizeClass = isExit
            ? "min-h-12 justify-center px-2 py-2 md:min-w-12 md:px-2"
            : "min-h-12 flex-1 justify-center px-2 py-2 md:justify-start md:px-4";

        return $"inline-flex items-center gap-2 rounded-md transition-all duration-500 ease-out {sizeClass} {stateClass
        }";
    }

    protected async Task HandleNavigateAsync(AppHeaderNavItem item)
    {
        _pendingTarget = item.Target;
        await InvokeAsync(StateHasChanged);
        await OnNavigateRequested.InvokeAsync(item.Target);
    }

    protected static bool IsExitItem(AppHeaderNavItem item)
    {
        return string.Equals(item.Target, "exit", StringComparison.OrdinalIgnoreCase);
    }
}