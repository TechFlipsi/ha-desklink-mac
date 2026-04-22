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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Diagnostics;

namespace HaDeskLink.Views;

/// <summary>
/// Main window with HA Dashboard (opens in browser) + status display.
/// ⚠️ COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER
/// 
/// Note: WebView2 for Avalonia is not yet stable on macOS.
/// The dashboard opens in the default browser for now.
/// A future update will embed the dashboard once WebView2 macOS support improves.
/// </summary>
public class MainWindow : Window
{
    private readonly Config _config;
    private readonly HaApiClient _api;

    public MainWindow(Config config, HaApiClient api)
    {
        _config = config;
        _api = api;

        Title = "HA DeskLink macOS ⚠️ Community Test";
        Width = 800;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15,
            Children =
            {
                // Warning banner
                new Border
                {
                    Background = Brushes.OrangeRed,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Child = new TextBlock
                    {
                        Text = "⚠️ COMMUNITY TEST VERSION\nDiese Version wurde NICHT vom Entwickler getestet!\nSie ist auf Community-Testing angewiesen.",
                        Foreground = Brushes.White,
                        FontSize = 14,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                },

                // Title
                new TextBlock
                {
                    Text = "🏠 HA DeskLink macOS",
                    FontSize = 24,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 10, 0, 0)
                },

                // Connection info
                new TextBlock
                {
                    Text = $"Verbunden mit: {_config.HaUrl}",
                    FontSize = 14,
                    Foreground = Brushes.Gray
                },

                // Open Dashboard button
                new Button
                {
                    Content = "📊 Home Assistant Dashboard öffnen",
                    FontSize = 16,
                    Padding = new Thickness(20, 10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = Brushes.DodgerBlue,
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(8)
                },

                // Sensor info
                new TextBlock
                {
                    Text = "📡 Sensoren werden automatisch an Home Assistant gesendet",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 20, 0, 0)
                },

                // Commands info
                new TextBlock
                {
                    Text = "🖥️ Befehle: shutdown, restart, sleep, lock, mute,\n    volume_up, volume_down, monitor_off/on,\n    brightness_up, brightness_down, brightness:N",
                    FontSize = 12,
                    Foreground = Brushes.Gray
                },

                // Version info
                new TextBlock
                {
                    Text = $"Version: {GetVersion()} | Sprache: {Localization.CurrentLanguage}",
                    FontSize = 11,
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 20, 0, 0)
                },
            }
        };

        // Add click handler for dashboard button
        var dashboardBtn = (Button)panel.Children[3];
        dashboardBtn.Click += OnOpenDashboard;

        Content = panel;
    }

    private void OnOpenDashboard(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _config.HaUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Öffnen des Browsers: {ex.Message}");
        }
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = System.IO.Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (System.IO.File.Exists(vfile)) return System.IO.File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "2.2.1";
    }
}