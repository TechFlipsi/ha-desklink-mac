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
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace HaDeskLink;

static class Program
{
    // Global config/api for DeskLinkApp to access
    internal static Config? GlobalConfig;
    internal static HaApiClient? GlobalApi;

    static void Main(string[] args)
    {
        Console.WriteLine("HA DeskLink macOS v" + GetVersion());
        Console.WriteLine("⚠️  COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER");
        Console.WriteLine("⚠️  Diese Version ist auf Community-Testing angewiesen!");
        Console.WriteLine();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var msg = e.ExceptionObject.ToString();
            File.WriteAllText(LogFile(), $"[CRASH] {DateTime.Now}\n{msg}");
            Console.WriteLine($"Schwerer Fehler: {msg}");
        };

        var config = Config.Load();
        var configDir = Config.GetConfigDir();
        var api = new HaApiClient(configDir, config.VerifySsl);

        if (!api.LoadRegistration())
        {
            Console.WriteLine(Localization.Get("setup_welcome"));
            Console.Write("HA URL (z.B. http://192.168.1.100:8123): ");
            var url = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Long-Lived Access Token: ");
            var token = Console.ReadLine()?.Trim() ?? "";
            Console.Write("SSL überprüfen? (j/n) [n]: ");
            var ssl = Console.ReadLine()?.Trim()?.ToLowerInvariant();
            var verifySsl = ssl == "j" || ssl == "y";

            config.HaUrl = url;
            config.HaToken = token;
            config.VerifySsl = verifySsl;
            config.Save();

            try
            {
                Task.Run(async () => await api.RegisterAsync(url, token)).GetAwaiter().GetResult();
                Console.WriteLine("✓ Registriert!");

                // Step 2: MQTT Setup
                RunMqttSetup(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registrierung fehlgeschlagen: {ex.Message}");
                return;
            }
        }

        Localization.LoadLanguage(config.Language);

        GlobalConfig = config;
        GlobalApi = api;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<DeskLinkApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Step 2: MQTT setup wizard (console-based).
    /// </summary>
    private static void RunMqttSetup(Config config)
    {
        Console.WriteLine("\n=== MQTT Setup ===\n");

        Console.WriteLine("MQTT-Funktionen:");
        Console.WriteLine("  Mit MQTT:                          Ohne MQTT:");
        Console.WriteLine("  ✓ PC Status                        ✓ PC Status");
        Console.WriteLine("  ✓ Sensoren                         ✓ Sensoren");
        Console.WriteLine("  ✓ Quick Actions                    ✓ Quick Actions");
        Console.WriteLine("  ✓ Mediensteuerung (Echtzeit)       ✗ Mediensteuerung");
        Console.WriteLine("  ✓ Schnelle Sensor-Updates          ✗ Schnelle Sensor-Updates");
        Console.WriteLine();

        Console.Write("MQTT nutzen? (j/n) [j]: ");
        var useMqtt = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (useMqtt == "n" || useMqtt == "no")
        {
            Console.WriteLine("✓ Ohne MQTT fortfahren.");
            return;
        }

        Console.WriteLine("\nVersuche MQTT-Broker automatisch zu konfigurieren...");

        try
        {
            var fallbackHost = string.IsNullOrEmpty(config.MqttBrokerFallback) ? null : config.MqttBrokerFallback;
            var result = Task.Run(() => MqttSetupHelper.AutoConfigureAsync(config.HaUrl, config.HaToken, fallbackHost)).GetAwaiter().GetResult();

            if (result.Success)
            {
                config.MqttEnabled = true;
                config.MqttBroker = result.BrokerHost ?? "";
                config.MqttPort = result.BrokerPort;
                config.MqttUsername = result.Username ?? "";
                config.MqttPassword = result.Password ?? "";
                config.MqttUseSsl = result.UseSsl;
                config.MqttAutoConfigured = true;
                config.Save();

                Console.WriteLine($"✓ MQTT erfolgreich konfiguriert!");
                Console.WriteLine($"  Broker: {result.BrokerHost}:{result.BrokerPort}");
            }
            else if (result.MosquittoNotInstalled)
            {
                Console.WriteLine("⚠️ Mosquitto MQTT-Broker nicht gefunden.");
                Console.WriteLine("   Installiere den Mosquitto Broker Add-on in Home Assistant:");
                Console.WriteLine("   Einstellungen → Add-ons → Mosquitto Broker installieren & starten.");
                Console.WriteLine();
                Console.Write("Erneut prüfen? (j/n) [n]: ");
                var retry = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (retry == "j" || retry == "y" || retry == "ja" || retry == "yes")
                {
                    RunMqttSetup(config);
                    return;
                }
                Console.WriteLine("✓ Ohne MQTT fortfahren.");
            }
            else
            {
                Console.WriteLine($"⚠️ Fehler: {result.ErrorMessage ?? "Unbekannt"}");
                RunManualMqtt(config);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Fehler: {ex.Message}");
            Console.Write("\nManuelle Konfiguration? (j/n) [j]: ");
            var manual = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (manual != "n" && manual != "no")
                RunManualMqtt(config);
        }
    }

    private static void RunManualMqtt(Config config)
    {
        Console.Write("Broker Host (z.B. homeassistant.local): ");
        var broker = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(broker))
        {
            try { broker = new Uri(config.HaUrl).Host; } catch { broker = "homeassistant.local"; }
            Console.WriteLine($"  Verwende: {broker}");
        }

        Console.Write("Port [1883]: ");
        var portStr = Console.ReadLine()?.Trim();
        if (!int.TryParse(portStr, out var port) || port <= 0) port = 1883;

        Console.Write("Benutzername (optional): ");
        var username = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Passwort (optional): ");
        var password = Console.ReadLine()?.Trim() ?? "";

        Console.Write("SSL/TLS verwenden? (j/n) [n]: ");
        var ssl = Console.ReadLine()?.Trim().ToLowerInvariant();
        var useSsl = ssl == "j" || ssl == "y" || ssl == "ja" || ssl == "yes";

        Console.WriteLine("\nTeste MQTT-Verbindung...");
        var ok = Task.Run(() => MqttSetupHelper.TestConnectionAsync(broker, port,
            string.IsNullOrEmpty(username) ? null : username,
            string.IsNullOrEmpty(password) ? null : password, useSsl)).GetAwaiter().GetResult();

        if (ok)
        {
            config.MqttEnabled = true;
            config.MqttBroker = broker;
            config.MqttPort = port;
            config.MqttUsername = username;
            config.MqttPassword = password;
            config.MqttUseSsl = useSsl;
            config.MqttAutoConfigured = false;
            config.Save();

            Console.WriteLine($"✓ MQTT-Verbindung zu {broker}:{port} erfolgreich!");
        }
        else
        {
            Console.WriteLine($"✗ Verbindung zu {broker}:{port} fehlgeschlagen!");
            Console.Write("\nOhne MQTT fortfahren? (j/n) [j]: ");
            var skip = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (skip != "n" && skip != "no") Console.WriteLine("✓ Ohne MQTT fortfahren.");
        }
    }

    /// <summary>
    /// MQTT configuration utility (callable from app).
    /// </summary>
    public static void ConfigureMqttFromApp(Config config)
    {
        Console.WriteLine("\n📡 MQTT-Konfiguration\n");

        if (config.MqttEnabled && !string.IsNullOrEmpty(config.MqttBroker))
        {
            Console.WriteLine($"MQTT ist aktiviert: {config.MqttBroker}:{config.MqttPort}" +
                (config.MqttUseSsl ? " (SSL)" : ""));
            Console.WriteLine($"Benutzer: {(string.IsNullOrEmpty(config.MqttUsername) ? "(anonym)" : config.MqttUsername)}");

            Console.Write("\nNeu konfigurieren? (j/n) [n]: ");
            var reconfigure = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (reconfigure != "j" && reconfigure != "y" && reconfigure != "ja" && reconfigure != "yes")
                return;
        }

        RunMqttSetup(config);
    }

    public static string LogFile()
    {
        var dir = Config.GetConfigDir();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "error.log");
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(vfile)) return File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "4.4.0";
    }
}