# Smart Parking System Context

## Overview

This repository contains a new cross-platform client application for the Smart Parking System.

The original hardware logic already existed on Arduino and an earlier Android application also existed, built with Java. That older mobile app was very minimal, visually poor, and effectively exposed only three buttons on screen. It did not provide a good user experience, did not scale well, and was not suitable as a modern interface for further development.

The current project exists because the system now needs multi-device support from a single codebase:

- Windows application
- Android application
- potentially iOS in the future

iOS is not a hard requirement yet. It is considered useful only if it can be supported without adding unnecessary distribution complexity. The primary target platforms are Windows and Android.

## Why This MAUI Project Exists

Even though the parking logic already exists in Arduino firmware, that firmware only controls the hardware layer:

- RFID card access
- barrier opening and closing
- LCD messages
- parking spot occupancy detection
- Bluetooth data transmission

This MAUI Blazor Hybrid project is the user-facing software layer above that hardware. Its job is to provide a much better interface, clearer system visibility, and room for future administrative and analytical features.

## Core Purpose Of The App

The app should become a proper control and monitoring panel for the parking system instead of a primitive button-only screen.

Main goals:

- show the current state of the parking system in a readable way
- support both Windows and Android from one project
- provide a more polished and user-friendly interface
- expose business functionality through separate screens
- allow future extension without rebuilding everything from scratch

## Planned Functional Direction

The intended UI direction is a styled application shell with a floating header and navigation between sections. Different sections should expose different business functions of the parking system.

Expected functional areas include:

- dashboard with overall parking state
- parking spot status view
- gate and access controls
- admin panel features
- analytics and activity review

## Admin Features

One important motivation for the new app is the need for administrative control. The app should not only display incoming data but also allow active management actions, for example:

- manually trigger the gate
- access internal control actions
- provide a more operator-friendly control surface

## Telemetry And Analytics

The Arduino side is expected to send system-related data over Bluetooth approximately every 10 seconds. That data should then be consumed by the app and used for:

- live state display
- event interpretation
- analytics
- better understanding of what is happening in the system overall

In short, the application is meant to become both:

- an operational interface
- an analytical and administrative layer over the existing Arduino parking controller

## Arduino Firmware Reference

The original Arduino firmware is stored alongside these docs as a starting-point reference:

- `SmartParkingSystemOriginal.ino`

That file reflects the starting hardware logic before later refinements.
