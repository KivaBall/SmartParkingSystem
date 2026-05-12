using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Camera;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Settings;
using SmartParkingSystem.Maui.Services.CameraAi;
using SmartParkingSystem.Maui.Services.Localization;
using SmartParkingSystem.Maui.Services.Settings.Environment;
using SmartParkingSystem.Maui.Services.Settings.Preferences;

namespace SmartParkingSystem.Maui.Components.Pages.Workspace.Parts;

public class WorkspaceSettingsViewBase : ComponentBase, IDisposable
{
    private const string AiConnectionTestImageDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private bool _hasLoaded;
    private bool _isTestingBackend;
    private bool _isTestingAi;
    private string _backendTestStatus = string.Empty;
    private string _openAiTestStatus = string.Empty;

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

    [Inject]
    protected IVehicleRecognitionAiService? VehicleRecognitionAiService { get; set; }

    [Inject]
    protected HttpClient? HttpClient { get; set; }

    [Parameter]
    public EventCallback OnLanguageChanged { get; set; }

    protected IReadOnlyList<SettingsInfoItem> DeviceItems { get; set; } = [];
    protected IReadOnlyList<SettingsInfoItem> EnvironmentItems { get; set; } = [];
    protected string BackendBaseUrl { get; private set; } = string.Empty;
    protected bool IsTestingBackend => _isTestingBackend;
    protected bool IsTestingAi => _isTestingAi;
    protected AppLanguage CurrentLanguage => RequireLocalizationService().CurrentLanguage;
    protected SettingsTexts Texts => RequireLocalizationService().GetSettingsTexts();

    protected bool BackendSyncEnabled => RequireSettingsPreferencesService().BackendSyncEnabled;
    protected bool OpenAiUsageEnabled => RequireSettingsPreferencesService().OpenAiUsageEnabled;
    protected string BackendTestStatus => _backendTestStatus;

    protected string OpenAiApiKey
    {
        get => RequireSettingsPreferencesService().CameraAiApiKey;
        set => RequireSettingsPreferencesService().CameraAiApiKey = value;
    }

    protected string OpenAiTestStatus => _openAiTestStatus;

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

    protected static string GetBackendToggleButtonClass(bool isActive)
    {
        return isActive
            ? "inline-flex min-h-12 items-center justify-center rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200"
            : "inline-flex min-h-12 items-center justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-calm-700 transition-all duration-500 ease-out hover:bg-calm-50";
    }

    protected static string GetIconTestButtonClass(bool isActive)
    {
        return isActive
            ? "inline-flex min-h-12 w-12 shrink-0 animate-pulse items-center justify-center rounded-md bg-warm-300 text-warm-700 transition-all duration-500 ease-out disabled:cursor-default disabled:opacity-70"
            : "inline-flex min-h-12 w-12 shrink-0 items-center justify-center rounded-md bg-brand-300 text-calm-900 transition-all duration-500 ease-out hover:bg-brand-400 disabled:cursor-default disabled:opacity-70";
    }

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

    protected Task ToggleOpenAiUsageAsync()
    {
        var preferencesService = RequireSettingsPreferencesService();
        preferencesService.OpenAiUsageEnabled = !preferencesService.OpenAiUsageEnabled;
        return InvokeAsync(StateHasChanged);
    }

    protected Task OnBackendBaseUrlChanged(ChangeEventArgs eventArgs)
    {
        BackendBaseUrl = eventArgs.Value?.ToString() ?? string.Empty;
        RequireSettingsPreferencesService().BackendBaseUrl = BackendBaseUrl;
        return InvokeAsync(StateHasChanged);
    }

    protected async Task TestBackendAsync()
    {
        if (_isTestingBackend)
        {
            return;
        }

        _isTestingBackend = true;
        _backendTestStatus = Texts.BackendTestingMessage;
        RequireSettingsPreferencesService().BackendBaseUrl = BackendBaseUrl;
        await InvokeAsync(StateHasChanged);
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await RequireHttpClient().GetAsync(
                CombineBackendUrl(BackendBaseUrl, "health"),
                cancellationTokenSource.Token);
            _backendTestStatus = response.IsSuccessStatusCode
                ? Texts.BackendTestSuccessMessage
                : $"{Texts.BackendTestFailedMessage} HTTP {(int)response.StatusCode}.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _backendTestStatus = $"{Texts.BackendTestFailedMessage} {exception.Message}";
        }
        finally
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            _isTestingBackend = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task TestOpenAiAsync()
    {
        if (_isTestingAi)
        {
            return;
        }

        _isTestingAi = true;
        _openAiTestStatus = Texts.OpenAiTestingMessage;
        await InvokeAsync(StateHasChanged);
        try
        {
            await RequireVehicleRecognitionAiService().RecognizeAsync(
                AiConnectionTestImageDataUrl,
                [],
                VehicleAiRecognitionMode.MatchCameraVehicleToKnownProfiles);
            _openAiTestStatus = RequireSettingsPreferencesService().CameraAiLastStatus;
        }
        catch (Exception exception)
        {
            RequireSettingsPreferencesService().CameraAiLastStatus = exception.Message;
            _openAiTestStatus = exception.Message;
        }
        finally
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            _isTestingAi = false;
            await InvokeAsync(StateHasChanged);
        }
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

    private IVehicleRecognitionAiService RequireVehicleRecognitionAiService()
    {
        return VehicleRecognitionAiService ??
               throw new InvalidOperationException("Vehicle recognition AI service is not available.");
    }

    private HttpClient RequireHttpClient()
    {
        return HttpClient ?? throw new InvalidOperationException("HTTP client is not available.");
    }

    private static string CombineBackendUrl(string baseUrl, string path)
    {
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://127.0.0.1:5112"
            : baseUrl.Trim().TrimEnd('/');
        return $"{normalizedBaseUrl}/{path.TrimStart('/')}";
    }

    private void OnPreferencesChanged()
    {
        BackendBaseUrl = RequireSettingsPreferencesService().BackendBaseUrl;
        _ = InvokeAsync(StateHasChanged);
    }
}
