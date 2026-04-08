# Device Integration Architecture

## Core Idea

The application should treat the Arduino controller as an external device that is connected through Bluetooth after the operator passes the connection stage.

The connection flow should work like this:

1. Bluetooth is enabled on the host device
2. the app opens the connection screen
3. the app either:
- scans automatically for the expected controller
- or lets the operator choose a concrete device / port manually
4. after transport connection is created, the app validates that the target is really the parking controller
5. only after successful validation does the app enter the main workspace

## Important Clarification

A successful Bluetooth or COM connection alone must not be treated as a successful parking-controller connection.

There should be a second validation step after transport connection:

- handshake keyword
- protocol token
- device signature
- or another agreed validation payload

This protects the app from treating an unrelated Bluetooth target as the real controller.

## Recommended Logical Layers

The integration should be split into clear layers instead of one large "Arduino service".

### 1. Transport Layer

Responsible only for low-level communication:

- Bluetooth connection
- COM port selection if needed
- opening and closing the underlying channel
- sending raw text or byte payloads
- receiving raw incoming payloads

This layer should not know business meaning like parking spots, gate state, or admin variables.

### 2. Handshake / Session Layer

Responsible for confirming that the connected target is the real parking controller.

Possible responsibilities:

- send validation request
- receive validation response
- confirm protocol version
- reject unknown or incompatible devices

This layer is what turns a raw transport connection into a valid controller session.

### 3. Telemetry Layer

Responsible for processing periodic data emitted by Arduino.

This data should be treated as a unified device snapshot or event stream that describes what is happening inside the controller.

Examples:

- parking place occupancy
- servo / gate state
- access results
- current thresholds
- device-side timers
- firmware-side configuration values

### 4. Command Layer

Responsible for sending operator actions back to the controller.

Examples:

- open gate manually
- request refresh
- change configurable variables
- trigger admin actions

This is the inverse direction of telemetry.

## Current Arduino Reality

The current reference firmware in `arduino/SmartParkingSystemOriginal.ino` already confirms part of this architecture:

- Bluetooth transport exists through `HC-05`
- the firmware already emits parking state data
- telemetry is currently sent as simple line-based messages like:
- `P1 O`
- `P2 F`
- `P3 O`
- the current emission interval in the reference code is `500 ms`

At the same time, the current firmware does **not** yet provide:

- a handshake protocol
- controller identity validation
- structured command parsing from the app
- unified multi-field telemetry snapshots

So the app architecture should be designed for these future capabilities even if the reference firmware is still simpler today.

## Recommended App-Side Service Direction

The future integration should not collapse into one giant service.

A healthier split is:

- `IDeviceConnectionService`
  transport discovery and connection lifecycle
- `IDeviceSessionService`
  handshake, validation, session state
- `IDeviceTelemetryService`
  parsing incoming device data into app models
- `IDeviceCommandService`
  sending operator commands to the controller
- `IDeviceProtocolExecutionService`
  serializing protocol exchanges that share the same physical connection

If the implementation starts simpler, one service may temporarily host more than one responsibility, but the architecture should still preserve these boundaries conceptually.

## Feature Service Direction

Workspace-level feature services should not open COM ports or send raw lines on their own.

Instead:

- `Gate` should consume current session state plus command services
- `Admin` should translate editable UI models into controller commands
- `Parking` should combine telemetry state with app-side layout information

This keeps device communication centralized and avoids duplicating protocol logic across multiple sections.

## Workspace Behavior After Connection

Once handshake succeeds, the app may treat the controller as active and allow the operator to enter the workspace shell.

After that:

- the app receives telemetry periodically
- the app updates dashboard and other sections from parsed device data
- the app sends manual commands back to the controller when required

This means the workspace is not the place where connection identity is decided. That must already be resolved before entering it.

## Protocol Direction

The most future-proof approach is to define a simple application protocol early.

At minimum, the protocol should eventually support:

- handshake message
- handshake success / failure response
- telemetry messages
- command messages
- optional error messages

Even if the first implementation still uses simple line-based strings, the architecture should already assume that communication is protocol-based rather than "just random text over Bluetooth".
