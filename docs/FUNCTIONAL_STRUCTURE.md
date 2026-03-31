# Functional Structure

## Entry Flow

The application should not open the full main interface immediately. First, it should try to establish connection with the parking device.

This creates a more logical startup flow:

1. App launch
2. Connection check / connection attempt
3. Success or failure state
4. Transition to main interface after successful connection

## Connection Screen

Before entering the main menu, the user should see a dedicated connection stage.

The purpose of this stage is:

- detect whether the Arduino-connected device is reachable
- allow the user to connect in a controlled way
- avoid opening the dashboard with no real device state

## Connection Options

The connection screen should support two main approaches:

- automatic connection attempt
- manual connection through selected port / device

### Automatic Mode

The application tries to find and connect to the expected device automatically.

This is the default and most user-friendly path.

Possible states:

- searching for device
- connecting
- connected successfully
- connection failed

### Manual Mode

If automatic connection fails, the user should be able to choose a device or port manually.

This mode is useful when:

- multiple ports are available
- auto-detection fails
- the operator wants explicit control

Possible actions:

- show available ports / Bluetooth targets
- select target manually
- try connection again

## Retry Flow

If connection is unsuccessful, the app should not fail silently.

Instead, it should offer:

- retry connection
- refresh available ports / devices
- switch between automatic and manual mode

This means the connection screen should behave like a lightweight connection manager before the user enters the main system.

## Main Navigation Concept

After a successful connection, the app should navigate to the main interface.

The top area should contain a floating header with:

- `rounded-md`
- icon-first square buttons
- minimal visual style
- hover tooltip or label reveal on desktop

The purpose of this header is to act as the primary navigation layer for the whole system.

## Navigation Behavior

The header buttons should primarily show icons.

On hover, the interface should reveal:

- the section name
- a short explanation of what that section does

This keeps the UI clean while still being understandable.

On mobile, where hover does not exist, labels should appear in a touch-friendly alternative form, for example:

- always-visible compact labels
- a bottom label area
- or an expandable navigation state

## Recommended Main Sections

These names should be clear, product-like, and suitable for both operator use and future scaling.

### Dashboard

The main overview screen.

Purpose:

- current parking summary
- current connection state
- quick system highlights
- important live indicators

### Parking

The parking occupancy screen.

Purpose:

- show parking places
- display whether spots are free or occupied
- provide a visual status map or card layout

### Gate Control

The access and gate management screen.

Purpose:

- manual gate activation
- display gate state
- access-related control actions

### Activity

The events and incoming system history screen.

Purpose:

- recent actions
- RFID-related events
- state updates received from the device

### Analytics

The analytical summary screen.

Purpose:

- trends
- usage summaries
- occupancy patterns
- system behavior over time

### Admin

The operator and advanced control screen.

Purpose:

- privileged actions
- manual overrides
- internal settings and service tools

## Recommended Primary Navigation Set

If the first version should stay compact, the most reasonable top-level menu set is:

- Dashboard
- Parking
- Gate Control
- Activity
- Admin

`Analytics` can either stay in the top navigation or be added later once enough data exists.

## General Functional Idea

The application should feel like a calm operational panel:

- first connect to the real device
- then enter the system
- then navigate between focused business sections through a clean floating header

This gives the project a much more serious and scalable structure than the previous Android app with only a few raw buttons on screen.

## Information Architecture

The first version of the application should follow a simple and clear screen structure.

Recommended order:

1. Connection Screen
2. Dashboard
3. Other business sections through header navigation

## Start Screen

The real start screen should be the connection screen, not the dashboard.

This screen should contain:

- app name / system name
- short connection status text
- automatic connection button
- manual connection option
- refresh available devices action
- visible success or failure message

Recommended actions:

- `Connect Automatically`
- `Choose Device`
- `Refresh`

Recommended connection-related icons:

- `plug`
- `bluetooth`
- `refresh-cw`
- `search`

## Dashboard Screen

After a successful connection, the user should land on `Dashboard`.

This page should act as the main overview of the whole parking system.

Recommended dashboard content:

- current connection state
- gate state
- total parking capacity
- occupied places count
- free places count
- last device update time
- quick warnings or system notices

Recommended quick action area:

- open gate manually
- refresh current data
- go to parking view
- go to admin tools

Recommended dashboard icons:

- `layout-dashboard`
- `activity`
- `bar-chart-3`
- `circle-parking`

## Parking Screen

`Parking` should focus specifically on parking place state.

Recommended content:

- list or card grid of parking places
- free / occupied indicator for each place
- last update timestamp
- optional visual grouping by zone if needed later

Possible future additions:

- place history
- place details
- occupancy duration per place

Recommended icon:

- `circle-parking`

## Gate Control Screen

`Gate Control` should contain direct interaction with the barrier and access control layer.

Recommended content:

- current gate state
- manual open action
- manual close action if supported later
- last access event
- recent RFID authorization result

Important note:

manual gate actions should be visually clear and intentionally separated from passive status information so that the operator understands these are real control commands.

Recommended icons:

- `gate`
- `shield-check`
- `key-round`

If a specific `gate` icon is unavailable in the chosen icon set, use the closest calm access-control alternative.

## Activity Screen

`Activity` should work as the operational event feed.

Recommended content:

- recent RFID scans
- gate open events
- blocked card events
- invalid card events
- telemetry updates from device
- timestamps for each event

This page should help the operator understand what recently happened in the system.

Recommended icons:

- `history`
- `logs`
- `scan-line`

## Analytics Screen

`Analytics` should summarize historical system behavior once enough data exists.

Recommended content:

- occupancy trends
- access frequency
- peak usage periods
- number of denied or blocked access attempts
- device communication patterns

This page is more important after telemetry and event persistence are implemented properly.

Recommended icons:

- `bar-chart-3`
- `chart-column`
- `trending-up`

## Admin Screen

`Admin` should contain privileged operator tools and service-oriented actions.

Recommended content:

- manual override actions
- service controls
- diagnostic actions
- device-level utilities
- internal settings

This section should be treated carefully because it is the place where potentially risky control actions live.

Recommended icons:

- `shield`
- `settings`
- `tool-case`

## Header Navigation Proposal

The floating top header should contain compact square icon buttons for the main sections.

Recommended order:

1. Dashboard
2. Parking
3. Gate Control
4. Activity
5. Analytics
6. Admin

If the first version should stay simpler, `Analytics` can temporarily be hidden from the primary navigation and added later.

## Tooltip / Label Proposal

On desktop hover, each navigation item should reveal:

- title
- short helper text

Example structure:

- `Dashboard` — system overview and quick actions
- `Parking` — current parking place occupancy
- `Gate Control` — barrier and access management
- `Activity` — recent events and RFID history
- `Analytics` — trends and summarized behavior
- `Admin` — advanced operator tools

## Mobile Navigation Note

Because hover is not available on mobile devices, the mobile version should adapt the same navigation logic in a simpler way.

Possible approaches:

- show icon with visible short text under it
- allow header buttons to expand
- use a compact secondary label area below the floating header

The key rule is that navigation should stay understandable on both desktop and mobile.
