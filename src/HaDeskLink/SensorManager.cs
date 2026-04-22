// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HaDeskLink;

/// <summary>
/// Collects sensor data on macOS.
/// 
/// ⚠️ COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER
/// Some sensors may not work correctly on all macOS versions.
/// </summary>
public static class SensorManager
{
    public static List<SensorData> GetAllSensors()
    {
        var sensors = new List<SensorData>();

        // CPU Temperature
        var cpuTemp = GetCpuTemperature();
        if (cpuTemp.HasValue)
            sensors.Add(new SensorData("cpu_temp", "CPU Temperatur", cpuTemp.Value, "°C", "temperature", "mdi:thermometer", "measurement"));

        // CPU Usage
        var cpuUsage = GetCpuUsage();
        if (cpuUsage.HasValue)
            sensors.Add(new SensorData("cpu_usage", "CPU Auslastung", cpuUsage.Value, "%", "", "mdi:cpu-64-bit", "measurement"));

        // Memory
        var (memPercent, memAvail) = GetMemory();
        if (memPercent.HasValue)
            sensors.Add(new SensorData("memory", "RAM Auslastung", memPercent.Value, "%", "", "mdi:memory", "measurement"));
        if (memAvail.HasValue)
            sensors.Add(new SensorData("memory_available", "RAM Verfügbar", memAvail.Value, "GB", "", "mdi:memory", "measurement"));

        // Battery
        var (batPercent, batCharging) = GetBattery();
        if (batPercent.HasValue)
            sensors.Add(new SensorData("battery", "Akku", batPercent.Value, "%", "battery", "mdi:battery", "measurement"));
        if (batCharging.HasValue)
            sensors.Add(new SensorData("battery_charging", "Akku Laden", batCharging.Value ? "Lädt" : "Nicht ladend", "", "plug", "mdi:battery-charging"));

        // Battery cycle count (macOS exclusive)
        var cycleCount = GetBatteryCycleCount();
        if (cycleCount.HasValue)
            sensors.Add(new SensorData("battery_cycle_count", "Akku-Ladezyklen", cycleCount.Value, "", "battery", "mdi:battery-heart", "measurement"));

        // Power adapter status (macOS exclusive)
        var powerAdapter = GetPowerAdapterStatus();
        sensors.Add(new SensorData("power_adapter", "Netzteil", powerAdapter, "", "plug", "mdi:power-plug"));

        // Disk usage for root volume
        var diskPercent = GetDiskUsage();
        if (diskPercent.HasValue)
            sensors.Add(new SensorData("disk_usage", "Festplatte", diskPercent.Value, "%", "", "mdi:harddisk", "measurement"));

        // Uptime
        var uptime = GetUptime();
        if (uptime.HasValue)
            sensors.Add(new SensorData("uptime", "Betriebszeit", uptime.Value, "min", "", "mdi:clock-outline", "measurement"));

        // GPU info (macOS exclusive – model name)
        var gpuModel = GetGpuModel();
        if (!string.IsNullOrEmpty(gpuModel))
            sensors.Add(new SensorData("gpu_model", "GPU-Modell", gpuModel, "", "", "mdi:chip"));

        // Display resolution (macOS exclusive)
        var displayRes = GetDisplayResolution();
        if (!string.IsNullOrEmpty(displayRes))
            sensors.Add(new SensorData("display_resolution", "Bildschirmauflösung", displayRes, "", "", "mdi:monitor-screenshot"));

        // Process count
        var processCount = GetProcessCount();
        if (processCount.HasValue)
            sensors.Add(new SensorData("process_count", "Prozesse", processCount.Value, "", "", "mdi:application-cog", "measurement"));

        // IP Address
        var ipAddress = GetIpAddress();
        if (!string.IsNullOrEmpty(ipAddress))
            sensors.Add(new SensorData("ip_address", "IP-Adresse", ipAddress, "", "", "mdi:ip-network"));

        // WiFi SSID
        var wifiSsid = GetWifiSsid();
        if (!string.IsNullOrEmpty(wifiSsid))
            sensors.Add(new SensorData("wifi_ssid", "WiFi-Name", wifiSsid, "", "", "mdi:wifi"));

        // Keyboard backlight (macOS exclusive)
        var kbBacklight = GetKeyboardBacklight();
        if (kbBacklight.HasValue)
            sensors.Add(new SensorData("keyboard_backlight", "Tastaturbeleuchtung", kbBacklight.Value, "%", "", "mdi:keyboard-outline", "measurement"));

        // Fullscreen sensor
        var fullscreenApp = GetFullscreenApp();
        sensors.Add(new SensorData("fullscreen_app", "Vollbild-App", fullscreenApp, "", "", "mdi:fullscreen"));
        sensors.Add(new SensorData("fullscreen", "Vollbild", string.IsNullOrEmpty(fullscreenApp) ? "Aus" : "An", "", "", "mdi:fullscreen"));

        // Monitor layout
        var layout = GetMonitorLayout();
        sensors.Add(new SensorData("monitor_layout", "Monitor-Layout", layout, "", "", "mdi:monitor"));

        // Brightness
        var brightness = GetCurrentBrightness();
        if (brightness.HasValue)
            sensors.Add(new SensorData("brightness", "Helligkeit", brightness.Value, "%", "", "mdi:brightness-6", "measurement"));

        return sensors;
    }

