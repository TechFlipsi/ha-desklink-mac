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

        // Disk usage for root volume
        var diskPercent = GetDiskUsage();
        if (diskPercent.HasValue)
            sensors.Add(new SensorData("disk_usage", "Festplatte", diskPercent.Value, "%", "", "mdi:harddisk", "measurement"));

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
        try
        {
            // Try using powermetrics (requires sudo on some systems)
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

            // Parse CPU die temperature from powermetrics output
            // Example line: "CPU die temperature: 58.75 C"
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

        // Fallback: try osx-cpu-temp if installed
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
            // Output: "58.75°C"
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
                var idleStr = output.Split("idle")[0].Trim();
                var percentStr = idleStr.Split('%').Last().Trim();
                // Extract the idle percentage
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
                Arguments = "-c \"vm_stat | head -10; sysctl -n hw.memsize\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return (null, null);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            ulong totalPages = 0, freePages = 0, inactivePages = 0, totalPagesSpecified = 0;
            ulong totalMemory = 0;
            const int pageSize = 16384; // Apple Silicon: 16KB pages

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Pages free:"))
                    freePages = ParseUlong(line.Split(':')[1].Trim().Trim('.'));
                else if (line.Contains("Pages inactive:"))
                    inactivePages = ParseUlong(line.Split(':')[1].Trim().Trim('.'));
                else if (line.Contains("hw.memsize"))
                    totalMemory = ParseUlong(line.Trim());
                else if (line.Contains("page size of"))
                {
                    var psMatch = System.Text.RegularExpressions.Regex.Match(line, @"page size of (\d+)");
                    if (psMatch.Success) { /* could override pageSize */ }
                }
            }

            // If we got hw.memsize, use it
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

            // Output: "-1	100%; discharging; ..."
            // or: "100%; charging; ..."
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var pct))
            {
                bool charging = output.Contains("charging") || output.Contains("AC Power");
                bool discharging = output.Contains("discharging");
                return (pct, charging);
            }
        }
        catch { }
        return (null, null);
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

            // Output: "Filesystem Size Used Avail Capacity Mounted on\n/dev/... 500G 250G 250G 50% /"
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

    #region Fullscreen

    private static string GetFullscreenApp()
    {
        try
        {
            // Use AppleScript to check for fullscreen apps
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

            // Check each visible process for fullscreen
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

                    // Check if window covers full screen
                    if (!string.IsNullOrEmpty(bounds) && bounds.Contains(","))
                    {
                        // Simple heuristic: if window starts at 0,0 it might be fullscreen
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
            // Try brightness command (Homebrew installable)
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

        // Fallback: try osascript
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

        // Try brightness command
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

        // Fallback: osascript
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