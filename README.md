# Taskbar Match

Automatic taskbar color and contrast for Windows 11.

Taskbar Match samples the active window edge and adjusts each Windows taskbar to keep its background, text, and icons readable. It is designed for the stock Windows 11 x64 taskbar.

## Current beta

- Windows 11 x64, build 22621 or later
- No administrator access required
- No screenshots, telemetry, or network access
- Multi-monitor sampling
- Tray controls for pause, refresh, diagnostics, startup, and restore
- GPL-3.0-or-later

This beta reaches private Windows taskbar XAML controls through a small native helper. It may be incompatible with other taskbar stylers or with future Windows taskbar changes. Do not run it alongside TranslucentTB, ExplorerPatcher, or StartAllBack.

## Run the prebuilt package

1. Extract `TaskbarMatch-1.1.2-beta.zip`.
2. Double-click `TaskbarMatch.cmd`.
3. Use the tray icon to pause or exit and restore the taskbar.

The launcher copies the release layout into `%LOCALAPPDATA%\TaskbarMatch` and starts the prebuilt `TaskbarMatch.exe`. It does not compile code on the user's machine.

The beta binaries are not yet Authenticode-signed, so Windows may show an unfamiliar-app warning on first launch.

To remove it, exit Taskbar Match, disable `Start with Windows` in the tray menu, and delete `%LOCALAPPDATA%\TaskbarMatch` plus the extracted package folder. A helper DLL loaded by Explorer may remain locked until the next sign-out.

## Source and build

`TaskbarMatch-1.1.2.cmd` is the complete single-file source distribution. The readable source is also available under `src/`, with the native build recipe in `build_native.py`.

The release executable was built from the embedded C# and native sources. The native helper hash is recorded in the launcher and verified before loading.

## Scope

This is beta software. The supported target is the stock modern Windows 11 taskbar on x64 systems. It does not change global Windows accent colors, application themes, or Start. It restores its saved local taskbar values when stopped and monitors the controller process so Explorer can restore them after a crash.
