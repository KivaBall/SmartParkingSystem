using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Models.Localization;

namespace SmartParkingSystem.Components.Pages.Connection.Parts;

public class ConnectionLanguageSwitchBase : ComponentBase
{
    [Parameter]
    public string EnglishButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string UkrainianButtonClass { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<AppLanguage> OnLanguageSelected { get; set; }
}