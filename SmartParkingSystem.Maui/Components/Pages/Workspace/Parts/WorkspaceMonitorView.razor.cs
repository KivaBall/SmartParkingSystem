using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.DeviceConnection;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Monitor;
using SmartParkingSystem.Maui.Services.DeviceConnection.Session;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Monitor;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceMonitorViewBase : ComponentBase, IDisposable
{
    [Inject]
    protected IMonitorService? MonitorService { get; set; }

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected IDeviceSessionService? DeviceSessionService { get; set; }

    [Parameter]
    public bool IsExiting { get; set; }

    protected MonitorSnapshot Snapshot { get; set; } = new MonitorSnapshot(
        string.Empty,
        false,
        new MonitorEditableSettings());

    protected MonitorEditableSettings EditableSettings { get; set; } = new MonitorEditableSettings();
    protected bool IsLoading { get; private set; } = true;
    protected bool IsBusy { get; private set; }
    private bool IsReloading { get; set; }
    protected string StatusMessage { get; private set; } = string.Empty;
    protected MonitorTexts Texts => RequireLocalizationService().GetMonitorTexts();
    protected bool HasChanges => IsDirty();
    protected string StatusText => string.IsNullOrWhiteSpace(StatusMessage) ? Texts.ValidationHint : StatusMessage;

    protected string DisplayText =>
        !string.IsNullOrWhiteSpace(Snapshot.CurrentText)
            ? Snapshot.CurrentText
            : !string.IsNullOrWhiteSpace(Snapshot.EditableSettings.DefaultText)
                ? Snapshot.EditableSettings.DefaultText
                : "N/A";

    protected string DisplayClass => IsExiting
        ? "animate-exit-left rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-left rounded-md bg-brand-100/80 p-6 opacity-0";

    protected string ControlsClass => IsExiting
        ? "animate-exit-right rounded-md bg-warm-100 p-6"
        : "animate-page-enter-right rounded-md bg-warm-100 p-6 opacity-0";

    protected static string DisplayStyle => "animation-delay: 0ms;";
    protected static string ControlsStyle => "animation-delay: 120ms;";

    public void Dispose()
    {
        RequireDeviceSessionService().SessionChanged -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        RequireDeviceSessionService().SessionChanged += OnSessionChanged;
        await ReloadAsync();
    }

    protected async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.RefreshingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireMonitorService().RefreshAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            IsLoading = false;
            StatusMessage = Texts.RefreshSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task SaveAsync()
    {
        if (IsBusy || !HasChanges)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.SavingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireMonitorService().SaveAsync(EditableSettings);
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.SaveSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task ResetAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = Texts.ResettingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            Snapshot = await RequireMonitorService().ResetAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            StatusMessage = Texts.ResetSuccessMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected Task ToggleForceAsync()
    {
        EditableSettings.ForceEnabled = !EditableSettings.ForceEnabled;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ReloadAsync()
    {
        if (IsReloading)
        {
            return;
        }

        IsReloading = true;
        try
        {
            Snapshot = await RequireMonitorService().GetSnapshotAsync();
            EditableSettings = Snapshot.EditableSettings.Clone();
            IsLoading = false;
        }
        finally
        {
            IsReloading = false;
        }
    }

    private bool IsDirty()
    {
        var original = Snapshot.EditableSettings;
        return EditableSettings.ForceEnabled != original.ForceEnabled
               || EditableSettings.ForcedText != original.ForcedText
               || EditableSettings.DefaultText != original.DefaultText
               || EditableSettings.AllowedText != original.AllowedText
               || EditableSettings.BlockedText != original.BlockedText
               || EditableSettings.InvalidText != original.InvalidText
               || EditableSettings.LockedText != original.LockedText;
    }

    private IMonitorService RequireMonitorService()
    {
        return MonitorService ?? throw new InvalidOperationException("Monitor service is not available.");
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private IDeviceSessionService RequireDeviceSessionService()
    {
        return DeviceSessionService ?? throw new InvalidOperationException("Device session service is not available.");
    }

    private void OnSessionChanged(DeviceControllerSession? session)
    {
        if (IsBusy || HasChanges || IsReloading)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await ReloadAsync();
            StateHasChanged();
        });
    }
}