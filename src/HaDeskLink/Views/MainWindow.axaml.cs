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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HaDeskLink.Views;

/// <summary>
/// Main window with HA Dashboard (opens in browser) + status display.
/// ⚠️ COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER
/// 
/// Note: WebView2 for Avalonia is not yet stable on macOS.
/// The dashboard opens in the default browser for now.
/// A future update will embed the dashboard once WebView2 macOS support improves.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Config _config;
    private readonly HaApiClient _api;
    private HaWebSocketClient? _ws;
    private TextBlock? _statusText;
    private Button? _retryButton;

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

                // Quick Actions button
                new Button
                {
                    Content = "⚡ Quick Actions",
                    FontSize = 14,
                    Padding = new Thickness(15, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
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

                // Status text (for login failure messages)
                _statusText = new TextBlock
                {
                    Text = "",
                    FontSize = 13,
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                },

                // Retry button (hidden by default)
                _retryButton = new Button
                {
                    Content = "🔄 Erneut verbinden",
                    FontSize = 14,
                    Padding = new Thickness(15, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CornerRadius = new CornerRadius(8),
                    IsVisible = false
                },
            }
        };

        // Add click handler for dashboard button
        var dashboardBtn = (Button)panel.Children[3];
        dashboardBtn.Click += OnOpenDashboard;

        // Quick Actions button
        var quickActionsBtn = (Button)panel.Children[4];
        quickActionsBtn.Click += OnQuickActions;

        // Retry button
        _retryButton!.Click += OnRetryClicked;

        Content = panel;
    }

    /// <summary>Set the WebSocket reference so the retry button can reset login blocks.</summary>
    public void SetWebSocketClient(HaWebSocketClient ws) { _ws = ws; CheckLoginBlock(); }

    /// <summary>Show retry button and error message when login is blocked.</summary>
    public void CheckLoginBlock()
    {
        if (_ws != null && _ws.IsBlocked)
        {
            _statusText!.Text = "⛔ Login fehlgeschlagen. Token ungültig.\nBitte überprüfe deinen Home Assistant Token in den Einstellungen.";
            _retryButton!.IsVisible = true;
        }
        else
        {
            _statusText!.Text = "";
            _retryButton!.IsVisible = false;
        }
    }

    private async void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        if (_ws == null) return;
        _ws.ResetLoginBlock();
        _statusText!.Text = "🔄 Verbindung wird erneut versucht...";
        _retryButton!.IsVisible = false;
        // Restart WebSocket connection
        _ = Task.Run(async () =>
        {
            try { await _ws.ConnectAsync(); }
            catch (Exception ex) { Console.WriteLine($"WebSocket-Fehler: {ex.Message}"); }
        });
        // Check status after a delay
        await Task.Delay(5000);
        CheckLoginBlock();
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

    private async void OnQuickActions(object? sender, RoutedEventArgs e)
    {
        var actions = LoadQuickActions(_config);

        if (actions.Count == 0)
        {
            var emptyDialog = new Window
            {
                Title = "Quick Actions",
                Width = 350, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "Keine Quick Actions konfiguriert.", FontSize = 14 },
                        new TextBlock { Text = "In config.json hinzufügen:\nFormat: entity_id,name", Foreground = Brushes.Gray, FontSize = 12 },
                        new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center }
                    }
                }
            };
            ((Button)((StackPanel)emptyDialog.Content).Children[2]).Click += (s, args) => emptyDialog.Close();
            await emptyDialog.ShowDialog(this);
            return;
        }

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "⚡ Quick Actions", FontSize = 18, FontWeight = FontWeight.Bold });

        foreach (var action in actions)
        {
            var btn = new Button
            {
                Content = action.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = action
            };
            btn.Click += async (s, args) =>
            {
                var a = (QuickActionItem)((Button)s).Tag!;
                try
                {
                    await _api.ToggleEntityAsync(a.EntityId);
                    btn.Content = $"✓ {a.Name}";
                }
                catch { btn.Content = $"✗ {a.Name}"; }
                await Task.Delay(1000);
                btn.Content = a.Name;
            };
            panel.Children.Add(btn);
        }

        var dialog = new Window
        {
            Title = "Quick Actions",
            Width = 350,
            Height = Math.Max(150, 80 + actions.Count * 50),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        await dialog.ShowDialog(this);
    }

    private static List<QuickActionItem> LoadQuickActions(Config config)
    {
        var result = new List<QuickActionItem>();
        try
        {
            var arr = System.Text.Json.JsonDocument.Parse(config.QuickActions).RootElement;
            foreach (var item in arr.EnumerateArray())
            {
                var entityId = item.TryGetProperty("entityId", out var eid) ? eid.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? entityId : entityId;
                if (!string.IsNullOrEmpty(entityId))
                    result.Add(new QuickActionItem(entityId, name));
            }
        }
        catch { }
        return result;
    }
}

public class QuickActionItem
{
    public string EntityId { get; }
    public string Name { get; }
    public QuickActionItem(string entityId, string name) { EntityId = entityId; Name = name; }
}