    #region CPU & Hardware

    private static float? GetCpuTemperature()
    {
        // Method 1: ioreg SMC – reads temperature directly from AppleSMC, NO sudo needed
        try
        {
            // Try multiple SMC temp keys (different Mac models use different keys)
            var tempKeys = new[] { "TA0p", "TC0P", "TC0H", "TC0D", "TCGC", "TG0P", "TG0D", "Th1H" };
            foreach (var key in tempKeys)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ioreg",
                        Arguments = "-r -n AppleSMC",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    var proc = Process.Start(psi);
                    if (proc == null) continue;
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    // Find the key and its hex value: "TA0p" = <000e2c00>
                    var keyPattern = "\"" + key + "\"";
                    var keyIdx = output.IndexOf(keyPattern);
                    if (keyIdx < 0) continue;

                    // Look for hex value <XXXXXXXX> near the key
                    var afterKey = output.Substring(keyIdx);
                    var match = System.Text.RegularExpressions.Regex.Match(afterKey, @"<([0-9a-fA-F]{8})>");
                    if (match.Success)
                    {
                        var hex = match.Groups[1].Value;
                        if (hex.Length == 8)
                        {
                            var b0 = Convert.ToInt32(hex.Substring(0, 2), 16);
                            var b1 = Convert.ToInt32(hex.Substring(2, 2), 16);
                            var b2 = Convert.ToInt32(hex.Substring(4, 2), 16);
                            var b3 = Convert.ToInt32(hex.Substring(6, 2), 16);

                            // SMC temp encoding: big-endian 2-byte value, divide by 256
                            // Try both pair orderings
                            int val1 = (b0 << 8) | b1;
                            int val2 = (b2 << 8) | b3;
                            float t1 = val1 / 256f;
                            float t2 = val2 / 256f;
                            if (t1 >= 10 && t1 <= 120)
                                return (float)Math.Round(t1, 1);
                            if (t2 >= 10 && t2 <= 120)
                                return (float)Math.Round(t2, 1);
                        }
                    }
                }
                catch { continue; }
            }
        }
        catch { }

        // Method 2: powermetrics (may require sudo on some systems)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powermetrics",
                Arguments = "--samplers smc -i 1 -n 1",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("CPU die temperature") || line.Contains("die temperature"))
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length > 1)
                    {
                        var tempStr = parts[1].Trim().Replace("C", "").Replace("°", "").Trim();
                        if (float.TryParse(tempStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temp))
                            return temp;
                    }
                }
            }
        }
        catch { }

        // Method 3: osx-cpu-temp (requires: brew install osx-cpu-temp)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osx-cpu-temp",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            output = output.Replace("°C", "").Trim();
            if (float.TryParse(output, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temp))
                return temp;
        }
        catch { }

        return null;
    }

    private static float? GetCpuUsage()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"top -l 1 -n 0 | grep 'CPU usage'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            // Output: "CPU usage: 12.5% user, 8.3% sys, 79.2% idle"
            if (output.Contains("idle"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+[\.,]\d+)%\s*idle");
                if (match.Success && float.TryParse(match.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var idle))
                {
                    return 100f - idle;
                }
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region Memory

    private static (float? percent, float? availableGB) GetMemory()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"vm_stat | head -10; echo '---'; sysctl -n hw.memsize\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return (null, null);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            ulong freePages = 0, inactivePages = 0;
            ulong totalMemory = 0;
            const int pageSize = 16384; // Apple Silicon: 16KB pages

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Pages free:"))
                    freePages = ParseUlong(line.Split(':')[1].Trim().Trim('.'));
                else if (line.Contains("Pages inactive:"))
                    inactivePages = ParseUlong(line.Split(':')[1].Trim().Trim('.'));
            }

            // Parse hw.memsize (after --- separator)
            var sections = output.Split("---");
            if (sections.Length > 1)
            {
                var memStr = sections[1].Trim();
                totalMemory = ParseUlong(memStr);
            }

            if (totalMemory > 0)
            {
                var freeMemory = (freePages + inactivePages) * (ulong)pageSize;
                var usedPercent = (1f - (float)freeMemory / totalMemory) * 100f;
                return ((float)Math.Round(usedPercent, 1), (float)Math.Round((float)freeMemory / 1024 / 1024 / 1024, 1));
            }
        }
        catch { }
        return (null, null);
    }

    private static ulong ParseUlong(string s)
    {
        return ulong.TryParse(s.Trim(), out var v) ? v : 0;
    }

    #endregion

    #region Battery

    private static (float? percent, bool? charging) GetBattery()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"pmset -g batt\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return (null, null);
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var pct))
            {
                bool charging = output.Contains("charging") || output.Contains("AC Power");
                return (pct, charging);
            }
        }
        catch { }
        return (null, null);
    }

    private static int? GetBatteryCycleCount()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"system_profiler SPPowerDataType | grep 'Cycle Count'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            // Output: "Cycle Count: 42"
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Cycle Count:\s*(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                return count;
        }
        catch { }
        return null;
    }

    private static string GetPowerAdapterStatus()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"pmset -g adapter\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return "Unbekannt";
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            // If adapter info is present, it's connected
            if (!string.IsNullOrEmpty(output) && output.Contains("Watt"))
                return "Angeschlossen";
        }
        catch { }

        // Fallback: check via pmset -g batt
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"pmset -g batt\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return "Unbekannt";
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            if (output.Contains("AC Power") || output.Contains("charging"))
                return "Angeschlossen";
            if (output.Contains("discharging"))
                return "Nicht angeschlossen";
        }
        catch { }

        return "Unbekannt";
    }

    #endregion

    #region Disk

    private static float? GetDiskUsage()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "df",
                Arguments = "-h /",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("/") && line.Contains("%"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)%");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var pct))
                        return pct;
                }
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region Uptime

    private static float? GetUptime()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"sysctl -n kern.boottime\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            // Output: "sec = 1703275200, usec = 0" (Unix timestamp of boot)
            var match = System.Text.RegularExpressions.Regex.Match(output, @"sec = (\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var bootSec))
            {
                var bootTime = DateTimeOffset.FromUnixTimeSeconds(bootSec);
                var uptime = DateTimeOffset.UtcNow - bootTime;
                return (float)Math.Round(uptime.TotalMinutes, 0);
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region GPU

    private static string? GetGpuModel()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"system_profiler SPDisplaysDataType | grep 'Chipset Model'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            // Output: "Chipset Model: Apple M1"
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Chipset Model:"))
                {
                    var model = line.Split(new[] { ':' }, 2).Last().Trim();
                    if (!string.IsNullOrEmpty(model))
                        return model;
                }
            }
        }
        catch { }
        return null;
    }

    private static string? GetDisplayResolution()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"system_profiler SPDisplaysDataType | grep 'Resolution'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            // Output: "Resolution: 2560 x 1600 Retina"
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Resolution:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s*x\s*(\d+)");
                    if (match.Success)
                        return $"{match.Groups[1].Value}x{match.Groups[2].Value}";
                }
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region Process Count

    private static int? GetProcessCount()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"ps aux | wc -l\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            if (int.TryParse(output, out var count))
                return Math.Max(0, count - 1); // subtract header line
        }
        catch { }
        return null;
    }

    #endregion

    #region Network

    private static string? GetIpAddress()
    {
        try
        {
            // Try primary interface (en0 = usually WiFi or Ethernet on Mac)
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"ipconfig getifaddr en0 2>/dev/null || echo ''\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            if (!string.IsNullOrEmpty(output) && output.Contains("."))
                return output;
        }
        catch { }

        // Fallback: try en1 (secondary interface)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"ipconfig getifaddr en1 2>/dev/null || echo ''\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            if (!string.IsNullOrEmpty(output) && output.Contains("."))
                return output;
        }
        catch { }
        return null;
    }

    private static string? GetWifiSsid()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"networksetup -getairportnetwork en0 2>/dev/null || /System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport -I 2>/dev/null | grep ' SSID'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            // Output: "Current Wi-Fi Network: MyNetwork" or "SSID: MyNetwork"
            if (output.Contains(":"))
            {
                var ssid = output.Split(new[] { ':' }, 2).Last().Trim();
                if (!string.IsNullOrEmpty(ssid) && !ssid.Contains("could not") && !ssid.Contains("not found"))
                    return ssid;
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region Keyboard Backlight

    private static int? GetKeyboardBacklight()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"ioreg -r -c AppleBacklightDisplay | grep -i brightness\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);

                var match = System.Text.RegularExpressions.Regex.Match(output, @"brightness.*?(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var val))
                    return Math.Clamp(val, 0, 100);
            }
        }
        catch { }

        // Fallback: try keyboard backlight via osascript
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e 'tell application \"System Events\" to get keyboard brightness'",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (float.TryParse(output, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    return (int)Math.Round(val * 100);
                }
            }
        }
        catch { }
        return null;
    }

    #endregion

    #region Fullscreen

    private static string GetFullscreenApp()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e 'tell application \"System Events\" to get name of first process whose visible is true and (count of windows) > 0'",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return "";
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            foreach (var appName in output.Split(',').Select(s => s.Trim().Trim('"')))
            {
                if (string.IsNullOrEmpty(appName)) continue;
                try
                {
                    var fsPsi = new ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e 'tell application \"{appName}\"' -e 'get bounds of window 1' -e 'end tell'",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    var fsProc = Process.Start(fsPsi);
                    if (fsProc == null) continue;
                    var bounds = fsProc.StandardOutput.ReadToEnd().Trim();
                    fsProc.WaitForExit(2000);

                    if (!string.IsNullOrEmpty(bounds) && bounds.Contains(","))
                    {
                        var parts = bounds.Split(',').Select(s => s.Trim()).ToArray();
                        if (parts.Length >= 4 && parts[0] == "0" && parts[1] == "0")
                            return appName;
                    }
                }
                catch { continue; }
            }
        }
        catch { }
        return "";
    }

    #endregion

    #region Monitor Layout

    private static string GetMonitorLayout()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"system_profiler SPDisplaysDataType | grep -c 'Resolution:'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return "1";
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (int.TryParse(output, out var count) && count > 0)
                return count == 1 ? "1" : $"1+{count - 1}";
        }
        catch { }
        return "unbekannt";
    }

    #endregion

    #region Brightness

    public static int? GetCurrentBrightness()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "brightness",
                Arguments = "-l",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                var match = System.Text.RegularExpressions.Regex.Match(output, @"brightness\s+([\d.]+)");
                if (match.Success && float.TryParse(match.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    return (int)Math.Round(val * 100);
                }
            }
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e 'tell application \"System Events\" to get brightness of current screen'",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (float.TryParse(output, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    return (int)Math.Round(val * 100);
                }
            }
        }
        catch { }

        return null;
    }

    public static void SetBrightness(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        var fraction = (percent / 100f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "brightness",
                Arguments = fraction,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit(3000);
            return;
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e 'tell application \"System Events\" to set brightness of current screen to {fraction}'",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit(3000);
        }
        catch { }
    }

    #endregion
}