# Skyrim AE Backup Tool

A lightweight Windows desktop app to **backup and restore Skyrim Special Edition / Anniversary Edition Creation Club content** with a single click.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![Language](https://img.shields.io/badge/C%23-WPF-green)
![License](https://img.shields.io/badge/license-MIT-lightgrey)

---

## Why?

Reinstalling Skyrim, switching mod profiles, or troubleshooting a broken setup often means losing or re-downloading your purchased Anniversary Edition / Creation Club content. This tool packages all CC/AE files into a single timestamped `.zip` you can restore anytime — no Bethesda re-download required.

## Features

- 🔍 **Auto-detects** Skyrim SE install path (Bethesda registry + Steam library scan)
- 📂 **Manual folder selection** if auto-detect fails
- 💾 **Configurable backup folder** — set once, reused for every backup
- 🗜 **One-click backup** — bundles all AE/CC files into a timestamped `.zip`
- ♻️ **One-click restore** — extracts the zip back into your `Data/` folder
- 🔒 **Safety filter** — restore only extracts files matching known AE/CC patterns; won't drop random files into Data
- 💼 **Settings persistence** — remembers your folders between sessions

## Screenshots

> *(Add screenshots here)*

---

## Requirements

- **Windows 10/11**
- **.NET 8 Desktop Runtime** ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
  - Not needed if you use a self-contained build (see below)

## Download

Grab the latest pre-built EXE from the [Releases](../../releases) page, or build from source.

---

## Build from Source

### Prerequisites
- Visual Studio 2022 with **.NET desktop development** workload, **or**
- .NET 8 SDK + your favorite editor

### Clone & build
```bash
git clone https://github.com/YOUR_USERNAME/SkyrimAEBackup.git
cd SkyrimAEBackup
dotnet build -c Release
```

### Run
```bash
dotnet run -c Release
```

### Publish single-file EXE
```bash
# Requires .NET runtime on target machine (smaller exe)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Self-contained — no .NET install needed on target
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Output: `bin/Release/net8.0-windows/win-x64/publish/SkyrimAEBackup.exe`

---

## Usage

1. Launch the app — it auto-detects your Skyrim folder
2. (Optional) Click **Browse** to set a custom backup folder (default: `Documents\SkyrimAEBackups`)
3. Click **Scan AE Content** to preview what will be backed up
4. Click **Backup AE Content** — done
5. To restore later, click **Restore from Backup...** and pick the zip

### What counts as "AE content"?

The tool identifies Creation Club / Anniversary Edition files in your `Data/` folder via filename pattern matching:

| Pattern | Example |
|---|---|
| `cc[DEV]SSE[NUM]*.esl/esm/esp/bsa` | `ccBGSSSE001-Fish.esl` |
| `_ResourcePack.*` | `_ResourcePack.esl`, `_ResourcePack.bsa` |

**Bethesda master files** (`Skyrim.esm`, `Update.esm`, `Dawnguard.esm`, `HearthFires.esm`, `Dragonborn.esm`) are implicit in SE and are **not** touched by this tool.

---

## How it works

| Component | Purpose |
|---|---|
| `SkyrimDetector.cs` | Auto-locates Skyrim via Bethesda registry key + Steam `libraryfolders.vdf` parsing |
| `AEContentDetector.cs` | Regex-based identification of CC/AE files |
| `BackupManager.cs` | `System.IO.Compression`-based zip create/extract |
| `AppSettings.cs` | JSON settings stored at `%APPDATA%\SkyrimAEBackup\settings.json` |
| `MainWindow.xaml(.cs)` | WPF UI with async backup/restore + progress log |

## Tech Stack

- **C# 12** / **.NET 8**
- **WPF** for the UI
- **System.IO.Compression** for zip operations
- **Microsoft.Win32.Registry** for Steam/Bethesda path detection

---

## FAQ

**Q: Will this back up my regular mods?**
No. Only files matching the AE/CC naming pattern are touched. Your Nexus mods, SKSE, ENB, etc. are ignored.

**Q: Is it safe to run with MO2 / Vortex?**
**Close MO2/Vortex/Skyrim before restoring** to avoid file locks. Backup is read-only and safe to run anytime.

**Q: Where are settings stored?**
`%APPDATA%\SkyrimAEBackup\settings.json` — delete this file to reset.

---

## Contributing

Issues and PRs welcome. Open an issue first for major changes.

## License

[MIT](LICENSE)

## Disclaimer

This tool is not affiliated with Bethesda Softworks. "The Elder Scrolls V: Skyrim Anniversary Edition", and "Creation Club" are trademarks of their respective owners. This software only manages files already on your machine — it does not download, redistribute, or circumvent any DRM.