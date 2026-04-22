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
            var ssl = Console.ReadLine()?.Trim().ToLowerInvariant();
            var verifySsl = ssl == "j" || ssl == "y";

            config.HaUrl = url;
            config.HaToken = token;
            config.VerifySsl = verifySsl;
            config.Save();

            try
            {
                api.RegisterAsync(url, token).Wait();
                Console.WriteLine("✓ Registriert!");
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
        return "2.2.1";
    }
}