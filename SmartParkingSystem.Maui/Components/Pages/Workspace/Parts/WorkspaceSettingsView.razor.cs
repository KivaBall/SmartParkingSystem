using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Settings;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Settings.Environment;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceSettingsViewBase : ComponentBase, IDisposable
{
    private bool _hasLoaded;

    [Parameter]
    public bool IsExiting { get; set; }

    protected string CurrentDeviceClass => IsExiting
        ? "animate-exit-left rounded-md bg-brand-100/80 p-6"
        : "animate-page-enter-left rounded-md bg-brand-100/80 p-6 opacity-0";

    protected string InterfaceLanguageClass => IsExiting
        ? "animate-exit-left rounded-md bg-white/85 p-6"
        : "animate-page-enter-left rounded-md bg-white/85 p-6 opacity-0";

    protected string EnvironmentClass => IsExiting
        ? "animate-exit-right rounded-md bg-mint-100 p-6"
        : "animate-page-enter-right rounded-md bg-mint-100 p-6 opacity-0";

    protected string BackendClass => IsExiting
        ? "animate-exit-right rounded-md bg-white/85 p-6"
        : "animate-page-enter-right rounded-md bg-white/85 p-6 opacity-0";

    protected string CurrentDeviceStyle => IsExiting ? "animation-delay: 240ms;" : "animation-delay: 0ms;";
    protected string InterfaceLanguageStyle => "animation-delay: 120ms;";
    protected string EnvironmentStyle => "animation-delay: 120ms;";
    protected string BackendStyle => IsExiting ? "animation-delay: 0ms;" : "animation-delay: 240ms;";

    [Inject]
    protected ILocalizationService? LocalizationService { get; set; }

    [Inject]
    protected ISettingsService? SettingsService { get; set; }

    [Inject]
    protected ISettingsPreferencesService? SettingsPreferencesService { get; set; }

    [Parameter]
    public EventCallback OnLanguageChanged { get; set; }

    protected IReadOnlyList<SettingsInfoItem> DeviceItems { get; set; } = [];
    protected IReadOnlyList<SettingsInfoItem> EnvironmentItems { get; set; } = [];
    protected string BackendBaseUrl { get; private set; } = string.Empty;
    protected AppLanguage CurrentLanguage => RequireLocalizationService().CurrentLanguage;
    protected SettingsTexts Texts => RequireLocalizationService().GetSettingsTexts();

    protected bool BackendSyncEnabled => RequireSettingsPreferencesService().BackendSyncEnabled;

    public void Dispose()
    {
        RequireSettingsPreferencesService().PreferencesChanged -= OnPreferencesChanged;
        GC.SuppressFinalize(this);
    }

    protected override Task OnInitializedAsync()
    {
        RequireSettingsPreferencesService().PreferencesChanged += OnPreferencesChanged;
        BackendBaseUrl = RequireSettingsPreferencesService().BackendBaseUrl;
        EnvironmentItems = RequireSettingsService().GetEnvironmentItems(Texts);
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasLoaded)
        {
            return;
        }

        await ReloadAsync();
        await InvokeAsync(StateHasChanged);
    }

    protected string GetLanguageButtonClass(AppLanguage language)
    {
        var stateClass = CurrentLanguage == language
            ? "bg-brand-200 text-calm-900 hover:bg-brand-400"
            : "bg-white/85 text-calm-700 hover:bg-calm-100";

        return
            $"inline-flex min-h-12 items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition-all duration-500 ease-out {
                stateClass}";
    }

    protected string BackendSyncButtonClass => BackendSyncEnabled
        ? "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-brand-200 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-brand-400"
        : "inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-white/85 px-4 py-3 text-sm font-semibold text-calm-700 transition-all duration-500 ease-out hover:bg-calm-100";

    protected async Task SetLanguageAsync(AppLanguage language)
    {
        RequireLocalizationService().CurrentLanguage = language;
        await ReloadAsync();
        await OnLanguageChanged.InvokeAsync();
    }

    protected Task ToggleBackendSyncAsync()
    {
        var preferencesService = RequireSettingsPreferencesService();
        preferencesService.BackendSyncEnabled = !preferencesService.BackendSyncEnabled;
        return InvokeAsync(StateHasChanged);
    }

    protected Task OnBackendBaseUrlChanged(ChangeEventArgs eventArgs)
    {
        BackendBaseUrl = eventArgs.Value?.ToString() ?? string.Empty;
        RequireSettingsPreferencesService().BackendBaseUrl = BackendBaseUrl;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ReloadAsync()
    {
        EnvironmentItems = RequireSettingsService().GetEnvironmentItems(Texts);
        DeviceItems = await RequireSettingsService().GetDeviceItemsAsync(Texts);
        _hasLoaded = true;
    }

    private ILocalizationService RequireLocalizationService()
    {
        return LocalizationService ?? throw new InvalidOperationException("Localization service is not available.");
    }

    private ISettingsService RequireSettingsService()
    {
        return SettingsService ?? throw new InvalidOperationException("Settings service is not available.");
    }

    private ISettingsPreferencesService RequireSettingsPreferencesService()
    {
        return SettingsPreferencesService ??
               throw new InvalidOperationException("Settings preferences service is not available.");
    }

    private void OnPreferencesChanged()
    {
        BackendBaseUrl = RequireSettingsPreferencesService().BackendBaseUrl;
        _ = InvokeAsync(StateHasChanged);
    }
}
