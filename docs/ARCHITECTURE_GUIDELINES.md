# Architecture Guidelines

## Page Structure

- Page components should not keep markup and implementation logic in one `.razor` file once the page has real behavior.
- Each page should be split into:
- a `.razor` file for markup
- a separate `.cs` code-behind file for component logic

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

Examples:

- `Components/Pages/Connection/ConnectionPageState.cs`
- `Components/Pages/Connection/ConnectionPageCoordinator.cs`
- `Components/Pages/Connection/ConnectionMode.cs`
- `Components/Pages/Connection/ConnectionPageStyles.cs`
- `Components/Pages/Connection/ConnectionPageTimings.cs`

## Service Architecture

- The project should follow a service-oriented internal architecture.
- Business behavior, integration logic, localization logic, and device communication should live in dedicated services instead of page components.
- Services should be grouped in focused folders under `Services/` by responsibility.
- Domain models and shared data records should live under `Models/`, not inside service folders.
- Each service area should prefer:
- an interface file
- one or more implementation files
- no embedded domain model files unless there is a very strong reason

Examples:

- `Services/DeviceConnection/IDeviceConnectionService.cs`
- `Services/DeviceConnection/FakeDeviceConnectionService.cs`
- `Services/Localization/ILocalizationService.cs`
- `Services/Localization/FakeLocalizationService.cs`
- `Models/DeviceConnection/ConnectionTarget.cs`
- `Models/DeviceConnection/ConnectionResult.cs`
- `Models/Localization/AppLanguage.cs`
- `Models/Localization/ConnectionTexts.cs`

Pages should consume service contracts through dependency injection instead of depending directly on concrete implementations.

## Service Implementations

- Concrete service implementations should sit behind interfaces.
- Temporary or development-only implementations should use an explicit `Fake` prefix.
- Fake implementations should preserve the same contract shape expected from the future real implementation whenever reasonably possible.

Examples:

- `FakeDeviceConnectionService`
- `FakeLocalizationService`

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

## Startup And Platform Files

- Application bootstrap and platform entry files should stay thin.
- `App`, `MainPage`, `MauiProgram`, and platform startup files should only compose the app, register services, and host the UI shell.
- Business logic, page flow, and feature behavior should not be implemented in startup or platform files.

## Shared Browser Hooks

- Repeated browser-side initialization should not spread across many pages indefinitely.
- If the same JS interop hook is needed by multiple pages, prefer extracting a shared helper or service before the pattern grows further.
- Page files may still call a page-local or shared JS helper, but should not accumulate unrelated browser bootstrapping logic.
