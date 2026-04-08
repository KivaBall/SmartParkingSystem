# Architecture Guidelines

## Page Structure

- Page components should not keep markup and implementation logic in one `.razor` file once the page has real behavior.
- Each page should be split into:
- `PageName.razor` for markup
- `PageName.razor.cs` for component logic

## Required Pattern

For every real page, use this structure:

- `Components/Pages/PageName/PageNamePage.razor`
- `Components/Pages/PageName/PageNamePage.razor.cs`
- `Components/Pages/PageName/Parts/...`
- `Components/Pages/PageName/Parts/PartName.razor`
- `Components/Pages/PageName/Parts/PartName.razor.cs`

Example:

- `Components/Pages/Connection/ConnectionPage.razor`
- `Components/Pages/Connection/ConnectionPage.razor.cs`
- `Components/Pages/Connection/Parts/ConnectionHero.razor`
- `Components/Pages/Connection/Parts/ConnectionHero.razor.cs`
- `Components/Pages/Connection/ConnectionPageState.cs`
- `Components/Pages/Connection/ConnectionPageCoordinator.cs`

## Purpose

This rule exists to keep page files maintainable as the project grows.

Benefits:

- cleaner markup
- cleaner component logic
- easier navigation in the codebase
- easier future refactoring into smaller components
- less risk of creating large "god files"
- a page and its internal parts stay grouped in one place instead of polluting the shared `Pages` folder

## Page Module Structure

- Page-specific support types should stay near the page module instead of being promoted too early into global folders.
- If a page needs its own UI state, coordinator, local enums, or internal helpers, keep them inside that page folder.
- Page-specific state and coordination logic should not be moved into global application services unless multiple independent pages genuinely share the same behavior.
- Page-specific style builders, UI timing constants, and similar presentation-only helpers should also stay inside the page module instead of going into global services.
- Operator areas with a persistent header and internal sections should prefer a single workspace shell route with internal view switching instead of multiple route pages that remount the same UI frame.
- In that shell approach, the static frame stays in the shell page, while the inner sections are implemented as page-local views under `Parts/`.

Examples:

- `Components/Pages/Connection/ConnectionPageState.cs`
- `Components/Pages/Connection/ConnectionPageCoordinator.cs`
- `Components/Pages/Connection/ConnectionMode.cs`
- `Components/Pages/Connection/ConnectionPageStyles.cs`
- `Components/Pages/Connection/ConnectionPageTimings.cs`
- `Components/Pages/Workspace/WorkspacePage.razor`
- `Components/Pages/Workspace/WorkspacePage.razor.cs`
- `Components/Pages/Workspace/Parts/WorkspaceDashboardView.razor`
- `Components/Pages/Workspace/Parts/WorkspaceSettingsView.razor`

## Service Architecture

- The project should follow a service-oriented internal architecture.
- Business behavior, integration logic, localization logic, and device communication should live in dedicated services instead of page components.
- Services should be grouped in focused folders under `Services/` by responsibility.
- If one service area grows beyond a simple interface-plus-class pair, it should be split into focused subfolders inside that service area rather than kept as one flat directory.
- Domain models and shared data records should live under `Models/`, not inside service folders.
- Each service area should prefer:
- an interface file
- one or more implementation files
- no embedded domain model files unless there is a very strong reason

When a service area becomes broader, prefer this style:

- `Services/DeviceConnection/Connection/...`
- `Services/DeviceConnection/Session/...`
- `Services/DeviceConnection/Telemetry/...`
- `Services/DeviceConnection/Commands/...`
- `Services/DeviceConnection/Transport/...`
- `Services/Settings/Environment/...`
- `Services/Settings/Preferences/...`

Examples:

- `Services/DeviceConnection/Connection/IDeviceConnectionService.cs`
- `Services/DeviceConnection/Connection/DeviceConnectionService.cs`
- `Services/DeviceConnection/Session/IDeviceSessionService.cs`
- `Services/DeviceConnection/Session/DeviceSessionService.cs`
- `Services/DeviceConnection/Telemetry/IDeviceTelemetryService.cs`
- `Services/DeviceConnection/Telemetry/DeviceTelemetryService.cs`
- `Services/DeviceConnection/Commands/IDeviceCommandService.cs`
- `Services/DeviceConnection/Commands/DeviceCommandService.cs`
- `Services/DeviceConnection/Transport/IDeviceTransportService.cs`
- `Services/DeviceConnection/Transport/SerialDeviceTransportService.cs`
- `Services/Localization/ILocalizationService.cs`
- `Services/Localization/LocalizationService.cs`
- `Services/Settings/Environment/ISettingsService.cs`
- `Services/Settings/Environment/SettingsService.cs`
- `Services/Settings/Preferences/ISettingsPreferencesService.cs`
- `Services/Settings/Preferences/SettingsPreferencesService.cs`
- `Models/DeviceConnection/ConnectionTarget.cs`
- `Models/DeviceConnection/ConnectionResult.cs`
- `Models/Localization/AppLanguage.cs`
- `Models/Localization/ConnectionTexts.cs`

