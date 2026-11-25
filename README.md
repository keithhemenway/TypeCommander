# TypeCommander

A tiny, portable Windows tool that "types" the contents of a text file into whatever window has focus, using `SendKeys`. Handy for getting commands into remote consoles, VMs, KVM/IPMI/iLO/iDRAC viewers, or any session where clipboard paste doesn't work.

- Single ~28 KB `TypeCommander.exe`, no install, no dependencies.
- Runs on any Windows with .NET Framework 4.x (built into Windows 8, 10, 11 and Server 2012+).

## Download

Get the latest `TypeCommander-vX.Y.Z-win.zip` (or the bare exe) from the [Releases](https://github.com/keithhemenway/TypeCommander/releases) page. Copy the exe anywhere, e.g. a USB stick or admin share.

## If double-clicking does nothing

TypeCommander isn't code-signed yet, so Windows may block it silently:

- **Smart App Control** (on by default on clean Windows 11 installs) blocks unsigned apps it doesn't recognize, sometimes with only a brief notification. Check *Windows Security > App & browser control > Smart App Control*, and *Event Viewer > Applications and Services Logs > Microsoft > Windows > CodeIntegrity > Operational* (event 3077 or 3033).
- **Downloaded file blocked:** right-click the exe or zip > Properties > tick **Unblock**, or run `Unblock-File .\TypeCommander.exe` in PowerShell.
- **.NET errors** appear in *Event Viewer > Windows Logs > Application* (source ".NET Runtime" or "Application Error").

## Using the app

1. Run `TypeCommander.exe`.
2. Pick the text file with **Browse...** or drag it onto the window. Default is `C:\temp\keys.txt`.
3. Check the preview, set the **Delay**, and click **Start typing**.
4. Click into the target window before the countdown ends. To stop at any point, press **Esc** (works from any window), press **Pause**, move the mouse, or click **Cancel**.

Safety checks: TypeCommander won't type binary files (control characters would turn into Ctrl/Alt shortcuts), won't type into the desktop, taskbar, File Explorer or itself, stops if the target window loses focus or the mouse moves, and asks before typing files over 20,000 characters.

With "Minimize while waiting" on, the app gets out of the way and focus usually returns to the window you were in before.

You can also pass a file on the command line: `TypeCommander.exe C:\path\to\commands.txt`

## Repository layout

```
src/TypeCommander.cs      App source (single file, C# 5)
src/AssemblyInfo.cs       File properties (title, description, copyright)
src/app.ico               App icon
scripts/TypeCommander.ps1 Original PowerShell one-shot script
VERSION                   Current version number
CHANGELOG.md              Release notes
build.bat                 Windows build (uses built-in csc.exe)
build.sh                  Linux/macOS build (Mono)
.github/workflows/        CI build and GitHub Release on tag
```

Build output goes to `build/` and `dist/`, which are not committed.

## Building

On Windows (no Visual Studio needed): `build.bat`

On Linux/macOS with Mono (`mcs`) and `zip`: `sh build.sh`

Both produce `build\TypeCommander.exe` and `dist\TypeCommander-v<VERSION>-win.zip`.

## Releasing a new version

1. Update `VERSION` (e.g. `1.1.0`) and add a section to `CHANGELOG.md`.
2. Commit, then tag and push:
   ```
   git tag v1.1.0
   git push origin main --tags
   ```
3. GitHub Actions builds on Windows and publishes a GitHub Release with the zip and exe attached. The build fails if the tag doesn't match `VERSION`.

## PowerShell version

`scripts/TypeCommander.ps1` is the original script: it waits 3 seconds and types `C:\temp\keys.txt`.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\TypeCommander.ps1
```

## How typing works

SendKeys special characters (`+ ^ % ~ ( ) { } [ ]`) are escaped so they type literally, and line breaks become `{ENTER}`. Keep focus on the target window until typing finishes.
