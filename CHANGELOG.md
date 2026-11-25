# Changelog

All notable changes to TypeCommander are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-11-25

### Added
- Progress bar while typing, with the percentage shown in the title bar and taskbar.

## [1.0.3] - 2026-11-25

### Fixed
- Stopping now works reliably. Typing runs on a background thread, so the window and Cancel button stay responsive.

### Added
- Esc is a global hotkey while counting down or typing: press it from any window to stop.
- Press Pause, or move the mouse more than a little, to stop typing.
- Status shows how much was typed when stopped.

## [1.0.2] - 2026-11-25

### Fixed
- Refuses to type binary or non-text files. Control characters were being sent as Ctrl/Alt key combinations, which could launch programs and open windows.
- Refuses to type into the desktop, taskbar, File Explorer, or TypeCommander itself.

### Added
- Typing stops if the target window loses focus, or if you hold Esc.
- Asks for confirmation before typing files over 20,000 characters.
- Shows which window the text was typed into.

### Changed
- "Minimize while waiting" is now off by default.

## [1.0.1] - 2026-11-25

### Fixed
- Startup errors now show a message box instead of exiting silently.
- Shortened the minimize option label so it fits the window.

### Changed
- README: notes on Smart App Control and SmartScreen blocking unsigned downloads.

## [1.0.0] - 2026-11-25

### Added
- Windows app (single portable exe, .NET Framework 4.x) that types a text file into the focused window.
- File picker, drag and drop, and command-line file argument.
- Text preview with character and line counts.
- Adjustable delay (0 to 300 seconds) with countdown, Esc to cancel.
- Optional minimize while waiting, so focus returns to the target window.
- App icon and version info in file properties.
- Original `TypeCommander.ps1` PowerShell script.
