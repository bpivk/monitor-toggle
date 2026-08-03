# Rig Toggle

![Build Status](https://github.com/bpivk/monitor-toggle/actions/workflows/build.yml/badge.svg)
![License](https://img.shields.io/github/license/bpivk/monitor-toggle)
![Latest Release](https://img.shields.io/github/v/release/bpivk/monitor-toggle)

A Windows GUI utility that switches between a normal desktop setup and a secondary
rig setup with one click — from the GUI, a tray menu, or a global keyboard shortcut.
Toggling to rig mode disables the primary monitor at the OS level, switches the
default audio output to the rig speakers, and launches/focuses a companion app.
Toggling back restores the exact previous monitor/audio state and minimizes the
companion app.

## Why this exists

Some games and apps misbehave when launched while Windows still treats their
target monitor as a secondary display — for example, minimizing themselves on
launch instead of opening normally. Rig Toggle fixes this by making the primary
monitor genuinely absent from Windows' active display list (not just powered
off) before switching to the secondary display, then reliably restores the
original monitor and audio configuration when you toggle back.

## Features

- **One-click toggle** — disables the primary monitor at the OS level, switches
  the default audio output device, and launches/focuses a companion app in one
  action; toggling back restores the exact previous monitor and audio state and
  minimizes the companion app
- **Tray residency** — runs minimized to the system tray with autostart on boot
  and a tray icon context menu; configurable close/minimize-to-tray behavior
- **Global hotkey** — trigger a toggle from anywhere with a single Windows-wide
  keyboard shortcut, no need to bring the GUI into focus
- **Multi-monitor sets** — configure arbitrary independent disable/enable sets
  across any number of monitors, not just a single hardcoded primary
- **Live theme-following** — the GUI matches the current Windows light/dark mode
  automatically, including the title bar, and updates live if you change the
  Windows theme while the app is running
- **Redesigned icons** — shape-distinct tray and app icons that stay legible on
  any taskbar/theme, no color-only differentiation

## Screenshots

| Normal mode | Rig mode |
|---|---|
| ![MainForm — normal mode](docs/screenshots/main-normal.png) | ![MainForm — rig mode](docs/screenshots/main-rig.png) |

| Settings | Tray menu |
|---|---|
| ![Settings](docs/screenshots/settings.png) | ![Tray menu](docs/screenshots/tray-menu.png) |

## Download

Grab the latest release from the [GitHub Releases page](https://github.com/bpivk/monitor-toggle/releases/latest).
The standalone `.exe` is attached to releases from v1.2 onward; earlier releases
(v1.0, v1.1) are notes-only.

## Build a standalone .exe

Publish is self-contained, single-file, and untrimmed (win-x64 only). From the repo root:

```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```

If the RID is ever not picked up for any reason (e.g. an older/mismatched SDK), fall back
to the explicit-flag form:

```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64
```

The output single-file exe lands in `src/RigToggle.App/bin/publish/win-x64/` and requires
no separate .NET runtime install to run (PACKAGING-01).

Note: the build is intentionally untrimmed (`PublishTrimmed=false`) — trimming can strip
the COM interop (audio default-device switching) and P/Invoke (display CCD topology)
marshalling this app depends on — and it targets Windows x64 only.

## System requirements

Windows 10 or 11, x64. Full visual polish (Mica backdrop, rounded window corners) is
Windows 11-only and gracefully degrades on Windows 10 — the app still runs and looks
correct, just without those two effects.
