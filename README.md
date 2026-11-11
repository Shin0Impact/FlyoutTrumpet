# FlyoutTrumpet

A customised fork of [EarTrumpet](https://github.com/File-New-Project/EarTrumpet), the Windows per-app volume mixer (MIT licensed, see `LICENSE`).

## At a glance

| | |
|---|---|
| **What** | Personal fork of the open-source EarTrumpet volume mixer for Windows with extra features |
| **Stack** | C# and WPF/XAML, as in upstream |
| **Build** | GitHub Actions builds the app on every push; no local Visual Studio needed |
| **Upstream** | [File-New-Project/EarTrumpet](https://github.com/File-New-Project/EarTrumpet), MIT licensed |

## Changes
- **Device switcher** - a button in the volume flyout header lists all output devices; click one to make it the default.
- **Sharp tray icon** - drawn at the size of the highest-DPI monitor, so it stays crisp on mixed-DPI setups. Still changes with volume and mute.
- **Now-playing flyout** - Windows 10 style box in the top-left (art, title, artist, prev/play/next, timeline), built on the Windows media transport controls API.

## Build
The GitHub Action in `.github/workflows/build.yml` builds the app on every push. Download `EarTrumpet-custom.zip` from the run's artifacts, unzip it and run `EarTrumpet.exe`.
