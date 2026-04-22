# HA DeskLink macOS v2.2.1

**Home Assistant Companion App for macOS**

⚠️ **COMMUNITY TEST VERSION – NOT TESTED BY THE DEVELOPER!** ⚠️

This version was created without Mac hardware and relies on **community testing**.
Please report bugs under [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).

---

## ⚠️ IMPORTANT NOTICE

**This macOS version has NOT been tested by the developer (Fabian Kirchweger)!**
There is no Mac hardware available for testing. The app was created based on the working Windows version.
If you use macOS, please test this version and report bugs – **the community is the tester!**

---

## Features

- 🖥️ CPU temperature, CPU usage, RAM, battery, disk
- 🖥️ Fullscreen sensor (which app is in fullscreen)
- 📺 Monitor layout sensor
- ☀️ Brightness control & display
- 🌍 Multi-language (de, en, es, fr, zh, ja)
- 📊 Dashboard opens Home Assistant in browser
- 🔒 HA token encrypted (macOS Keychain + AES-GCM)
- 🔔 Push notifications via WebSocket

## Commands

`shutdown`, `restart`, `sleep`, `lock`, `mute`, `volume_up`, `volume_down`, `monitor_off`, `monitor_on`, `screenshot`, `brightness_up`, `brightness_down`, `brightness:50`

## Installation

```bash
# Download DMG and install
# or: Build from source
dotnet build src/HaDeskLink -c Release -r osx-arm64
```

## 📐 Versioning
Starting from v2.2.1, each platform has **independent version numbers**:

| Change | Example | Description |
|---|---|---|
| **Bug Fix** | 2.2.1 → 2.2.2 | Bug fix, affected platform only |
| **New Features** | 2.2.x → 3.0.0 | New features, all platforms simultaneously |

Each platform (Windows, Linux, macOS) has **its own version number**. A bug fix on macOS doesn't change the Windows version – and vice versa. Major feature updates bump all platforms at once.

## License

GPL v3 – see [LICENSE](LICENSE)

## AI Assistance

Idea: Fabian Kirchweger | Code: GLM-5.1 (via OpenClaw) – see [CREDITS.md](CREDITS.md)

[Deutsche Version](README.md)