# Changelog


## [v3.0.5] - 2026-04-22
- ✨ **Neu:** ha_desklink_version Sensor – zeigt aktuelle App-Version in HA
- 🐛 **Bug Fix:** fullscreen_app Sensor entfernt (Duplikat von active_window/fullscreen)

## [v3.0.1] - 2026-04-22
- 🐛 **Bug Fix:** Token-Entschlüsselung gibt leeren String zurück → keine HA-Verbindung mehr (verhindert IP-Sperre durch zu viele fehlgeschlagene Auth-Versuche)
- 🐛 **Bug Fix:** WebhookServer-Crash durch disposed CancellationTokenSource (Windows)
Alle nennenswerten Änderungen an diesem Projekt werden hier dokumentiert.

## [v3.0.0] - 2026-04-22
- 🔔 **Actionable Notifications** – Benachrichtigungen mit Aktionen via osascript. Auto-executet `command_on_action`, zeigt verfügbare Aktionen im Notification-Text.
- ⚡ **Quick Actions** – Avalonia UI Popup mit HA-Entity-Toggle-Buttons. Button im Dashboard. Konfigurierbar in config.json (`QuickActions`-Feld).
- 📸 **Screenshot Save+Upload** – Neuer Befehl `screenshot_save`: speichert via `screencapture` und lädt als HA-Event hoch.
- 📷 **Webcam-Sensor** – Neuer Sensor `webcam_active` (on/off) prüft via lsof/ioreg ob Kamera aktiv.
- 🌍 **Neue Lokalisierungs-Keys** für alle 6 Sprachen

## [v2.2.2] - 2026-04-22
- 🆕 **Neue Sensoren:**
  - Akku-Ladezyklen (`battery_cycle_count`) – 🍎 macOS-exklusiv
  - Netzteil-Status (`power_adapter`) – 🍎 macOS-exklusiv
  - Betriebszeit (`uptime`)
  - GPU-Modell (`gpu_model`) – 🍎 macOS-exklusiv
  - Bildschirmauflösung (`display_resolution`) – 🍎 macOS-exklusiv
  - Prozess-Anzahl (`process_count`)
  - IP-Adresse (`ip_address`)
  - WiFi-Name (`wifi_ssid`)
  - Tastaturbeleuchtung (`keyboard_backlight`) – 🍎 macOS-exklusiv
- 🔧 **CPU-Temperatur:** Jetzt mit ioreg SMC-Methode (kein `brew install` nötig) als primäre Option
- 📋 **Sensor-Vergleich** in README: Welche Sensoren fehlen und warum

## [v2.2.1] - 2026-04-22
- 🔒 **Sicherheitsupdate:** HA Token wird jetzt verschlüsselt gespeichert (macOS Keychain + AES-GCM Fallback)
- Nie wieder Klartext-Tokens in der Konfigurationsdatei

## [v2.2.0] - 2026-04-22
- 🖥️ **Vollbild-Sensor** – zeigt welches Programm im Vollbild läuft
- 📺 **Monitor-Layout-Sensor** – aktives Monitor-Setup
- ☀️ **Helligkeit steuern** – `brightness_up`, `brightness_down`, `brightness:N` + Sensor
- 🌍 **Mehrsachigkeit** – Deutsch, Englisch, Spanisch, Französisch, Chinesisch, Japanisch

## ⚠️ HINWEIS
Diese macOS-Version ist eine **Community Test Version** und wurde NICHT vom Entwickler getestet!
Bitte teste und melde Bugs unter [Issues](https://github.com/TechFlipsi/ha-desklink-mac/issues).