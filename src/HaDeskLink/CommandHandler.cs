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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace HaDeskLink;

/// <summary>
/// Execute system commands on macOS.
/// ⚠️ COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER
/// </summary>
public static class CommandHandler
{
    public static async Task ExecuteAsync(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "shutdown":
                Run("osascript", "-e 'tell app \"System Events\" to shut down'");
                break;
            case "restart":
            case "reboot":
                Run("osascript", "-e 'tell app \"System Events\" to restart'");
                break;
            case "hibernate":
            case "sleep":
                Run("osascript", "-e 'tell app \"System Events\" to sleep'");
                break;
            case "lock":
                // macOS: lock screen via CGSession
                Run("/System/Library/CoreServices/Menu Extras/User.menu/Contents/Resources/CGSession", "-suspend");
                break;
            case "mute":
                Run("osascript", "-e 'set volume output muted true'");
                break;
            case "volume_up":
                Run("osascript", "-e 'set volume output volume (output volume of (get volume settings) + 10)'");
                break;
            case "volume_down":
                Run("osascript", "-e 'set volume output volume (output volume of (get volume settings) - 10)'");
                break;
            case "monitor_off":
                Run("pmset", "displaysleepnow");
                break;
            case "monitor_on":
                // Wake display by pressing a key via cgcommand
                Run("caffeinate", "-u -t 1");
                break;
            case "screenshot":
                // Save screenshot to Desktop
                Run("screencapture", "-x ~/Desktop/ha-desklink-screenshot.png");
                break;
            case "screenshot_save":
                var savePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ha-desklink-screenshot.png");
                Run("screencapture", $"-x {savePath}");
                // Upload to HA
                try
                {
                    var app = DeskLinkApp.Instance;
                    if (app != null)
                    {
                        var api = app.GetApiClient();
                        await api.UploadScreenshotAsync(savePath);
                    }
                }
                catch { }
                break;
            case "brightness_up":
                {
                    var current = SensorManager.GetCurrentBrightness();
                    if (current.HasValue)
                        SensorManager.SetBrightness(Math.Min(100, current.Value + 10));
                }
                break;
            case "brightness_down":
                {
                    var current = SensorManager.GetCurrentBrightness();
                    if (current.HasValue)
                        SensorManager.SetBrightness(Math.Max(0, current.Value - 10));
                }
                break;
            default:
                if (command.StartsWith("brightness:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(command.Substring("brightness:".Length), out int value))
                        SensorManager.SetBrightness(Math.Clamp(value, 0, 100));
                    else
                        throw new NotSupportedException($"Ungültiger Helligkeitswert: {command}");
                }
                else
                    throw new NotSupportedException($"{Localization.Get("command_unknown", command)}");
                break;
        }
    }

    private static void Run(string cmd, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Befehl fehlgeschlagen ({cmd} {args}): {ex.Message}");
        }
    }
}