Pages should consume service contracts through dependency injection instead of depending directly on concrete implementations.

This means:

- a simple service area may stay as `Interface + Implementation`
- a broader service area such as `DeviceConnection` should be split by responsibility instead of keeping many unrelated files in one folder
- a broader service area such as `Settings` may split into smaller concerns like environment data and user preferences

## Workspace Preferences

- Cross-section workspace preferences should live in a dedicated preference service instead of being duplicated in individual views.
- If a setting from `Settings` changes how another workspace section behaves, that state should be shared through a service contract, not through page-local flags.
- This applies to UI-level operator preferences such as hiding disabled parking slots or enabling parking layout editing.

## Service Lifetimes

- Services that represent shared workspace state, controller session state, localization state, or operator preferences should normally be registered as `Singleton`.
- Services that depend on browser-side or JS-driven environment data should prefer `Scoped` unless there is a clear reason to widen their lifetime.
- Feature services such as `Gate`, `Admin`, and `Parking` may be `Singleton` when they act as stateless coordinators over other singleton device services.
- Avoid mixing long-lived singleton state with page-local transient flags inside components.

Examples:

- `ILocalizationService` -> `Singleton`
- `ISettingsPreferencesService` -> `Singleton`
- `IDeviceSessionService` -> `Singleton`
- `ISettingsService` -> `Scoped`

## Localization Growth

- A single localization service is acceptable while the application is still small.
- Once the localization surface becomes large, prefer splitting internal text construction by feature group instead of growing one monolithic dictionary forever.
- The app should still expose one localization contract to pages even if its internal implementation later becomes more modular.

## Device Integration Boundaries

- Bluetooth / COM transport, device validation, telemetry parsing, and command sending should be treated as separate responsibilities even if an early implementation temporarily combines some of them.
- A raw transport connection must not automatically be treated as a valid controller session.
- The app should confirm device identity through a handshake or validation protocol before entering the main workspace.
- Device-side periodic data should be modeled as telemetry rather than mixed ad hoc UI updates.
- Operator actions sent back to the controller should be modeled as commands rather than direct string writes scattered across pages.
- Workspace feature services such as `Gate`, `Admin`, and `Parking` should consume device session, telemetry, and command services rather than talking to the transport layer directly.
- Low-level transport should stay hidden behind protocol-aware services so page modules and feature services do not manually parse raw serial text.
- Protocol exchanges that share one physical channel should be serialized through a dedicated coordination service to avoid interleaving reads and writes from different workspace sections.

## Service Implementations

- Concrete service implementations should sit behind interfaces.
- Temporary or development-only implementations should use an explicit `Fake` prefix.
- Fake implementations should preserve the same contract shape expected from the future real implementation whenever reasonably possible.

Examples:

- `FakeDeviceConnectionService`

## Type Design

- Simple immutable data carriers should prefer `record`.
- Fixed sets of allowed values should prefer `enum`.
- Classes that are not intended for inheritance should default to `sealed`.
- Avoid introducing class hierarchies unless there is a real architectural need.
- Prefer clear, narrow types over overly flexible generic structures.

## Scope

- This rule applies to pages first.
- Internal page parts should follow the same split approach and use `.razor` plus `.razor.cs`.
- If a smaller reusable component also becomes stateful or large, the same split approach should be preferred there too.
- Flat page files are acceptable only for very small or temporary screens.
- As soon as a page gains real state, orchestration, or multiple UI sections, it should be promoted into its own page module folder.
- Cross-page reusable UI blocks should move into a shared component area instead of staying inside a single page module.
- Shared UI components should use shared model types instead of page-local nested types when they are consumed by more than one page.

## Startup And Platform Files

- Application bootstrap and platform entry files should stay thin.
- `App`, `MainPage`, `MauiProgram`, and platform startup files should only compose the app, register services, and host the UI shell.
- Business logic, page flow, and feature behavior should not be implemented in startup or platform files.

## Shared Browser Hooks

- Repeated browser-side initialization should not spread across many pages indefinitely.
- If the same JS interop hook is needed by multiple pages, prefer extracting a shared helper or service before the pattern grows further.
- Page files may still call a page-local or shared JS helper, but should not accumulate unrelated browser bootstrapping logic.
