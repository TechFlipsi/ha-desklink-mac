# HA DeskLink macOS v2.2.1

**Home Assistant Companion App für macOS**

⚠️ **COMMUNITY TEST VERSION – NICHT VOM ENTWICKLER GETESTET!** ⚠️

Diese Version wurde ohne Mac-Hardware erstellt und ist auf **Community-Testing** angewiesen.
Bitte melde Bugs unter [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).

---

## ⚠️ WICHTIGER HINWEIS

**Diese macOS-Version wurde NICHT vom Entwickler (Fabian Kirchweger) getestet!**
Es gibt keine Mac-Hardware zum Testen. Die App wurde basierend auf der funktionierenden Windows-Version erstellt.
Wenn du macOS nutzt, teste bitte diese Version und melde Bugs – **die Community ist der Tester!**

---

## Features

- 🖥️ CPU-Temperatur, CPU-Auslastung, RAM, Akku, Festplatte
- 🖥️ Vollbild-Sensor (welches Programm ist im Vollbild)
- 📺 Monitor-Layout-Sensor
- ☀️ Helligkeit steuern & anzeigen
- 🌍 Mehrsprachigkeit (de, en, es, fr, zh, ja)
- 📊 Dashboard öffnet Home Assistant im Browser
- 🔒 HA Token verschlüsselt (macOS Keychain + AES-GCM)
- 🔔 Push-Benachrichtigungen via WebSocket

## Befehle

`shutdown`, `restart`, `sleep`, `lock`, `mute`, `volume_up`, `volume_down`, `monitor_off`, `monitor_on`, `screenshot`, `brightness_up`, `brightness_down`, `brightness:50`

## Installation

```bash
# DMG herunterladen und installieren
# oder: Aus dem Quellcode bauen
dotnet build src/HaDeskLink -c Release -r osx-arm64
```

## Lizenz

GPL v3 – siehe [LICENSE](LICENSE)

## KI-Unterstützung

Idee: Fabian Kirchweger | Code: GLM-5.1 (via OpenClaw) – siehe [CREDITS.md](CREDITS.md)

[English Version](README_EN.md)