# Route LED And Smart Routing Validation

This document validates the route LED and smart parking changes against the local project docs.

## Docs Checked

- `docs/ARCHITECTURE_GUIDELINES.md`
- `docs/DEVICE_INTEGRATION_ARCHITECTURE.md`
- `docs/FUNCTIONAL_STRUCTURE.md`
- `docs/CSS_GUIDELINES.md`
- `arduino/HARDWARE_SETUP_ArduinoMega.md`
- `arduino/HARDWARE_SETUP_ArduinoUno.md`

## Scope

Validated changes cover Arduino route LED protocol support, MAUI device commands, parking services, smart RFID-based recommendation, protocol parsing, card profile persistence, workspace route controls, Telegram route controls, and Arduino hardware docs.

## Architecture Validation

Status: OK.

- Device communication stays behind command/session/protocol services.
- `DeviceCommandService` owns raw Arduino route command text.
- `ParkingService` exposes feature-level route operations and 30-second route auto-clear.
- `SmartParkingRouteService` owns RFID history tracking and recommendation.
- `WorkspaceParkingView` calls `IParkingService`, not transport or raw serial APIs.
- Telegram callbacks become backend commands before MAUI executes device actions.
- Page markup and behavior remain split between `.razor` and `.razor.cs`.

## Protocol Validation

Status: OK.

Arduino supports the same route protocol in all three controller files:

- `arduino/SmartParkingSystemController.ino`
- `arduino/SmartParkingSystemControllerDemo.ino`
- `arduino/SmartParkingSystemControllerWokwi.ino`

Command mapping:

| Command | Success response | Meaning |
| --- | --- | --- |
| `PARKING ROUTE 1` | `OK|PARKING|ROUTE_ENABLED` | Light route strip for P1 |
| `PARKING ROUTE 2` | `OK|PARKING|ROUTE_ENABLED` | Light route strip for P2 |
| `PARKING ROUTE 3` | `OK|PARKING|ROUTE_ENABLED` | Light route strip for P3 |
| `PARKING ROUTE_CLEAR` | `OK|PARKING|ROUTE_CLEARED` | Clear all route strips |

Invalid route slots return `ERR|PARKING|ROUTE_SLOT_HAS_NO_STRIP`.

Arduino `CONFIG` now exposes `route_slot`, which is parsed into `DeviceControllerConfiguration.ActiveRouteSlot`.

Arduino `SNAPSHOT` now exposes:

- `last_access_uid`
- `last_access_result`
- `last_access_counter`

These fields are parsed into `DeviceControllerSnapshot` and used by the smart route service.

## Hardware Documentation Validation

Status: OK.

`arduino/HARDWARE_SETUP_ArduinoMega.md` is the primary hardware guide and documents this physical correlation:

| Parking slot | Physical meaning | Occupancy sensor | LED strip pin |
| --- | --- | --- | --- |
| P1 | Nearest to entrance | HC-SR04 #1, D7/D8 | D22 |
| P2 | Middle route slot | HC-SR04 #2, D4/D6 | D23 |
| P3 | Farthest route slot | HC-SR04 #3, A0/A1 | D24 |

`arduino/HARDWARE_SETUP_ArduinoUno.md` is marked obsolete because Uno/Nano do not have enough comfortable pin and memory headroom for the full stand.

## Smart Routing Validation

Status: OK, with demo-stand assumptions.

Current flow:

1. Arduino reports an allowed RFID event.
2. MAUI stores a pending access for that card UID.
3. The next P1-P3 slot that becomes occupied within 30 seconds is assigned to that card.
4. When that slot becomes free, MAUI calculates visit duration.
5. The card profile is persisted in `smart-parking-profiles.json`.
6. On the next allowed access, MAUI recommends a free P1-P3 slot.

Physical distance order is currently hardcoded for the stand:

- P1 nearest
- P2 middle
- P3 farthest

Stay classification does not use hardcoded minute thresholds. It sorts known card average durations:

- lower third -> nearest free slot
- middle third -> middle free slot when possible
- upper third -> farthest free slot

If the preferred slot is occupied, the algorithm chooses the free slot closest to the preferred physical position.

## Route Auto-Clear Validation

Status: OK.

Every `ParkingService.ShowRouteToSlotAsync` call schedules automatic route clearing after 30 seconds. This covers MAUI manual controls, Telegram route controls, and RFID-based smart routing.

Manual `ClearRouteAsync` cancels pending auto-clear generation.

## UI And CSS Validation

Status: Mostly OK.

- No new visible borders or shadows were introduced.
- Route buttons stay inside the existing workspace parking view.
- The page-part split remains intact.

Nuance: visually verify the Lucide `route-off` icon. If it does not render, replace it with `x`, `power`, or `circle-off`.

## Telegram Validation

Status: OK for manual route control, partial for smart route.

Telegram supports route buttons, clear route, active route labels, and a smart route callback.

Nuance: Telegram `Smart маршрут` currently lacks RFID UID context, so it does not use per-card history. Full card-history smart routing runs automatically after MAUI receives an allowed RFID event.

## Persistence Validation

Status: OK.

Card route statistics are stored through `IAppMemoryStore` / `AppMemoryStore` in `smart-parking-profiles.json`.

Stored profile fields:

- card UID
- visit count
- average parking duration in minutes
- last known slot ID

## Arduino Memory Validation

Status: Improved.

Route response strings were moved to flash with `F(...)` where possible. User compile feedback showed SRAM usage improved from 2250 bytes to 1410 bytes.

Nuances:

- Arduino Mega 2560 remains the intended board.
- Uno/Nano may compile after optimization, but runtime headroom is still limited.
- Some conditional card-list responses still use RAM strings because the result depends on runtime branch selection.

## Verification

Passed:

```text
dotnet build SmartParkingSystem.Maui/SmartParkingSystem.Maui.csproj -f net10.0-android
```

Passed:

```text
git diff --check
```

Known non-code blocker: full solution build can fail on Windows if `SmartParkingSystem.Maui.exe` is already running and locks the output exe.

## Open Nuances

1. Smart visit assignment assumes the next newly occupied P1-P3 slot within 30 seconds belongs to the just-allowed RFID card.
2. Overlapping cars can confuse the RFID-to-slot association.
3. The P1/P2/P3 physical distance order is hardcoded and must be updated if the printed layout changes.
4. If MAUI disconnects before route auto-clear fires, Arduino can keep the last LED route until another command clears or changes it.
5. Telegram smart route is not the same as RFID-history smart route because Telegram lacks card UID context.
6. Final Arduino firmware validation still needs Arduino IDE or Arduino CLI with `Adafruit NeoPixel` installed.
