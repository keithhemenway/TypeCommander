# TypeCommander

[![Latest release](https://img.shields.io/github/v/release/keithhemenway/TypeCommander?label=release)](https://github.com/keithhemenway/TypeCommander/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/keithhemenway/TypeCommander/release.yml?branch=main&label=build)](https://github.com/keithhemenway/TypeCommander/actions/workflows/release.yml)
[![Code signed](https://img.shields.io/badge/code%20signed-Azure%20Artifact%20Signing-0078D4)](docs/code-signing.md)
![Windows](https://img.shields.io/badge/platform-Windows-0078D4)

**Types text into any window, keystroke by keystroke.**

TypeCommander is a tiny, portable Windows tool that types text (from a file, the clipboard, or typed in) into whatever window has focus. It's useful anywhere paste doesn't work: remote consoles, VMs, KVM/IPMI/iLO/iDRAC viewers, locked-down sessions.

- **Portable:** a single ~28 KB `TypeCommander.exe`. No installer, no dependencies.
- **Runs everywhere:** needs only .NET Framework 4.x, which is built into Windows 8, 10, 11 and Server 2012+.
- **Handles slow consoles:** an adjustable key delay for viewers that drop fast keystrokes.
- **Signed:** release builds are code-signed, so Windows shows a verified publisher.
- **Safe by default:** refuses binary files, won't type into the desktop or Explorer, and stops on Esc, Pause, mouse movement or loss of focus.

## Download

Get `TypeCommander-vX.Y.Z-win.zip` or the bare `TypeCommander.exe` from the **[latest release](https://github.com/keithhemenway/TypeCommander/releases/latest)**, then copy the exe anywhere, such as a USB stick or an admin share.

To confirm the download is genuine, right-click the exe > **Properties** > **Digital Signatures** and check that the signer is **Keith Hemenway**. You can also run this in PowerShell:

```powershell
Get-AuthenticodeSignature .\TypeCommander.exe   # Status should be Valid
```

## Using it

1. Run `TypeCommander.exe`.
2. Get the text into the box. Any of these works:
   - **Browse...** for a file (any extension), or drag a file onto the window
   - **Paste** from the clipboard
   - type or edit the text directly
3. Set **Start after** (seconds to wait) and, if needed, **Key delay**. Then click **Start typing**.
4. Click into the target window before the countdown ends.

**To stop at any time,** press **Esc** (works from any window), press **Pause**, move the mouse, or click **Cancel**.

**Characters going missing?** Slow consoles and KVM/iLO/iDRAC viewers can drop keystrokes. Set **Key delay** to 20-50 ms and try again.

With **Minimize while waiting** on, the app gets out of the way and focus usually returns to the window you were in.

You can also pass a file on the command line:

```
TypeCommander.exe C:\path\to\commands.txt
```

TypeCommander remembers the last file, delays, and window size in `%APPDATA%\TypeCommander\settings.ini`. Delete that file to reset.

### Safety checks

TypeCommander:

- won't type binary files, because control characters would turn into Ctrl/Alt shortcuts
- won't type into the desktop, taskbar, File Explorer or itself
- stops if the target window loses focus or the mouse moves
- asks before typing files over 20,000 characters

### How typing works

SendKeys special characters (`+ ^ % ~ ( ) { } [ ]`) are escaped so they type literally, and line breaks become `{ENTER}`. Keep focus on the target window until typing finishes.

## Troubleshooting

**Double-clicking does nothing.** Windows may be blocking the exe. Release builds from v1.1.1 on are signed. Older releases and your own local builds aren't.

- **Smart App Control** (on by default on clean Windows 11 installs) silently blocks unsigned apps it doesn't recognize. Check *Windows Security > App & browser control > Smart App Control*, and *Event Viewer > Applications and Services Logs > Microsoft > Windows > CodeIntegrity > Operational* (events 3077 or 3033).
- **Downloaded file blocked:** right-click the exe or zip > **Properties** > tick **Unblock**, or run `Unblock-File .\TypeCommander.exe` in PowerShell.
- **.NET errors** appear in *Event Viewer > Windows Logs > Application* (source ".NET Runtime" or "Application Error").

**SmartScreen still warns about a signed release.** A newly signed file can still get a warning until it builds download reputation. Check that the signer is Keith Hemenway, then choose **More info > Run anyway**.

## Building from source

| Platform | Command | Requires |
|---|---|---|
| Windows | `build.bat` | Nothing extra (uses the built-in `csc.exe`) |
| Linux / macOS | `sh build.sh` | Mono (`mcs`) and `zip` |

Both produce `build\TypeCommander.exe` and `dist\TypeCommander-v<VERSION>-win.zip`. Local builds are unsigned.

```
src/TypeCommander.cs        App source (single file, C# 5)
src/AssemblyInfo.cs         File properties (title, description, copyright)
src/app.ico                 App icon
scripts/TypeCommander.ps1   PowerShell version (no GUI)
docs/code-signing.md        How release signing is set up
VERSION                     Current version number
CHANGELOG.md                Release notes
build.bat / build.sh        Build scripts
.github/workflows/          CI: build on every push, sign and release on tags
```

## Releasing

1. Bump `VERSION` and add a matching section to `CHANGELOG.md`.
2. Commit, then tag and push:
   ```
   git tag v1.2.3
   git push origin main v1.2.3
   ```
3. GitHub Actions builds the exe, signs it, and publishes a release, using the `CHANGELOG.md` section as the release notes. The run fails if the tag doesn't match `VERSION` or the signature doesn't verify.

Signing setup and troubleshooting: [docs/code-signing.md](docs/code-signing.md).

## PowerShell version

`scripts/TypeCommander.ps1` is a no-GUI version for when you can't run the exe. It waits a few seconds, then types the file:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\TypeCommander.ps1 -Path .\commands.txt -Delay 5
```
