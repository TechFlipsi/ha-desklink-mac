# HA DeskLink macOS v3.0

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/ha-desklink-mac/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/ha-desklink-mac/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/ha-desklink-mac?label=Version)](https://github.com/TechFlipsi/ha-desklink-mac/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/ha-desklink-mac?label=License)](https://github.com/TechFlipsi/ha-desklink-mac/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/ha-desklink-mac/total?label=Downloads)](https://github.com/TechFlipsi/ha-desklink-mac/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

**Home Assistant Companion App für macOS**

> 🔍 **Suchst du einen Home Assistant Desktop-Companion für macOS?** HA DeskLink verbindet deinen Mac direkt mit Home Assistant – Sensordaten, Systemstatus und Steuerelemente live auf dem Desktop.

<!-- SEO: home assistant macos desktop app, home assistant mac companion, hass macos, home assistant sensor monitor mac, smart home mac widget -->

📖 **[Betriebsanleitung / Manual](MANUAL.md)** – Installation, Sensoren, Befehle, Quick Actions, Actionable Notifications, Screenshot, Webcam, Plattform-Vergleich & mehr (DE + EN)

📊 **[HASS.Agent vs. HA DeskLink](COMPARISON.md)** – Features, Architektur & Migration im Vergleich (DE + EN)

⚠️ **COMMUNITY TEST VERSION – NICHT VOM ENTWICKLER GETESTET!** ⚠️

Diese Version wurde ohne Mac-Hardware erstellt und ist auf **Community-Testing** angewiesen.
Bitte melde Bugs unter [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).

---

## ⚠️ WICHTIGER HINWEIS

**Diese macOS-Version wurde NICHT vom Entwickler (Fabian Kirchweger) getestet!**
Es gibt keine Mac-Hardware zum Testen. Die App wurde basierend auf der funktionierenden Windows-Version erstellt.
Wenn du macOS nutzt, teste bitte diese Version und melde Bugs – **die Community ist der Tester!**

---

## v3.0 New Features

- 🔔 **Actionable Notifications** – Benachrichtigungen mit Aktions-Buttons via osascript
- ⚡ **Quick Actions** – Dashboard-Button mit HA-Entity-Toggle-Popup
- 📸 **Screenshot Save+Upload** – `screenshot_save` speichert via `screencapture` und lädt als HA-Event hoch
- 📷 **Webcam-Sensor** – Neuer Sensor `webcam_active` (on/off)

## Sensoren

| Sensor | ID | Einheit | Verfügbar | Hinweis |
|---|---|---|---|---|
| CPU-Temperatur | `cpu_temp` | °C | ⚠️ | Siehe unten |
| CPU-Auslastung | `cpu_usage` | % | ✅ | |
| RAM Auslastung | `memory` | % | ✅ | |
| RAM Verfügbar | `memory_available` | GB | ✅ | |
| Akku | `battery` | % | ✅ | |
| Akku lädt | `battery_charging` | – | ✅ | |
| Akku-Ladezyklen | `battery_cycle_count` | – | ✅ | 🍎 macOS-exklusiv |
| Netzteil | `power_adapter` | – | ✅ | 🍎 macOS-exklusiv |
| Festplatte | `disk_usage` | % | ✅ | |
| Betriebszeit | `uptime` | min | ✅ | |
| GPU-Modell | `gpu_model` | – | ✅ | 🍎 macOS-exklusiv |
| Bildschirmauflösung | `display_resolution` | – | ✅ | 🍎 macOS-exklusiv |
| Prozesse | `process_count` | – | ✅ | |
| IP-Adresse | `ip_address` | – | ✅ | |
| WiFi-Name | `wifi_ssid` | – | ✅ | |
| Tastaturbeleuchtung | `keyboard_backlight` | % | ⚠️ | 🍎 macOS-exklusiv, nicht auf allen Macs |
| Vollbild | `fullscreen` | – | ✅ | |
| Monitor-Layout | `monitor_layout` | – | ✅ | |
| Helligkeit | `brightness` | % | ✅ | |
| Webcam aktiv | `webcam_active` | – | ✅ | on/off |
| App-Version | `ha_desklink_version` | – | ✅ | Aktuelle Version |

### CPU-Temperatur – Erklärung

Die CPU-Temperatur wird in folgender Reihenfolge versucht:

1. **ioreg SMC** (keine Installation nötig) – liest direkt aus dem Apple System Management Controller. Funktioniert ohne sudo auf den meisten Macs.
2. **powermetrics** – macOS-Bordmittel, benötigt aber `sudo` auf vielen Systemen.
3. **osx-cpu-temp** – externes Tool, muss installiert werden: `brew install osx-cpu-temp`

Wenn keine Methode funktioniert, bleibt der Sensor leer.

### Nicht verfügbare Sensoren (im Vergleich zu Windows/Linux)

| Sensor | Windows | Linux | macOS | Warum nicht auf macOS? |
|---|---|---|---|---|
| GPU-Temperatur | ✅ LibreHardwareMonitor | ✅ | ❌ | Keine öffentliche API, LibreHardwareMonitor gibt es nicht für macOS |
| GPU-Auslastung | ✅ LibreHardwareMonitor | ✅ | ❌ | Nur via `sudo powermetrics` – nicht ohne Admin-Rechte |
| CPU-Takt | ✅ LibreHardwareMonitor | ✅ | ❌ | `sysctl hw.cpufrequency` funktioniert **nicht** auf Apple Silicon (nur Intel-Macs) |
| Lüfter-Drehzahl | ✅ LibreHardwareMonitor | ✅ | ❌ | Nur via `sudo powermetrics` auslesbar |
| Lüfter-Steuerung | ✅ | ❌ | ❌ | System-geregelt, kein User-Zugang |
| Netzwerk-Upload/Download | ✅ | ✅ | ❌ | `netstat -ib` liefert Byte-Counts aber keine Live-Rate ohne Polling-Logik |
| WLAN-Signal | ✅ | ✅ | ❌ | Keine User-Level API für Signalstärke auf macOS |
| Seitenfile | ✅ | ❌ | ❌ | macOS hat kein Äquivalent zum Windows Page File |

## Befehle

`shutdown`, `restart`, `sleep`, `lock`, `mute`, `volume_up`, `volume_down`, `monitor_off`, `monitor_on`, `screenshot`, `screenshot_save`, `brightness_up`, `brightness_down`, `brightness:50`

## Installation

```bash
# ZIP herunterladen und entpacken
# oder: Aus dem Quellcode bauen
dotnet build src/HaDeskLink -c Release -r osx-arm64
```

## 📐 Versionierung
Ab v2.2.1 gelten **plattformunabhängige Versionsnummern**:

| Änderung | Beispiel | Erklärung |
|---|---|---|
| **Bug Fix** | 2.2.1 → 2.2.2 | Fehlerbehebung, nur betroffene Plattform |
| **Neue Funktionen** | 2.2.x → 3.0.0 | Neue Features, alle Plattformen gleichzeitig |

Jede Plattform (Windows, Linux, macOS) hat **eigene Versionsnummern**. Ein Bug-Fix unter macOS ändert nicht die Windows-Version – und umgekehrt. Große Funktionsupdates (Major) bekommen alle Plattformen gleichzeitig.

## Lizenz

GPL v3 – siehe [LICENSE](LICENSE)

## KI-Unterstützung

Idee: Fabian Kirchweger | Code: GLM-5.1 (via OpenClaw) – siehe [CREDITS.md](CREDITS.md)

[English Version](README_EN.md)