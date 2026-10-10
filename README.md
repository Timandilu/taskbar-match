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

## Run the prebuilt executable

1. Download `TaskbarMatch.exe` from the release page.
2. Double-click it.

The executable contains the native helper and extracts it into its per-user cache on first use. The ZIP is an optional bundle with the executable, source license, and checksums.
3. Use the tray icon to pause or exit and restore the taskbar.

To start at sign-in, enable **Start with Windows** in the tray menu. This copies the executable to `%LOCALAPPDATA%\TaskbarMatch\TaskbarMatch.exe` and registers that stable path. You can then delete the download. Autostart waits up to 60 seconds for the taskbar connection and ignores duplicate launches.

## Startup troubleshooting

This release supports Windows 11 22H2 or later on x64 hardware. Windows 10, Windows 11 21H2, ARM64, and replacement taskbars are not supported. It uses the .NET Framework included with supported Windows 11 installations; no .NET 3.5 or Visual C++ redistributable is required.

If launch fails, check `%LOCALAPPDATA%\TaskbarMatch\runtime.log` and the error dialog. Report the Windows build, CPU architecture, and complete error text. Run `TaskbarMatch.exe --selftest` from a terminal to check platform compatibility, sampling logic, embedded helper integrity, and native loading without modifying the taskbar. This does not test the Explorer XAML connection.

Beta 4 corrects the executable compatibility manifest, catches UI initialization errors, restores damaged helper caches, and fixes duplicate autostart shutdowns. Stop the previous version using its tray menu before opening the new executable.

The beta binaries are not yet Authenticode-signed, so Windows may show an unfamiliar-app warning on first launch.

To remove it, exit Taskbar Match, disable `Start with Windows` in the tray menu, and delete `%LOCALAPPDATA%\TaskbarMatch` plus the extracted package folder. A helper DLL loaded by Explorer may remain locked until the next sign-out.

## Source and build

`TaskbarMatch-1.1.2.cmd` is the complete single-file source distribution. The readable source is also available under `src/`, with the native build recipe in `build_native.py`.

The release executable was built from the embedded C# and native sources. The native helper hash is recorded in the launcher and verified before loading.

Build the native helper with `python build_native.py` (requires clang and lld-link), then build and package the Windows executable with `powershell -ExecutionPolicy Bypass -File build.ps1`. The Windows build synchronizes the launcher's embedded C# from `src/` and checks the helper hash before compiling.

## Scope

This is beta software. The supported target is the stock modern Windows 11 taskbar on x64 systems. It does not change global Windows accent colors, application themes, or Start. It restores its saved local taskbar values when stopped and monitors the controller process so Explorer can restore them after a crash.
