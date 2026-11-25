# TypeCommander

A tiny, portable Windows tool that "types" the contents of a text file into whatever window has focus, using `SendKeys`. Handy for getting commands into remote consoles, VMs, KVM/IPMI/iLO/iDRAC viewers, or any session where clipboard paste doesn't work.

- Single ~27 KB `TypeCommander.exe`, no install, no dependencies.
- Runs on any Windows with .NET Framework 4.x (built into Windows 8, 10, 11 and Server 2012+).

## Using the app

1. Run `TypeCommander.exe` (copy it anywhere, e.g. a USB stick or admin share).
2. Pick the text file with **Browse...** or drag it onto the window. Default is `C:\temp\keys.txt`.
3. Check the preview, set the **Delay**, and click **Start typing**.
4. Click into the target window before the countdown ends. Press **Esc** or **Cancel** to abort.

With "Minimize while waiting" on, the app gets out of the way and focus usually returns to the window you were in before.

You can also pass a file on the command line: `TypeCommander.exe C:\path\to\commands.txt`

## Building

No Visual Studio needed. On any Windows machine, run:

```
build.bat
```

This uses the `csc.exe` that ships with .NET Framework (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\`). Source is a single file, `src\TypeCommander.cs`, plus the icon `src\app.ico`.

On Linux/macOS with Mono: `mcs -target:winexe -optimize+ -win32icon:src/app.ico -out:TypeCommander.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll src/TypeCommander.cs`

## PowerShell version

`TypeCommander.ps1` is the original one-shot script: it waits 3 seconds and types `C:\temp\keys.txt`.

```powershell
powershell -ExecutionPolicy Bypass -File .\TypeCommander.ps1
```

## How typing works

SendKeys special characters (`+ ^ % ~ ( ) { } [ ]`) are escaped so they type literally, and line breaks become `{ENTER}`. Keep focus on the target window until typing finishes.
