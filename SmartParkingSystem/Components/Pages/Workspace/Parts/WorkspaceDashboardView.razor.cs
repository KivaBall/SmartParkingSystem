using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Models.Dashboard;
using SmartParkingSystem.Models.DeviceConnection;
using SmartParkingSystem.Models.Gate;
using SmartParkingSystem.Models.Localization;
using SmartParkingSystem.Services.Dashboard;
using SmartParkingSystem.Services.DeviceConnection.Session;
using SmartParkingSystem.Services.Localization;

namespace SmartParkingSystem.Components.Pages.Workspace.Parts;

public class WorkspaceDashboardViewBase : ComponentBase, IDisposable
{
    private static readonly TimeSpan InitialAnimationQuietWindow = TimeSpan.FromMilliseconds(900);
    private DateTimeOffset _updatesAllowedAt;

    [Inject]
    protected IDashboardService? DashboardService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    [Parameter]
    public EventCallback<string> OnSectionRequested { get; set; }

    protected DashboardSnapshot Snapshot { get; private set; } = CreateEmptySnapshot();

    protected DashboardTexts Texts => RequireLocalizationService().GetDashboardTexts();

    protected string HeroTitle => RequireLocalizationService().CurrentLanguage == AppLanguage.Ukrainian
        ? "Дашборд Системи Смарт Паркінгу"
        : Texts.HeroTitle;

    protected string PageClass => IsExiting
        ? "space-y-4 animate-exit-right"
        : "space-y-4";

    protected string HeroSectionClass => IsExiting
        ? "rounded-md bg-brand-100/80 p-6 sm:p-8 animate-exit-left"
        : "rounded-md bg-brand-100/80 p-6 sm:p-8 animate-page-enter-left opacity-0";

    protected string HeroSectionStyle => IsExiting ? "animation-delay: 540ms;" : "animation-delay: 0ms;";
    protected string BottomConnectionStyle => IsExiting ? "animation-delay: 240ms;" : "animation-delay: 360ms;";
    protected string BottomGateStyle => IsExiting ? "animation-delay: 120ms;" : "animation-delay: 480ms;";
    protected string BottomSystemStyle => IsExiting ? "animation-delay: 0ms;" : "animation-delay: 600ms;";

    protected string ConnectionSectionClass => IsExiting
        ? "rounded-md bg-brand-100/80 p-6 animate-exit-left"
        : "rounded-md bg-brand-100/80 p-6 animate-page-enter-left opacity-0";

    protected string GateSectionClass => IsExiting
        ? "rounded-md bg-brand-100/80 p-6 animate-exit-right"
        : "rounded-md bg-brand-100/80 p-6 animate-page-enter-right opacity-0";

    protected string SystemSectionClass => IsExiting
        ? "rounded-md bg-warm-100 p-6 animate-exit-right"
        : "rounded-md bg-warm-100 p-6 animate-page-enter-right opacity-0";

    public void Dispose()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    protected override Task OnInitializedAsync()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
        _updatesAllowedAt = DateTimeOffset.UtcNow.Add(InitialAnimationQuietWindow);
        Snapshot = RequireDashboardService().GetSnapshot();
        return Task.CompletedTask;
    }

    protected Task RefreshAsync()
    {
        Snapshot = RequireDashboardService().GetSnapshot();
        return Task.CompletedTask;
    }

    protected Task OpenParkingAsync()
    {
        return OnSectionRequested.InvokeAsync("parking");
    }

    protected Task OpenGateAsync()
    {
        return OnSectionRequested.InvokeAsync("gate");
    }

    protected Task OpenAdminAsync()
    {
        return OnSectionRequested.InvokeAsync("admin");
    }

    protected string GetConnectionChipClass()
    {
        return Snapshot.IsConnected
            ? "inline-flex min-h-12 items-center gap-2 rounded-md bg-mint-100 px-4 py-2 text-sm font-semibold text-mint-700"
            : "inline-flex min-h-12 items-center gap-2 rounded-md bg-warm-100 px-4 py-2 text-sm font-semibold text-warm-700";
    }

    protected string GetGateMetricSurfaceClass()
    {
        return Snapshot.GateMode switch
        {
            GateMode.ForcedOpen => "bg-brand-100/80",
            GateMode.TemporaryOpen => "bg-mint-100",
            GateMode.Locked => "bg-warm-100",
            _ => "bg-white/85"
        };
    }

    protected string GetGateStateLabel()
    {
        if (!Snapshot.IsConnected)
        {
            return Texts.NoConnectionValue;
        }

        return Snapshot.GateMode switch
        {
            GateMode.ForcedOpen => Texts.ForceOpenStateLabel,
            GateMode.TemporaryOpen => Texts.OpenStateLabel,
            GateMode.Locked => Texts.LockedStateLabel,
            _ => Texts.ClosedStateLabel
        };
    }

    protected string GetLastSyncValue()
    {
        return Snapshot.IsConnected
            ? $"{Snapshot.TelemetryIntervalMs} {Texts.MillisecondsUnit}"
            : Texts.NotAvailableValue;
    }

    protected string GetConnectedAtValue()
    {
        return Snapshot.ConnectedAt?.ToLocalTime().ToString("HH:mm:ss") ?? Texts.NotAvailableValue;
    }

    protected string GetRemainingValue()
    {
        return Snapshot.RemainingSeconds > 0
            ? $"{Snapshot.RemainingSeconds} {Texts.SecondsUnit}"
            : Texts.NotAvailableValue;
    }

    protected string GetThresholdValue()
    {
        return Snapshot.IsConnected
            ? $"{Snapshot.ThresholdCm} {Texts.CentimetersUnit}"
            : Texts.NotAvailableValue;
    }

    protected string GetTelemetryValue()
    {
        return Snapshot.IsConnected
            ? $"{Snapshot.TelemetryIntervalMs} {Texts.MillisecondsUnit}"
            : Texts.NotAvailableValue;
    }

    protected string GetOpenWindowValue()
    {
        return Snapshot.IsConnected
            ? $"{Snapshot.OpenDurationMs} {Texts.MillisecondsUnit}"
            : Texts.NotAvailableValue;
    }

    protected string GetSlotCapacityValue()
    {
        return Snapshot.IsConnected
            ? Snapshot.SlotCapacity.ToString()
            : Texts.NotAvailableValue;
    }

    private static DashboardSnapshot CreateEmptySnapshot()
    {
        return new DashboardSnapshot(
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            GateMode.Closed,
            0,
            false,
            false,
            0,
            0,
            0);
    }

    private IDashboardService RequireDashboardService()
    {
        return DashboardService ?? throw new InvalidOperationException("Dashboard service is not available.");
    }

    private IDeviceSessionService RequireDeviceSessionService()
    {
        return DeviceSessionService ?? throw new InvalidOperationException("Device session service is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        if (DateTimeOffset.UtcNow < _updatesAllowedAt)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await RefreshAsync();
            StateHasChanged();
        });
    }
}