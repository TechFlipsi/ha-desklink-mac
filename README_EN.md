# HA DeskLink macOS v3.0

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/ha-desklink-mac/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/ha-desklink-mac/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/ha-desklink-mac?label=Version)](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/ha-desklink-mac?label=License)](https://github.com/TechFlipsi/ha-desklink-mac/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/ha-desklink-mac/total?label=Downloads)](https://github.com/TechFlipsi/ha-desklink-mac/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

**Home Assistant Companion App for macOS**

📖 **[Manual](MANUAL.md)** – Installation, Sensors, Commands, Quick Actions, Actionable Notifications, Screenshot, Webcam & more (DE + EN)

📊 **[HASS.Agent vs. HA DeskLink](COMPARISON.md)** – Feature comparison (DE + EN)

⚠️ **COMMUNITY TEST VERSION – NOT TESTED BY THE DEVELOPER!** ⚠️

This version was created without Mac hardware and relies on **community testing**.
Please report bugs at [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).

---

## Features
- 🌡️ **CPU Temperature** – via ioreg SMC, powermetrics, or osx-cpu-temp
- 📊 **All Sensors** – CPU, RAM, Battery, Disk, Uptime, Network, macOS-exclusive sensors
- 🖥️ **PC Commands from HA** – Shutdown, Restart, Sleep, Lock, Volume, Brightness
- 🖥️ **Graphical Interface** – Avalonia UI dashboard with status & setup
- 📬 **Push Notifications** – WebSocket-based, like the mobile app
- 🔔 **Actionable Notifications** – Notifications with action buttons
- ⚡ **Quick Actions** – Dashboard button for HA entity toggles
- 📸 **Screenshot** – Screenshot save + upload as HA event
- 📷 **Webcam Sensor** – Shows if webcam is active (on/off)
- 🔌 **mobile_app Protocol** – identical to the Windows app
- 🔄 **Auto-Update** – checks for updates on startup
- 🍎 **macOS-exclusive Sensors** – Battery cycle count, Power adapter, Keyboard backlight, GPU model, Display resolution

## Sensors

| Sensor | Description | macOS Exclusive |
|---|---|:---:|
| `cpu_temp` | CPU temperature in °C | |
| `cpu_usage` | CPU usage in % | |
| `memory` | RAM usage in % | |
| `memory_available` | RAM available in GB | |
| `battery` | Battery level in % | |
| `battery_charging` | Battery charging | |
| `battery_cycle_count` | Battery cycle count | ✅ |
| `power_adapter` | Power adapter connected | ✅ |
| `disk_usage` | Drive usage in % | |
| `uptime` | Uptime in minutes | |
| `gpu_model` | GPU model name | ✅ |
| `display_resolution` | Display resolution | ✅ |
| `process_count` | Number of processes | |
| `ip_address` | IPv4 address | |
| `wifi_ssid` | WiFi network name | |
| `keyboard_backlight` | Keyboard backlight in % | ✅ |
| `fullscreen` | Fullscreen mode (on/off) | |
| `monitor_layout` | Monitor layout | |
| `brightness` | Screen brightness in % | |
| `webcam_active` | Webcam active (on/off) | |
| `ha_desklink_version` | App version | |

### CPU Temperature on macOS

The CPU temperature is attempted in this order:
1. **ioreg SMC** (no installation needed) – reads from Apple System Management Controller
2. **powermetrics** – requires `sudo` on many systems
3. **osx-cpu-temp** – external tool: `brew install osx-cpu-temp`

### Sensors NOT Available on macOS

| Sensor | Windows | Linux | macOS | Why? |
|---|:---:|:---:|:---:|---|
| GPU Temperature | ✅ | ✅ | ❌ | No public API, LibreHardwareMonitor not available |
| GPU Load | ✅ | ✅ | ❌ | Only via `sudo powermetrics` |
| CPU Clock | ✅ | ✅ | ❌ | `sysctl hw.cpufrequency` doesn't work on Apple Silicon |
| Fan Speed | ✅ | ✅ | ❌ | Only via `sudo powermetrics` |
| Network Upload/Download | ✅ | ✅ | ❌ | No user-level API for live network speed |

## Commands

`shutdown`, `restart`, `sleep`, `lock`, `mute`, `volume_up`, `volume_down`, `monitor_off`, `monitor_on`, `screenshot`, `screenshot_save`, `brightness_up`, `brightness_down`, `brightness:50`

> ⚠️ **Brightness commands** generally only work on **MacBooks** with built-in displays. External monitors may not support software brightness control.

## Installation

1. Download `.dmg` from [Releases](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest)
2. Drag app to Applications folder
3. On first start: enter HA URL + Token

## 📐 Versioning
Starting from v2.2.1, each platform has **independent version numbers**:

| Change | Example | Description |
|---|---|---|
| **Bug Fix** | 2.2.1 → 2.2.2 | Bug fix, affected platform only |
| **New Features** | 2.2.x → 3.0.0 | New features, all platforms simultaneously |

## License
GPL v3 – see [LICENSE](LICENSE)

## Attribution
Idea: Fabian Kirchweger | Code: GLM-5.1 (via OpenClaw) – see [CREDITS.md](CREDITS.md)

[Deutsch](README.md)