using Microsoft.AspNetCore.Components;
using SmartParkingSystem.Domain.Models.Localization;

namespace SmartParkingSystem.Maui.Components.Pages.Connection.Parts;

public class ConnectionLanguageSwitchBase : ComponentBase
{
    [Parameter]
    public string EnglishButtonClass { get; set; } = string.Empty;

    [Parameter]
    public string UkrainianButtonClass { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<AppLanguage> OnLanguageSelected { get; set; }
}