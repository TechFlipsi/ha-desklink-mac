# HA DeskLink macOS v2.2.2

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/ha-desklink-mac/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/ha-desklink-mac/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/ha-desklink-mac?label=Version)](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/ha-desklink-mac?label=License)](https://github.com/TechFlipsi/ha-desklink-mac/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/ha-desklink-mac/total?label=Downloads)](https://github.com/TechFlipsi/ha-desklink-mac/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

**Home Assistant Companion App for macOS**

⚠️ **COMMUNITY TEST VERSION – NOT TESTED BY THE DEVELOPER!** ⚠️

This version was created without Mac hardware and relies on **community testing**.
Please report bugs at [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).

---

## ⚠️ IMPORTANT NOTICE

**This macOS version has NOT been tested by the developer (Fabian Kirchweger)!**
There is no Mac hardware available for testing. The app was built based on the working Windows version.
If you use macOS, please test this version and report bugs – **the community is the tester!**

---

## Sensors

| Sensor | ID | Unit | Available | Note |
|---|---|---|---|---|
| CPU Temperature | `cpu_temp` | °C | ⚠️ | See below |
| CPU Usage | `cpu_usage` | % | ✅ | |
| RAM Usage | `memory` | % | ✅ | |
| RAM Available | `memory_available` | GB | ✅ | |
| Battery | `battery` | % | ✅ | |
| Battery Charging | `battery_charging` | – | ✅ | |
| Battery Cycle Count | `battery_cycle_count` | – | ✅ | 🍎 macOS exclusive |
| Power Adapter | `power_adapter` | – | ✅ | 🍎 macOS exclusive |
| Disk Usage | `disk_usage` | % | ✅ | |
| Uptime | `uptime` | min | ✅ | |
| GPU Model | `gpu_model` | – | ✅ | 🍎 macOS exclusive |
| Display Resolution | `display_resolution` | – | ✅ | 🍎 macOS exclusive |
| Process Count | `process_count` | – | ✅ | |
| IP Address | `ip_address` | – | ✅ | |
| WiFi SSID | `wifi_ssid` | – | ✅ | |
| Keyboard Backlight | `keyboard_backlight` | % | ⚠️ | 🍎 macOS exclusive, not on all Macs |
| Fullscreen App | `fullscreen_app` | – | ✅ | |
| Fullscreen | `fullscreen` | – | ✅ | |
| Monitor Layout | `monitor_layout` | – | ✅ | |
| Brightness | `brightness` | % | ✅ | |

### CPU Temperature – Explanation

CPU temperature is attempted in the following order:

1. **ioreg SMC** (no installation needed) – reads directly from the Apple System Management Controller. Works without sudo on most Macs.
2. **powermetrics** – built into macOS, but requires `sudo` on many systems.
3. **osx-cpu-temp** – external tool, install via: `brew install osx-cpu-temp`

If no method works, the sensor stays empty.

### Unavailable Sensors (compared to Windows/Linux)

| Sensor | Windows | Linux | macOS | Why not on macOS? |
|---|---|---|---|---|
| GPU Temperature | ✅ LibreHardwareMonitor | ✅ | ❌ | No public API, LibreHardwareMonitor not available for macOS |
| GPU Usage | ✅ LibreHardwareMonitor | ✅ | ❌ | Only via `sudo powermetrics` – not without admin rights |
| CPU Clock | ✅ LibreHardwareMonitor | ✅ | ❌ | `sysctl hw.cpufrequency` does **not** work on Apple Silicon (Intel Macs only) |
| Fan Speed | ✅ LibreHardwareMonitor | ✅ | ❌ | Only readable via `sudo powermetrics` |
| Fan Control | ✅ | ❌ | ❌ | System-controlled, no user access |
| Network Upload/Download | ✅ | ✅ | ❌ | `netstat -ib` provides byte counts but no live rate without polling logic |
| WiFi Signal | ✅ | ✅ | ❌ | No user-level API for signal strength on macOS |
| Page File | ✅ | ❌ | ❌ | macOS has no equivalent to Windows Page File |

## Commands

`shutdown`, `restart`, `sleep`, `lock`, `mute`, `volume_up`, `volume_down`, `monitor_off`, `monitor_on`, `screenshot`, `brightness_up`, `brightness_down`, `brightness:50`

## Installation

```bash
# Download and extract the ZIP
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