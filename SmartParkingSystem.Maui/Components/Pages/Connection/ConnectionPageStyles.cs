using SmartParkingSystem.Domain.Models.Localization;

namespace SmartParkingSystem.Maui.Components.Pages.Connection;

public static class ConnectionPageStyles
{
    public static string PrimaryButtonClass =>
        "min-h-12 w-full rounded-md bg-brand-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-brand-400 disabled:cursor-default disabled:opacity-50";

    public static string SecondaryButtonClass =>
        "min-h-12 rounded-md bg-mint-300 px-4 py-3 text-sm font-semibold text-calm-900 transition-all duration-500 ease-out hover:bg-mint-200 disabled:cursor-default disabled:opacity-50";

    public static string WarningButtonClass =>
        "min-h-12 rounded-md bg-warm-300 px-4 py-3 text-sm font-semibold text-warm-700 transition-all duration-500 ease-out hover:bg-warm-200 disabled:cursor-default disabled:opacity-50";

    public static string GetLeftPanelClass(bool isLeavingPage)
    {
        return isLeavingPage
            ? "animate-exit-left rounded-md bg-brand-100/70 p-8 sm:p-10"
            : "animate-enter-left rounded-md bg-brand-100/70 p-8 opacity-0 sm:p-10";
    }

    public static string GetRightPanelClass(bool isLeavingPage)
    {
        return isLeavingPage
            ? "animate-exit-right flex h-full flex-col gap-4"
            : "animate-enter-right flex h-full flex-col gap-4 opacity-0";
    }

    public static string GetModeButtonClass(ConnectionMode mode, ConnectionMode activeMode)
    {
        var stateClass = activeMode == mode
            ? "bg-brand-200 hover:bg-brand-400"
            : "bg-white/85 hover:bg-calm-100";

        return $"flex min-h-16 flex-1 items-center gap-3 rounded-md px-4 py-3 transition-all duration-500 ease-out {
            stateClass}";
    }

    public static string GetLanguageButtonClass(AppLanguage language, AppLanguage selectedLanguage)
    {
        var stateClass = selectedLanguage == language
            ? "bg-brand-200 text-calm-900 hover:bg-brand-400"
            : "bg-white/85 text-calm-700 hover:bg-calm-100";

        return
            $"inline-flex min-h-12 items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition-all duration-500 ease-out {
                stateClass}";
    }

    public static string GetModeContentClass(bool isVisible)
    {
        return isVisible
            ? "opacity-100 transition-opacity duration-[250ms] ease-out"
            : "opacity-0 transition-opacity duration-[250ms] ease-in";
    }
}